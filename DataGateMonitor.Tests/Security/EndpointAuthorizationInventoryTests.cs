using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using DataGateMonitor.Controllers;

namespace DataGateMonitor.Tests.Security;

/// <summary>
/// Ensures every controller action is either explicitly authorized or AllowAnonymous.
/// Documents intentional public surfaces so new anonymous endpoints cannot land unnoticed.
/// </summary>
public class EndpointAuthorizationInventoryTests
{
    /// <summary>
    /// Public-by-design endpoints (capability tokens / login / crash ingest / health).
    /// Any new AllowAnonymous action must be added here intentionally.
    /// </summary>
    private static readonly HashSet<string> IntentionalAnonymousActions = new(StringComparer.Ordinal)
    {
        "BaseController.Healthcheck",
        "AuthController.GetSessionPolicy",
        "AuthController.GenerateToken",
        "AuthController.GenerateDevToken",
        "AuthController.GetPublicKeyForMicroservice",
        "AuthController.Register",
        "AuthController.RequestEmailConfirmation",
        "AuthController.ConfirmEmail",
        "AuthController.Login",
        "AuthController.GoogleLogin",
        "AuthController.GoogleCodeLogin",
        "AuthController.TelegramCodeLogin",
        "AuthController.ForgotPassword",
        "AuthController.ResetPassword",
        "AuthController.Refresh",
        "AuthController.VerifyTotpLogin",
        "AuthController.CreateTvLoginSession",
        "AuthController.PollTvLoginSession",
        "MobileCrashIngestController.Ingest",
        "WindowsCrashIngestController.Ingest",
    };

    [Fact]
    public void EveryAction_HasAuthorizeOrAllowAnonymous()
    {
        var controllers = typeof(AuthController).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        var missing = new List<string>();

        foreach (var controller in controllers)
        {
            var classAllowAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) != null;
            var classAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();

            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any() &&
                    method.GetCustomAttribute<RouteAttribute>(inherit: true) is null)
                    continue;

                var allowAnonymous = classAllowAnonymous ||
                                     method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) != null;
                var authorize = classAuthorize ||
                                method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();

                if (!allowAnonymous && !authorize)
                    missing.Add($"{controller.Name}.{method.Name}");
            }
        }

        Assert.True(missing.Count == 0,
            "Actions without [Authorize] or [AllowAnonymous]: " + string.Join(", ", missing));
    }

    [Fact]
    public void AllowAnonymousActions_AreExplicitlyDocumented()
    {
        var controllers = typeof(AuthController).Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var controller in controllers)
        {
            var classAllowAnonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) != null;

            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any() &&
                    method.GetCustomAttribute<RouteAttribute>(inherit: true) is null)
                    continue;

                var allowAnonymous = classAllowAnonymous ||
                                     method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) != null;
                if (!allowAnonymous)
                    continue;

                // Method-level Authorize overrides class AllowAnonymous; class Authorize + method AllowAnonymous is public.
                var methodAuthorize = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
                if (methodAuthorize)
                    continue;

                found.Add($"{controller.Name}.{method.Name}");
            }
        }

        var undocumented = found.Except(IntentionalAnonymousActions).OrderBy(x => x).ToList();
        var stale = IntentionalAnonymousActions.Except(found).OrderBy(x => x).ToList();

        Assert.True(undocumented.Count == 0,
            "New anonymous endpoints must be reviewed and added to IntentionalAnonymousActions: " +
            string.Join(", ", undocumented));
        Assert.True(stale.Count == 0,
            "Stale IntentionalAnonymousActions entries (rename or remove): " + string.Join(", ", stale));
    }

    [Theory]
    [InlineData(typeof(PerformanceController), "Admin")]
    [InlineData(typeof(OpenVpnFilesController), "Admin,VpnUser,App")]
    [InlineData(typeof(ApplicationsController), "Admin,App")]
    public void ProtectedControllers_DeclareExpectedRoles(Type controllerType, string expectedRoles)
    {
        var attrs = controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
        Assert.NotEmpty(attrs);
        Assert.Contains(attrs, a => string.Equals(a.Roles, expectedRoles, StringComparison.Ordinal));
    }
}
