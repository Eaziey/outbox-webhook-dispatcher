using Outbox.Api.Data;
using Outbox.Api.Interfaces.IRepositories;

namespace Outbox.Api.Repositories;
public class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    private readonly AppDbContext _dbContext = dbContext;
    public Task<int> SaveChangesAsync(CancellationToken ct = default){
        return _dbContext.SaveChangesAsync(ct);
    }
}