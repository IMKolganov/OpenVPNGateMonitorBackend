using OpenVPNGateMonitor.DataBase.Contexts;
using OpenVPNGateMonitor.DataBase.Repositories;
using OpenVPNGateMonitor.DataBase.Repositories.Interfaces;
using OpenVPNGateMonitor.DataBase.Repositories.Queries.Interfaces;

namespace OpenVPNGateMonitor.DataBase.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IQueryFactory _queryFactory;

    public UnitOfWork(ApplicationDbContext context, IRepositoryFactory repositoryFactory, IQueryFactory queryFactory)
    {
        _context = context;
        _repositoryFactory = repositoryFactory;
        _queryFactory = queryFactory;
    }

    public IRepository<T> GetRepository<T>() where T : class
    {
        return _repositoryFactory.GetRepository<T>();
    }

    public IQuery<T> GetQuery<T>() where T : class
    {
        return _queryFactory.GetQuery<T>();
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}