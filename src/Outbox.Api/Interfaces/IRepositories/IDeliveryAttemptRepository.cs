using Outbox.Api.Entities;

namespace Outbox.Api.Interfaces.IRepositories;

public interface IDeliveryAttemptRepository
{
    Task<List<DeliveryAttempt>> GetByDeliveryAsync(Guid outboxDeliveryId,int skip,int take,CancellationToken ct = default);
    Task<List<DeliveryAttempt>> GetByMessageAsync(Guid outboxMessageId, int skip, int take, CancellationToken ct = default);
}