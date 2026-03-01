using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IServices;
using Outbox.Api.Utils;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Outbox.Api.Services;
public class WebhookSender: IWebhookSender{

    private readonly HttpClient _http;
    private readonly IHmacSigner _signer;
    private readonly ILogger<WebhookSender> _logger;
    private const int MaxStoredBody = 4000;

    public WebhookSender(HttpClient http, IHmacSigner signer, ILogger<WebhookSender> logger)
    {
        _http = http;
        _signer = signer;
        _logger = logger;
    }

    public async Task<(int statusCode, string? error)> SendAsync(Subscription sub, OutboxMessage msg, CancellationToken token)
    {
        var body = msg.PayloadJson ?? "{}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = _signer.CreateSignature(sub.Secret, body, timestamp);

        using var request = new HttpRequestMessage(HttpMethod.Post, sub.Endpoint);
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("X-Signature-Version", string.IsNullOrWhiteSpace(msg.SignatureVersion) ? "v1" : msg.SignatureVersion);
        
        if(!string.IsNullOrWhiteSpace(msg.SignatureSecretId))
        {
            request.Headers.Add("X-Signature-Id", msg.SignatureSecretId);
        }

        request.Headers.Add("X-Timestamp", timestamp);
        request.Headers.Add("Idempotency-Key", msg.IdempotencyKey);
        request.Headers.Add("X-Event-Type", msg.EventType);

        if (!string.IsNullOrWhiteSpace(msg.SubjectKey))
        {
            request.Headers.Add("X-Subject-Key", msg.SubjectKey);
        }

        if (!string.IsNullOrWhiteSpace(msg.TenantId))
        {
            request.Headers.Add("X-Tenant-Id", msg.TenantId);
        }

        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        if (!string.IsNullOrWhiteSpace(msg.ExtraHeadersJson))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(msg.ExtraHeadersJson);
                if (headers is not null)
                {
                    foreach (var (key, value) in headers){

                        request.Headers.TryAddWithoutValidation(key, value);
                    }
                }

            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Invalid ExtraHeadersJson on OutboxMessage {MessageId}", msg.Id);
            }
        }

        try
        {
            using var response = await _http.SendAsync(request, token);
            
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(token);
                return ((int)response.StatusCode, StringUtils.Truncate(err, MaxStoredBody));
            }

            return ((int)response.StatusCode, null);

        }
        catch (TaskCanceledException ex) when (!token.IsCancellationRequested)
        {
            return (0, "request timeout: " + ex.Message);
        }

        catch(Exception ex)
        {
            return (0, ex.Message);
        }

    }
}
