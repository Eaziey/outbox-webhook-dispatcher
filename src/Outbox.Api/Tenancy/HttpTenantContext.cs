using Microsoft.AspNetCore.Http;

namespace Outbox.Api.Tenancy
{
    public class HttpTenantContext : ITenantContext
    {
        public string? TenantId { get; }
        public bool IsBackgroundWorker => false;
        public HttpTenantContext(IHttpContextAccessor accessor)
        {
            // Read from header: X-Tenant-Id
            TenantId = accessor.HttpContext?
                .Request.Headers["X-Tenant-Id"]
                .FirstOrDefault();
        }
    }
}