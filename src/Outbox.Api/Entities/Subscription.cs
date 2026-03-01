namespace Outbox.Api.Entities;

public class Subscription
{
    public Guid Id {get; set;} = Guid.NewGuid();
    public string Endpoint {get; set;} = default!;
    public string Secret {get; set;} = default!;
    public bool IsActive {get; set;} = true;
    public string? TenantId { get; set; }
    public int? MaxConcurrency { get; set; }
    public int? MaxAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}