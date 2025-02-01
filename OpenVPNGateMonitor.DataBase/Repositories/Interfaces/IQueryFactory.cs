using OpenVPNGateMonitor.DataBase.Repositories.Queries.Interfaces;

namespace OpenVPNGateMonitor.DataBase.Repositories.Interfaces;

public interface IQueryFactory
{
    IQuery<T> GetQuery<T>() where T : class;
}