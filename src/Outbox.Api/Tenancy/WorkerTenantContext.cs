namespace Outbox.Api.Tenancy;

public class WorkerTenantContext : ITenantContext
{ 
    public string? TenantId { get; }
    public bool IsBackgroundWorker => true;
    public WorkerTenantContext(string? tenantId = null)
    {
        TenantId = tenantId;
    }
} 