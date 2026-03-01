namespace Outbox.Api.Tenancy
{
    public interface ITenantContext
    {
        string? TenantId { get; }
        bool IsBackgroundWorker { get; }
    }
}