using Outbox.Api.Entities;

namespace Outbox.Api.Interfaces.IServices;
public interface IWebhookSender
{
    Task<(int statusCode, string? error)> SendAsync(Subscription sub, OutboxMessage msg, CancellationToken ct);
}