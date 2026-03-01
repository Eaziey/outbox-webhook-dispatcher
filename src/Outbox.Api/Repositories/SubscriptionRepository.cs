using Microsoft.EntityFrameworkCore;
using Outbox.Api.Data;
using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IRepositories;
using Outbox.Api.Tenancy;
using static Outbox.Api.Tenancy.TenantQueryableExtensions;

namespace Outbox.Api.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenant;

    public SubscriptionRepository(AppDbContext dbContext, ITenantContext tenant)
    {
        _dbContext = dbContext;
        _tenant = tenant;
    }

    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var subscriptionQuery =
            from subscription in _dbContext.Subscriptions.ForTenant(_tenant)
            where subscription.Id == id
            select subscription;

        return subscriptionQuery.FirstOrDefaultAsync(ct);
    }

    public Task<List<Subscription>> ListAsync(bool? isActive, CancellationToken ct = default)
    {
        var subscriptionsQuery =
            from subscription in _dbContext.Subscriptions.ForTenant(_tenant)
            select subscription;

        if (isActive.HasValue)
        {
            subscriptionsQuery =
                from subscription in subscriptionsQuery
                where subscription.IsActive == isActive.Value
                select subscription;
        }

        subscriptionsQuery =
            from subscription in subscriptionsQuery
            orderby subscription.CreatedAtUtc descending
            select subscription;

        return subscriptionsQuery.AsNoTracking().ToListAsync(ct);
    }

    public Task AddAsync(Subscription subscription, CancellationToken ct = default)
    {
        subscription.TenantId = _tenant.TenantId;
        subscription.CreatedAtUtc = DateTime.UtcNow;
        subscription.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.Subscriptions.Add(subscription);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Subscription subscription, CancellationToken ct = default)
    {
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.Subscriptions.Update(subscription);

        return Task.CompletedTask;
    }

    public Task<List<Subscription>> ListActiveAsync(CancellationToken ct = default)
    {
        var activeSubscriptionsQuery =
            from subscription in _dbContext.Subscriptions.ForTenant(_tenant)
            where subscription.IsActive
            orderby subscription.CreatedAtUtc
            select subscription;

        return activeSubscriptionsQuery.AsNoTracking().ToListAsync(ct);
    }
}