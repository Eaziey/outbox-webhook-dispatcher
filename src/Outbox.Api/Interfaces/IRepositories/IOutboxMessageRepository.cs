using Outbox.Api.Entities;
using System.Text.Json;

namespace Outbox.Api.Interfaces.IRepositories;

public interface IOutboxMessageRepository
{
    Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task UpdateAsync(OutboxMessage message, CancellationToken ct = default);
    Task<OutboxMessage> EnqueueAsync(string eventType,JsonElement payload,string? subjectKey = null,string? idempotencyKey = null,string? extraHeadersJson = null,CancellationToken ct = default);

    Task<List<OutboxMessage>> ListAsync(int skip,int take,CancellationToken ct = default);
    Task<List<OutboxMessage>> ListBySubjectAsync(string subjectKey,int skip,int take,CancellationToken ct = default);
}
