using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using DataGateMonitor.Services.Api.Auth.Registers.Interfaces;

namespace DataGateMonitor.Configurations;

/// <summary>
/// JWT bearer events where failure is expected client behaviour (stale tab, SignalR reconnect), not a server fault.
/// </summary>
public static class JwtBearerEventHandlers
{
    public const string AppClientRevokedFailure = "API client revoked.";

    public static bool IsExpectedClientTokenFailure(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SecurityTokenExpiredException or SecurityTokenNotYetValidException)
                return true;

            if (current.Message.Contains("IDX10223", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("IDX10225", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// After JWT signature/lifetime validation: App-role tokens must belong to a non-revoked client.
    /// This closes the window where a revoked client could use an already-issued JWT until expiry.
    /// </summary>
    public static async Task RejectRevokedAppClientAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null)
            return;

        if (!principal.IsInRole("App"))
            return;

        var clientId = principal.FindFirstValue(ClaimTypes.Name)
                       ?? principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            context.Fail(AppClientRevokedFailure);
            return;
        }

        var appService = context.HttpContext.RequestServices.GetService(typeof(IApplicationService))
            as IApplicationService;
        if (appService is null)
        {
            context.Fail(AppClientRevokedFailure);
            return;
        }

        var app = await appService.GetApplicationByClientIdAsync(
            clientId,
            context.HttpContext.RequestAborted);

        if (app is null || app.IsRevoked)
            context.Fail(AppClientRevokedFailure);
    }
}
