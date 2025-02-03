using System.Collections.Concurrent;
using OpenVPNGateMonitor.DataBase.Contexts;
using OpenVPNGateMonitor.DataBase.Repositories.Interfaces;
using OpenVPNGateMonitor.DataBase.Repositories.Queries.Interfaces;

namespace OpenVPNGateMonitor.DataBase.Repositories.Queries;

public class QueryFactory : IQueryFactory
{
    private readonly ApplicationDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _queries = new();

    public QueryFactory(ApplicationDbContext context)
    {
        _context = context;
    }

    public IQuery<T> GetQuery<T>() where T : class
    {
        return (IQuery<T>)_queries.GetOrAdd(typeof(T), _ => new Query<T>(_context));
    }
}