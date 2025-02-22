using OpenVPNGateMonitor.DataBase.UnitOfWork;
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
        throw new NotImplementedException();
        // var newApp = new RegisteredApp { Name = name };
        // _dbContext.RegisteredApps.Add(newApp);
        // await _dbContext.SaveChangesAsync();
        // return newApp;
    }

    public async Task<RegisteredApp?> GetApplicationByClientIdAsync(string clientId)
    {
        throw new NotImplementedException();
        // return await _dbContext.RegisteredApps.FirstOrDefaultAsync(a => a.ClientId == clientId);
    }

    public async Task<List<RegisteredApp>> GetAllApplicationsAsync()
    {
        throw new NotImplementedException();
        // return await _dbContext.RegisteredApps.ToListAsync();
    }

    public async Task<bool> RevokeApplicationAsync(string clientId)
    {
        throw new NotImplementedException();
        // var app = await _dbContext.RegisteredApps.FirstOrDefaultAsync(a => a.ClientId == clientId);
        // if (app == null) return false;
        //
        // _dbContext.RegisteredApps.Remove(app);
        // await _dbContext.SaveChangesAsync();
        // return true;
    }
}
