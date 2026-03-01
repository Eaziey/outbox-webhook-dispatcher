using Microsoft.Extensions.Options;
using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IRepositories;
using Outbox.Api.Interfaces.IServices;
using Outbox.Api.Options;


namespace Outbox.Api.Services;

public class OutboxDispatcherService(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherService> logger, IOptions<OutboxDispatcherOptions> options) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<OutboxDispatcherService> _logger = logger;
    private readonly OutboxDispatcherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxDispatcherService started");

        var leaseBatchSize = _options.LeaseBatchSize;
        var leaseDuration = TimeSpan.FromSeconds(_options.LeaseDurationSeconds);
        var loopDelay = TimeSpan.FromMilliseconds(_options.LoopDelayMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var deliveryRepo = scope.ServiceProvider.GetRequiredService<IOutboxDeliveryRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var sender = scope.ServiceProvider.GetRequiredService<IWebhookSender>();
                var leased = await deliveryRepo.LeaseDueAsync(leaseBatchSize, leaseDuration, stoppingToken);

                if (leased.Count == 0)
                {
                    await Task.Delay(loopDelay, stoppingToken);
                    continue;
                }

                foreach (var delivery in leased)
                {
                    var subscription = delivery.Subscription;
                    var message = delivery.OutboxMessage;

                    try
                    {
                        var (statusCode, error) = await sender.SendAsync(subscription, message, stoppingToken);
                        bool success = statusCode >= 200 && statusCode < 300;

                        DateTime? nextAttemptUtc = null;
                        DeliveryStatus finalStatus;

                        if (success)
                        {
                            finalStatus = DeliveryStatus.Sent;
                        }
                        else
                        {
                            var nextAttemptNumber = delivery.AttemptCount + 1;
                            var seconds = Math.Min(_options.MaxBackoffSeconds, (int)Math.Pow(2, nextAttemptNumber - 1) * _options.BackoffBaseSeconds);
                            nextAttemptUtc = DateTime.UtcNow.AddSeconds(seconds);

                            var maxAttempts = subscription.MaxAttempts ?? _options.DefaultMaxAttempts;

                            finalStatus = nextAttemptNumber >= maxAttempts ? DeliveryStatus.Dead : DeliveryStatus.Failed;
                        }

                        await deliveryRepo.RecordAttemptAsync(
                            deliveryId: delivery.Id,
                            statusCode: statusCode,
                            error: error,
                            responseBody: null,
                            durationMs: null,
                            consideredRetryable: !success,
                            nextAttemptUtc: nextAttemptUtc,
                            finalStatusIfAny: finalStatus,
                            ct: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending delivery {DeliveryId}", delivery.Id);

                        var nextAttemptNumber = delivery.AttemptCount + 1;
                        var seconds = Math.Min(_options.MaxBackoffSeconds, (int)Math.Pow(2, nextAttemptNumber - 1) * _options.BackoffBaseSeconds);

                        var nextAttemptUtc = DateTime.UtcNow.AddSeconds(seconds);
                        int maxAttempts = subscription.MaxAttempts ?? _options.DefaultMaxAttempts;
                        var finalStatus = nextAttemptNumber >= maxAttempts ? DeliveryStatus.Dead : DeliveryStatus.Failed;

                        await deliveryRepo.RecordAttemptAsync(
                            deliveryId: delivery.Id,
                            statusCode: null,
                            error: ex.Message,
                            responseBody: null,
                            durationMs: null,
                            consideredRetryable: true,
                            nextAttemptUtc: nextAttemptUtc,
                            finalStatusIfAny: finalStatus,
                            ct: stoppingToken);
                    }
                }

                await unitOfWork.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // grace
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatcher loop error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }

            await Task.Delay(loopDelay, stoppingToken);
        }

        _logger.LogInformation("OutboxDispatcherService stopping");
    }
}