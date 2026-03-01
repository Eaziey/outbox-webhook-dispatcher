namespace Outbox.Api.Entities;

public class OutboxDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OutboxMessageId { get; set; }
    public OutboxMessage OutboxMessage { get; set; } = default!;
    public Guid SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = default!;
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public int AttemptCount { get; set; } = 0;
    public DateTime? NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptUtc { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastError { get; set; }
    public string? TenantId { get; set; }
    public string? SubjectKey { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<DeliveryAttempt> Attempts { get; set; } = new List<DeliveryAttempt>();

}