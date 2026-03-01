using Microsoft.EntityFrameworkCore;
using Outbox.Api.Data;
using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IRepositories;
using Outbox.Api.Tenancy;
using static Outbox.Api.Tenancy.TenantQueryableExtensions;

namespace Outbox.Api.Repositories;

public class DeliveryAttemptRepository : IDeliveryAttemptRepository
{

    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenant;

    public DeliveryAttemptRepository(AppDbContext db, ITenantContext tenant)
    {
        _dbContext = db;
        _tenant = tenant;
    }

    public Task<List<DeliveryAttempt>> GetByDeliveryAsync(Guid outboxDeliveryId,int skip,int take,CancellationToken ct = default)
    {
        var query =
            from attempt in _dbContext.DeliveryAttempts
            join delivery in _dbContext.OutboxDeliveries.ForTenant(_tenant)
                on attempt.OutboxDeliveryId equals delivery.Id
            where attempt.OutboxDeliveryId == outboxDeliveryId
            orderby attempt.AttemptedAtUtc descending
            select attempt;


        return query.Skip(skip).Take(take).AsNoTracking().ToListAsync(ct);
    }

    public Task<List<DeliveryAttempt>> GetByMessageAsync(Guid outboxMessageId, int skip, int take, CancellationToken ct = default)
    {
        var query =
            from attempt in _dbContext.DeliveryAttempts
            join delivery in _dbContext.OutboxDeliveries.ForTenant(_tenant)
                on attempt.OutboxDeliveryId equals delivery.Id
            where delivery.OutboxMessageId == outboxMessageId
            orderby attempt.AttemptedAtUtc descending
            select attempt;


        return query.Skip(skip).Take(take).AsNoTracking().ToListAsync(ct);
    }

}