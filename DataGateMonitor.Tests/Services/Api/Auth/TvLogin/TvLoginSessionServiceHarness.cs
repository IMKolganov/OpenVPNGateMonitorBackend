using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Hubs;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.TvLogin;

namespace DataGateMonitor.Tests.Services.Api.Auth.TvLogin;

/// <summary>Shared in-memory harness for <see cref="TvLoginSessionService"/> unit tests.</summary>
internal sealed class TvLoginSessionServiceHarness
{
    public List<TvLoginSession> Sessions { get; } = [];
    public Mock<ITokenService> TokenService { get; } = new();
    public Mock<IUserQueryService> UserQuery { get; } = new();
    public Mock<ITvLoginHubNotifier> Hub { get; } = new();
    public Mock<IHttpContextAccessor> Http { get; } = new();
    public ConcurrentQueue<Action<TvLoginSession>> PendingUpdates { get; } = new();
    public int? ForceUpdateWhereCount { get; set; }

    public MemoryCache Cache { get; } = new(new MemoryCacheOptions());
    public IConfiguration Config { get; set; }

    public TvLoginSessionServiceHarness(IConfiguration? config = null)
    {
        Config = config ?? new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Test-only values — production must set Auth__PublicWebBaseUrl via env/appsettings.
                ["Auth:PublicWebBaseUrl"] = "https://tv-link.test",
                ["Auth:TvLoginSessionMinutes"] = "5",
            })
            .Build();

        Http.SetupGet(h => h.HttpContext).Returns((HttpContext?)null);

        UserQuery
            .Setup(u => u.GetById(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new User
            {
                Id = id,
                DisplayName = "User" + id,
                Email = $"user{id}@example.com",
            });
    }

    public void EnqueueUpdate(Action<TvLoginSession> apply) => PendingUpdates.Enqueue(apply);

    public TvLoginSession AddSession(
        Guid? id = null,
        string userCode = "123456",
        TvLoginSessionStatus status = TvLoginSessionStatus.Pending,
        DateTimeOffset? expiresAt = null,
        int? approvedUserId = null,
        string? deviceName = "Test TV",
        string? client = "android-tv",
        string? deviceId = null,
        string? userAgent = null)
    {
        var session = new TvLoginSession
        {
            Id = id ?? Guid.NewGuid(),
            UserCode = userCode,
            Status = status,
            DeviceName = deviceName,
            Client = client,
            DeviceId = deviceId,
            UserAgent = userAgent,
            ApprovedUserId = approvedUserId,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5),
            CreateDate = DateTimeOffset.UtcNow,
        };
        Sessions.Add(session);
        return session;
    }

    public TvLoginSessionService CreateSut()
    {
        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.GetById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => Sessions.FirstOrDefault(s => s.Id == id));
        query.Setup(q => q.GetActiveByUserCode(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) =>
                Sessions.FirstOrDefault(s =>
                    s.UserCode == code
                    && s.Status is TvLoginSessionStatus.Pending or TvLoginSessionStatus.Viewed
                    && s.ExpiresAt > DateTimeOffset.UtcNow));
        query.Setup(q => q.GetLatestByUserCode(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) =>
                Sessions.Where(s => s.UserCode == code).OrderByDescending(s => s.CreateDate).FirstOrDefault());
        query.Setup(q => q.AnyActiveByUserCode(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) =>
                Sessions.Any(s =>
                    s.UserCode == code
                    && s.Status is TvLoginSessionStatus.Pending or TvLoginSessionStatus.Viewed
                    && s.ExpiresAt > DateTimeOffset.UtcNow));

        var command = new Mock<ICommandService<TvLoginSession, Guid>>();
        command.Setup(c => c.Add(It.IsAny<TvLoginSession>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TvLoginSession e, bool _, CancellationToken _) =>
            {
                Sessions.Add(e);
                return e;
            });

        command.Setup(c => c.UpdateWhere(
                It.IsAny<Expression<Func<TvLoginSession, bool>>>(),
                It.IsAny<Action<UpdateSettersBuilder<TvLoginSession>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                Expression<Func<TvLoginSession, bool>> predicate,
                Action<UpdateSettersBuilder<TvLoginSession>> set,
                CancellationToken __) =>
            {
                _ = set;

                lock (Sessions)
                {
                    if (ForceUpdateWhereCount is int forced)
                    {
                        ForceUpdateWhereCount = null;
                        return forced;
                    }

                    var compiled = predicate.Compile();
                    var matches = Sessions.Where(compiled).ToList();
                    foreach (var s in matches)
                    {
                        if (PendingUpdates.TryDequeue(out var apply))
                        {
                            apply(s);
                            continue;
                        }

                        ApplyDefaultTransition(s);
                    }

                    return matches.Count;
                }
            });

        return new TvLoginSessionService(
            query.Object,
            command.Object,
            UserQuery.Object,
            TokenService.Object,
            Hub.Object,
            Cache,
            Config,
            Http.Object,
            NullLogger<TvLoginSessionService>.Instance);
    }

    /// <summary>
    /// Default in-memory stand-in when a test did not enqueue an explicit transition.
    /// Mirrors production SetProperty intent for the common paths.
    /// </summary>
    private static void ApplyDefaultTransition(TvLoginSession s)
    {
        if (s.Status is TvLoginSessionStatus.Pending or TvLoginSessionStatus.Viewed
            && s.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            s.Status = TvLoginSessionStatus.Expired;
            return;
        }

        if (s.Status == TvLoginSessionStatus.Approved)
        {
            s.Status = TvLoginSessionStatus.Consumed;
            return;
        }

        if (s.Status is TvLoginSessionStatus.Pending or TvLoginSessionStatus.Viewed
            && s.ApprovedUserId is not null)
        {
            s.Status = TvLoginSessionStatus.Approved;
            s.CompletedAt = DateTimeOffset.UtcNow;
            return;
        }

        if (s.Status == TvLoginSessionStatus.Pending)
        {
            s.Status = TvLoginSessionStatus.Viewed;
            return;
        }

        if (s.Status == TvLoginSessionStatus.Viewed)
        {
            s.Status = TvLoginSessionStatus.Denied;
            s.CompletedAt = DateTimeOffset.UtcNow;
        }
    }
}
