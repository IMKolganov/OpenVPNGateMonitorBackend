using System.Security.Claims;
using DataGateMonitor.Services.Api.Auth.Login;

namespace DataGateMonitor.Middlewares;

/// <summary>
/// Extends the admin idle window on any authenticated Admin request.
/// Source of truth for idle is server-side; the SPA only mirrors this for UX warnings.
/// </summary>
public sealed class AdminIdleActivityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAdminIdleSessionTracker idleSessionTracker)
    {
        MaybeTouch(context, idleSessionTracker);
        await next(context);
    }

    private static void MaybeTouch(HttpContext context, IAdminIdleSessionTracker idleSessionTracker)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
            return;

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!AdminIdleSessionTracker.IsAdminRole(role))
            return;

        var idRaw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idRaw, out var userId) || userId <= 0)
            return;

        idleSessionTracker.Touch(userId);
    }
}
