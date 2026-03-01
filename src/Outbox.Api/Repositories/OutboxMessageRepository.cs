using Microsoft.EntityFrameworkCore;
using Outbox.Api.Data;
using Outbox.Api.Entities;
using Outbox.Api.Interfaces.IRepositories;
using Outbox.Api.Tenancy;
using static Outbox.Api.Tenancy.TenantQueryableExtensions;
using System.Text.Json;

namespace Outbox.Api.Repositories;

public class OutboxMessageRepository : IOutboxMessageRepository
{
   private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenant;

    public OutboxMessageRepository(AppDbContext db, ITenantContext tenant)
    {
        _dbContext = db;
        _tenant = tenant;
    }


    public Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var query =
            from message in _dbContext.OutboxMessages.ForTenant(_tenant)
            where message.Id == id
            select message;

        return query.Include(message => message.Deliveries).FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default)
    {
        var existsQuery = 
            from message in _dbContext.OutboxMessages.ForTenant(_tenant)
            where message.Id == id
            select message.Id;

        return existsQuery.AnyAsync(ct);
    }

    public Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var existsQuery =
            from message in _dbContext.OutboxMessages.ForTenant(_tenant)
            where message.IdempotencyKey == idempotencyKey
            select message.Id;

        return existsQuery.AnyAsync(ct);
    }

    public Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        message.TenantId = _tenant.TenantId;
        message.CreatedAtUtc = DateTime.UtcNow;
        message.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.OutboxMessages.Add(message);

        return Task.CompletedTask;
    }
    
    public Task UpdateAsync(OutboxMessage message, CancellationToken ct = default)
    {
        message.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.OutboxMessages.Update(message);

        return Task.CompletedTask;
    }

    public async Task<OutboxMessage> EnqueueAsync(string eventType, JsonElement payload,string? subjectKey = null,string? idempotencyKey = null,string? extraHeadersJson = null,CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var idempotencyExists = await ExistsByIdempotencyKeyAsync(idempotencyKey!, ct);
            if (idempotencyExists)
            {
                throw new InvalidOperationException($"Idempotency key already exists in this tenant: {idempotencyKey}");
            }
        }

        var message = new OutboxMessage
        {
            EventType = eventType,
            PayloadJson = payload.GetRawText(),
            IdempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString(),
            TenantId = _tenant.TenantId,
            SubjectKey = subjectKey,
            ExtraHeadersJson = extraHeadersJson,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.OutboxMessages.Add(message);
        return message;
    }
    public Task<List<OutboxMessage>> ListAsync(int skip, int take, CancellationToken ct = default)
    {
        var messagesQuery =
            from message in _dbContext.OutboxMessages.ForTenant(_tenant)
            orderby message.CreatedAtUtc descending
            select message;

        return messagesQuery.Include(m => m.Deliveries).AsNoTracking().Skip(skip).Take(take).ToListAsync(ct);
    }

    public Task<List<OutboxMessage>> ListBySubjectAsync(string subjectKey, int skip, int take, CancellationToken ct = default)
    {
        var messagesQuery =
            from message in _dbContext.OutboxMessages.ForTenant(_tenant)
            where message.SubjectKey == subjectKey
            orderby message.CreatedAtUtc descending
            select message;

        return messagesQuery.Include(m => m.Deliveries).AsNoTracking().Skip(skip).Take(take).ToListAsync(ct);
    }

}
