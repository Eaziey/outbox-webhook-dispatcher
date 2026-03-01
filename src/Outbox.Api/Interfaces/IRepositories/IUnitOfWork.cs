namespace Outbox.Api.Interfaces.IRepositories;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}