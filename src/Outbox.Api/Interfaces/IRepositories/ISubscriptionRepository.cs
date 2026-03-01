using Microsoft.AspNetCore.Mvc;
using Outbox.Api.Entities;

namespace Outbox.Api.Interfaces.IRepositories;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Subscription>> ListAsync(bool? isActive, CancellationToken ct = default);
    Task AddAsync(Subscription sub, CancellationToken ct = default);
    Task UpdateAsync(Subscription sub, CancellationToken ct = default);
    Task<List<Subscription>> ListActiveAsync(CancellationToken ct = default);
}


