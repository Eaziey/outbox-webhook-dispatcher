using Outbox.Api.Entities;

namespace Outbox.Api.Interfaces.IRepositories;

public interface IOutboxDeliveryRepository
{
    Task<OutboxDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<int> CreateDeliveriesForMessageAsync(OutboxMessage message, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxDelivery>> LeaseDueAsync( int batchSize, TimeSpan leaseDuration, CancellationToken ct = default);
    Task RecordAttemptAsync(Guid deliveryId, int? statusCode, string? error, string? responseBody, int? durationMs, bool consideredRetryable, DateTime? nextAttemptUtc, DeliveryStatus finalStatusIfAny, CancellationToken ct = default);

    Task RequeueAsync(Guid deliveryId, DateTime? nextAttemptUtc, CancellationToken ct = default);

    Task<List<OutboxDelivery>> ListByMessageAsync( Guid outboxMessageId, int skip, int take, CancellationToken ct = default);
}