using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.Services.Api.Auth;

public class ApplicationService : IApplicationService
{
    private readonly IUnitOfWork _unitOfWork;

    public ApplicationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ClientApplication> RegisterApplicationAsync(string name)
    {
        var existClientApplication = await _unitOfWork.GetQuery<ClientApplication>()
            .AsQueryable()
            .Where(x => x.Name == name)
            .FirstOrDefaultAsync();

        if (existClientApplication != null)
        {
            throw new Exception("ClientApplication already exists");
        }

        var clientApplication = new ClientApplication()
        {
            Name = name
        };
        
        var repositoryRegisterApp = _unitOfWork.GetRepository<ClientApplication>();
        await repositoryRegisterApp.AddAsync(clientApplication);
        await _unitOfWork.SaveChangesAsync();
        return clientApplication;
    }

    public async Task<ClientApplication?> GetApplicationByClientIdAsync(string clientId)
    {
        return await _unitOfWork.GetQuery<ClientApplication>()
            .AsQueryable()
            .Where(x => x.ClientId == clientId)
            .FirstOrDefaultAsync();
    }
    
    public async Task<ClientApplication?> GetApplicationSystemByClientIdAsync(string clientId)
    {
        return await _unitOfWork.GetQuery<ClientApplication>()
            .AsQueryable()
            .Where(x => x.ClientId == clientId && x.IsSystem && x.IsRevoked == false)
            .FirstOrDefaultAsync();
    }
    
    public async Task<bool> IsSystemApplicationSetAsync()
    {
        var systemApp = await _unitOfWork.GetQuery<ClientApplication>()
            .AsQueryable()
            .FirstOrDefaultAsync(x => x.IsSystem);

        return systemApp != null && !string.IsNullOrEmpty(systemApp.ClientSecret);
    }

    public async Task<List<ClientApplication>> GetAllApplicationsAsync()
    {
        return await _unitOfWork.GetQuery<ClientApplication>()
            .AsQueryable()
            .ToListAsync();
    }
    
    public async Task<ClientApplication> UpdateApplicationAsync(ClientApplication clientApplication)
    {
        var repository = _unitOfWork.GetRepository<ClientApplication>();
        repository.Update(clientApplication);
        await _unitOfWork.SaveChangesAsync();
    
        return clientApplication;
    }

    public async Task<bool> RevokeApplicationAsync(string clientId)
    {
        var clientApplication = await _unitOfWork.GetQuery<ClientApplication>()
            .AsQueryable()
            .Where(x => x.ClientId == clientId)
            .FirstOrDefaultAsync();

        if (clientApplication == null)
        {
            throw new InvalidOperationException("ClientApplication not found");
        }

        clientApplication.IsRevoked = true;
        
        var repositoryRegisterApp = _unitOfWork.GetRepository<ClientApplication>();
        repositoryRegisterApp.Update(clientApplication);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
