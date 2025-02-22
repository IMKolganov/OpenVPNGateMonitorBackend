using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Services.Api.Auth;

public class ApplicationService : IApplicationService
{
    private readonly IUnitOfWork _unitOfWork;

    public ApplicationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisteredApp> RegisterApplicationAsync(string name)
    {
        var existRegisteredApp = await _unitOfWork.GetQuery<RegisteredApp>()
            .AsQueryable()
            .Where(x => x.Name == name)
            .FirstOrDefaultAsync();

        if (existRegisteredApp != null)
        {
            throw new Exception("");
        }

        var registeredApp = new RegisteredApp()
        {
            Name = name
        };
        
        var repositoryRegisterApp = _unitOfWork.GetRepository<RegisteredApp>();
        await repositoryRegisterApp.AddAsync(registeredApp);
        await _unitOfWork.SaveChangesAsync();
        return registeredApp;
    }

    public async Task<RegisteredApp?> GetApplicationByClientIdAsync(string clientId)
    {
        return await _unitOfWork.GetQuery<RegisteredApp>()
            .AsQueryable()
            .Where(x => x.ClientId == clientId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<RegisteredApp>> GetAllApplicationsAsync()
    {
        return await _unitOfWork.GetQuery<RegisteredApp>()
            .AsQueryable()
            .ToListAsync();
    }

    public async Task<bool> RevokeApplicationAsync(string clientId)
    {
        var registeredApp = await _unitOfWork.GetQuery<RegisteredApp>()
            .AsQueryable()
            .Where(x => x.ClientId == clientId)
            .FirstOrDefaultAsync();

        if (registeredApp == null)
        {
            throw new Exception(""); 
        }

        registeredApp.IsRevoked = true;
        
        var repositoryRegisterApp = _unitOfWork.GetRepository<RegisteredApp>();
        repositoryRegisterApp.Update(registeredApp);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
