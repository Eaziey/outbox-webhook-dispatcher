namespace Outbox.Api.Entities;

public class DeliveryAttempt
{
    
    public long Id { get; set; }
    public Guid OutboxDeliveryId { get; set; }
    public OutboxDelivery OutboxDelivery { get; set; } = default!;
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
    public int? StatusCode { get; set; }
    public string? Error { get; set; }
    public int AttemptNumber { get; set; }
    public int? DurationMs { get; set; }
    public string? ResponseBody { get; set; } // consider truncating before saving
    public bool? ConsideredRetryable { get; set; }
    public string? TenantId { get; set; }

}