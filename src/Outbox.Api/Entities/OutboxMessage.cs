namespace Outbox.Api.Entities;
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString();
    public string? TenantId { get; set; }
    public string? SubjectKey { get; set; }
    public string? SignatureVersion { get; set; }
    public string? SignatureSecretId { get; set; } 
    public string? ExtraHeadersJson { get; set; } 
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<OutboxDelivery> Deliveries { get; set; } = new List<OutboxDelivery>();
}