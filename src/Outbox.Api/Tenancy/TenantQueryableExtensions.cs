using Microsoft.EntityFrameworkCore;

namespace Outbox.Api.Tenancy
{
    public static class TenantQueryableExtensions
    {
        public static IQueryable<T> ForTenant<T>(this IQueryable<T> query, ITenantContext tenant) 
        where T : class
        {
            if (tenant.IsBackgroundWorker) return query;

            if (string.IsNullOrWhiteSpace(tenant.TenantId))
            {
                return query.Where(_ => false); // defensive: match none
            }

            return query.Where(e =>
                EF.Property<string?>(e, nameof(Outbox.Api.Entities.OutboxMessage.TenantId)) == tenant.TenantId
                || EF.Property<string?>(e, "TenantId") == tenant.TenantId
            );

        }
    }
}