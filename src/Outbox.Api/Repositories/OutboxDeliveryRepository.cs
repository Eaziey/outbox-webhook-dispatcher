using Microsoft.EntityFrameworkCore;
using Outbox.Api.Data;
using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IRepositories;
using Outbox.Api.Tenancy;

namespace Outbox.Api.Repositories;

public class OutboxDeliveryRepository : IOutboxDeliveryRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenant;

    public OutboxDeliveryRepository(AppDbContext dbContext, ITenantContext tenant)
    {
        _dbContext = dbContext;
        _tenant = tenant;
    }

    public Task<OutboxDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var query =
            from delivery in _dbContext.OutboxDeliveries.ForTenant(_tenant)
            where delivery.Id == id
            select delivery;

        return query
            .Include(delivery => delivery.Subscription)
            .Include(delivery => delivery.OutboxMessage)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> CreateDeliveriesForMessageAsync(OutboxMessage message, CancellationToken ct = default)
    {
        // Create one delivery per active subscription in this tenant (HTTP scope filters by tenant)
        var activeSubscriptions = await _dbContext.Subscriptions
            .ForTenant(_tenant)
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        foreach (var subscription in activeSubscriptions)
        {
            var delivery = new OutboxDelivery
            {
                OutboxMessageId = message.Id,
                SubscriptionId = subscription.Id,
                Status = DeliveryStatus.Pending,
                AttemptCount = 0,
                NextAttemptUtc = DateTime.UtcNow,
                TenantId = message.TenantId,          // keep row-level tenant isolation
                SubjectKey = message.SubjectKey,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.OutboxDeliveries.Add(delivery);
        }

        return activeSubscriptions.Count;
    }

    public async Task<IReadOnlyList<OutboxDelivery>> LeaseDueAsync(int batchSize, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;

        // 1) Find due delivery candidates with active subscriptions (worker: all tenants; HTTP: filtered)
        var dueCandidatesQuery =
            from delivery in _dbContext.OutboxDeliveries.ForTenant(_tenant)
            join subscription in _dbContext.Subscriptions.ForTenant(_tenant)
                on delivery.SubscriptionId equals subscription.Id
            where (delivery.Status == DeliveryStatus.Pending || delivery.Status == DeliveryStatus.Failed)
                  && delivery.NextAttemptUtc != null
                  && delivery.NextAttemptUtc <= nowUtc
                  && subscription.IsActive
            orderby delivery.NextAttemptUtc, delivery.CreatedAtUtc
            select new { delivery, subscription };

        var candidatePairs = await dueCandidatesQuery
            .Take(batchSize * 3) // overfetch to enforce per-subscription maxConcurrency
            .ToListAsync(ct);

        // 2) Current 'Sending' counts by subscription (enforce maxConcurrency)
        var sendingCounts = await _dbContext.OutboxDeliveries
            .ForTenant(_tenant)
            .Where(d => d.Status == DeliveryStatus.Sending)
            .GroupBy(d => d.SubscriptionId)
            .Select(g => new { SubscriptionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubscriptionId, x => x.Count, ct);

        var shortlist = new List<OutboxDelivery>(batchSize);

        foreach (var pair in candidatePairs)
        {
            sendingCounts.TryGetValue(pair.subscription.Id, out var currentSending);
            var maxConc = pair.subscription.MaxConcurrency;

            if (maxConc.HasValue && currentSending >= maxConc.Value)
            {
                continue; // capacity reached for this subscription
            }

            shortlist.Add(pair.delivery);
            sendingCounts[pair.subscription.Id] = currentSending + 1;

            if (shortlist.Count == batchSize)
            {
                break;
            }
        }

        if (shortlist.Count == 0)
        {
            return Array.Empty<OutboxDelivery>();
        }

        // 3) Lease (mark as Sending and push NextAttemptUtc forward as a lease-until time)
        var leaseUntilUtc = nowUtc.Add(leaseDuration);
        var leasedIds = new List<Guid>(shortlist.Count);

        // We’ll lease each row with a concurrency predicate so only due & eligible rows transition to Sending
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        foreach (var d in shortlist)
        {
            // Build the base query with tenant visibility
            var candidate = _dbContext.OutboxDeliveries
                .ForTenant(_tenant)
                .Where(x => x.Id == d.Id)
                .Where(x =>
                    (x.Status == DeliveryStatus.Pending || x.Status == DeliveryStatus.Failed) &&
                    x.NextAttemptUtc != null &&
                    x.NextAttemptUtc <= nowUtc);

            // Execute atomic update; affected will be 1 if we successfully leased it
            var affected = await candidate.ExecuteUpdateAsync(updates => updates
                .SetProperty(x => x.Status, DeliveryStatus.Sending)
                .SetProperty(x => x.NextAttemptUtc, leaseUntilUtc)  // using NextAttemptUtc as a lease-until
                .SetProperty(x => x.UpdatedAtUtc, nowUtc), ct);

            if (affected == 1)
            {
                leasedIds.Add(d.Id);
            }
        }

        await tx.CommitAsync(ct);

        if (leasedIds.Count == 0)
        {
            return Array.Empty<OutboxDelivery>();
        }

        // 4) Load the leased rows with includes for sending
        var leased = await _dbContext.OutboxDeliveries
            .Where(x => leasedIds.Contains(x.Id))
            .Include(d => d.Subscription)
            .Include(d => d.OutboxMessage)
            .ToListAsync(ct);

        return leased;
    }

    public async Task RecordAttemptAsync(
        Guid deliveryId,
        int? statusCode,
        string? error,
        string? responseBody,
        int? durationMs,
        bool consideredRetryable,
        DateTime? nextAttemptUtc,
        DeliveryStatus finalStatusIfAny,
        CancellationToken ct = default)
    {
        var deliveryEntity = await _dbContext.OutboxDeliveries
            .ForTenant(_tenant)
            .Where(d => d.Id == deliveryId)
            .SingleAsync(ct);

        var nowUtc = DateTime.UtcNow;

        var attempt = new DeliveryAttempt
        {
            OutboxDeliveryId = deliveryEntity.Id,
            AttemptedAtUtc = nowUtc,
            AttemptNumber = deliveryEntity.AttemptCount + 1,
            StatusCode = statusCode,
            Error = error,
            ResponseBody = responseBody,
            DurationMs = durationMs,
            ConsideredRetryable = consideredRetryable,
            TenantId = deliveryEntity.TenantId
        };

        _dbContext.DeliveryAttempts.Add(attempt);

        deliveryEntity.AttemptCount++;
        deliveryEntity.LastAttemptUtc = nowUtc;
        deliveryEntity.LastStatusCode = statusCode;
        deliveryEntity.LastError = error;
        deliveryEntity.UpdatedAtUtc = nowUtc;

        if (finalStatusIfAny is DeliveryStatus.Sent or DeliveryStatus.Dead or DeliveryStatus.Failed)
        {
            deliveryEntity.Status = finalStatusIfAny;
            deliveryEntity.NextAttemptUtc = finalStatusIfAny == DeliveryStatus.Sent ? null : nextAttemptUtc;
        }
        else
        {
            deliveryEntity.NextAttemptUtc = nextAttemptUtc;
        }
    }

    public async Task RequeueAsync(Guid deliveryId, DateTime? nextAttemptUtc, CancellationToken ct = default)
    {
        var delivery = await _dbContext.OutboxDeliveries
            .ForTenant(_tenant)
            .Where(d => d.Id == deliveryId)
            .SingleAsync(ct);

        delivery.Status = DeliveryStatus.Pending;
        delivery.NextAttemptUtc = nextAttemptUtc ?? DateTime.UtcNow;
        delivery.UpdatedAtUtc = DateTime.UtcNow;
    }

    public Task<List<OutboxDelivery>> ListByMessageAsync(Guid outboxMessageId, int skip, int take, CancellationToken ct = default)
    {
        var deliveriesQuery =
            from delivery in _dbContext.OutboxDeliveries.ForTenant(_tenant)
            where delivery.OutboxMessageId == outboxMessageId
            orderby delivery.CreatedAtUtc descending
            select delivery;

        return deliveriesQuery.AsNoTracking().Skip(skip).Take(take).ToListAsync(ct);
    }
}