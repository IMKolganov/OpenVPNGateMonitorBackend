using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.TvLoginSessionTable;
using DataGateMonitor.Hubs;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth.Login;
using DataGateMonitor.Services.Api.Auth.TvLogin;
using DataGateMonitor.SharedModels.DataGateMonitor.Auth.Requests;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api.Auth.TvLogin;

public class TvLoginSessionServiceTests
{
    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSessionAsync_ReturnsSixDigitCode_QrPayload_AndHubPath()
    {
        var h = new TvLoginSessionServiceHarness();
        var sut = h.CreateSut();

        var result = await sut.CreateSessionAsync(
            new CreateTvLoginSessionRequest { DeviceName = "Living Room TV", Client = "android-tv" },
            "127.0.0.1",
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.Matches(@"^\d{6}$", result.UserCode);
        Assert.Equal("https://tv-link.test/tv/link", result.VerificationUrl);
        Assert.Equal(
            $"https://tv-link.test/tv/link?code={Uri.EscapeDataString(result.UserCode)}",
            result.QrPayload);
        Assert.Equal(2, result.PollIntervalSeconds);
        Assert.Equal(TvLoginHub.HubPath, result.SignalRHubPath);
        Assert.Single(h.Sessions);
        Assert.Equal(TvLoginSessionStatus.Pending, h.Sessions[0].Status);
        Assert.Equal("Living Room TV", h.Sessions[0].DeviceName);
        Assert.Equal("android-tv", h.Sessions[0].Client);
    }

    [Fact]
    public async Task CreateSessionAsync_UsesConfiguredTtl_And_TrimsTrailingSlashOnBaseUrl()
    {
        var h = new TvLoginSessionServiceHarness(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PublicWebBaseUrl"] = "https://tv-link.test/",
                ["Auth:TvLoginSessionMinutes"] = "3",
            })
            .Build());
        var sut = h.CreateSut();
        var before = DateTimeOffset.UtcNow;

        var result = await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "1.2.3.4", CancellationToken.None);

        Assert.Equal("https://tv-link.test/tv/link", result.VerificationUrl);
        Assert.InRange(result.ExpiresAt, before.AddMinutes(2.5), before.AddMinutes(3.5));
    }

    [Fact]
    public async Task CreateSessionAsync_WhenTtlConfigInvalid_FallsBackToFiveMinutes()
    {
        var h = new TvLoginSessionServiceHarness(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PublicWebBaseUrl"] = "https://tv-link.test",
                ["Auth:TvLoginSessionMinutes"] = "0",
            })
            .Build());
        var sut = h.CreateSut();
        var before = DateTimeOffset.UtcNow;

        var result = await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), null, CancellationToken.None);

        Assert.InRange(result.ExpiresAt, before.AddMinutes(4.5), before.AddMinutes(5.5));
    }

    [Fact]
    public async Task CreateSessionAsync_CapturesDeviceIdAndUserAgentFromHeaders()
    {
        var h = new TvLoginSessionServiceHarness();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "AndroidTV/1.0";
        httpContext.Request.Headers["X-Device-Id"] = "tv-install-1";
        h.Http.SetupGet(x => x.HttpContext).Returns(httpContext);
        var sut = h.CreateSut();

        await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "10.0.0.1", CancellationToken.None);

        Assert.Equal("tv-install-1", h.Sessions[0].DeviceId);
        Assert.Equal("AndroidTV/1.0", h.Sessions[0].UserAgent);
    }

    [Fact]
    public async Task CreateSessionAsync_TruncatesLongDeviceNameAndClient()
    {
        var h = new TvLoginSessionServiceHarness();
        var sut = h.CreateSut();
        var longName = new string('N', 200);
        var longClient = new string('C', 100);

        await sut.CreateSessionAsync(
            new CreateTvLoginSessionRequest { DeviceName = longName, Client = longClient },
            "1.1.1.1",
            CancellationToken.None);

        Assert.Equal(128, h.Sessions[0].DeviceName!.Length);
        Assert.Equal(64, h.Sessions[0].Client!.Length);
    }

    [Fact]
    public async Task CreateSessionAsync_WhenRateLimited_Throws()
    {
        var h = new TvLoginSessionServiceHarness();
        var sut = h.CreateSut();
        const string ip = "203.0.113.10";

        for (var i = 0; i < 10; i++)
            await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), ip, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), ip, CancellationToken.None));
        Assert.Equal(TvLoginSessionService.RateLimitMessage, ex.Message);
    }

    // ── Poll statuses ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(TvLoginSessionStatus.Pending, "pending")]
    [InlineData(TvLoginSessionStatus.Viewed, "viewed")]
    [InlineData(TvLoginSessionStatus.Denied, "denied")]
    [InlineData(TvLoginSessionStatus.Expired, "expired")]
    [InlineData(TvLoginSessionStatus.Consumed, "consumed")]
    public async Task PollSessionAsync_ReturnsStatusWithoutTokens(TvLoginSessionStatus status, string expected)
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: status);
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);

        Assert.Equal(expected, poll.Status);
        Assert.Null(poll.Token);
        Assert.Null(poll.RefreshToken);
        Assert.False(poll.RequiresTotp);
        Assert.Null(poll.LoginChallengeId);
        Assert.False(poll.RequiresTotpSetup);
        h.TokenService.Verify(
            t => t.IssueAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PollSessionAsync_WhenMissing_ThrowsNotFound()
    {
        var sut = new TvLoginSessionServiceHarness().CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.PollSessionAsync(Guid.NewGuid(), "1.1.1.1", CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotFoundMessage, ex.Message);
    }

    [Fact]
    public async Task PollSessionAsync_WhenPendingExpired_MarksExpired_AndNotifiesHub()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(
            status: TvLoginSessionStatus.Pending,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "3.3.3.3", CancellationToken.None);

        Assert.Equal("expired", poll.Status);
        Assert.Equal(TvLoginSessionStatus.Expired, session.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "expired", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PollSessionAsync_WhenViewedExpired_MarksExpired_AndNotifiesHub()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(
            status: TvLoginSessionStatus.Viewed,
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(-5));
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "3.3.3.4", CancellationToken.None);

        Assert.Equal("expired", poll.Status);
        Assert.Equal(TvLoginSessionStatus.Expired, session.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "expired", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Phone preview / viewed ──────────────────────────────────────────────

    [Fact]
    public async Task GetByUserCodeAsync_MarksViewed_AndNotifiesHub()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(userCode: "123456", deviceName: "Kitchen TV");
        var sut = h.CreateSut();

        var preview = await sut.GetByUserCodeAsync("123 456", 1, "1.1.1.1", CancellationToken.None);

        Assert.Equal(session.Id, preview.SessionId);
        Assert.Equal("123456", preview.UserCode);
        Assert.Equal("Kitchen TV", preview.DeviceName);
        Assert.Equal("pending", preview.Status); // phone UI still "pending"
        Assert.Equal(TvLoginSessionStatus.Viewed, session.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "viewed", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var poll = await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);
        Assert.Equal("viewed", poll.Status);
    }

    [Fact]
    public async Task GetByUserCodeAsync_WhenAlreadyViewed_DoesNotNotifyAgain()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(userCode: "222333", status: TvLoginSessionStatus.Viewed);
        var sut = h.CreateSut();

        var preview = await sut.GetByUserCodeAsync("222333", 1, "1.1.1.1", CancellationToken.None);

        Assert.Equal(session.Id, preview.SessionId);
        Assert.Equal("pending", preview.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(It.IsAny<Guid>(), "viewed", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public async Task GetByUserCodeAsync_WhenCodeInvalidLength_ThrowsNotFound(string code)
    {
        var sut = new TvLoginSessionServiceHarness().CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetByUserCodeAsync(code, 1, "1.1.1.1", CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotFoundMessage, ex.Message);
    }

    [Fact]
    public async Task GetByUserCodeAsync_WhenMissingEntirely_ThrowsNotFound()
    {
        var sut = new TvLoginSessionServiceHarness().CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetByUserCodeAsync("999888", 1, "1.1.1.1", CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotFoundMessage, ex.Message);
    }

    [Fact]
    public async Task GetByUserCodeAsync_WhenOnlyExpiredExists_ThrowsExpired()
    {
        var h = new TvLoginSessionServiceHarness();
        h.AddSession(userCode: "555666", status: TvLoginSessionStatus.Expired);
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetByUserCodeAsync("555666", 1, "1.1.1.1", CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionExpiredMessage, ex.Message);
    }

    // ── Approve / deny ──────────────────────────────────────────────────────

    [Fact]
    public async Task ApproveThenPoll_DeliversTokensOnce_ThenConsumed()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(userCode: "654321");
        h.UserQuery.Setup(u => u.GetById(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
            {
                session.ApprovedUserId = id;
                return new User { Id = 42, DisplayName = "Alice", Email = "a@example.com" };
            });
        h.TokenService
            .Setup(t => t.IssueAsync(42, null, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenPair(
                "access-token",
                DateTimeOffset.UtcNow.AddMinutes(15),
                "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
        var sut = h.CreateSut();

        var approve = await sut.ApproveAsync(
            new ApproveTvLoginSessionRequest { SessionId = session.Id },
            42,
            "9.9.9.9",
            CancellationToken.None);

        Assert.Equal("approved", approve.Status);
        Assert.Equal(TvLoginSessionStatus.Approved, session.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "approved", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var first = await sut.PollSessionAsync(session.Id, "2.2.2.2", CancellationToken.None);
        Assert.Equal("approved", first.Status);
        Assert.Equal("access-token", first.Token);
        Assert.Equal("refresh-token", first.RefreshToken);
        Assert.Equal(42, first.UserId);
        Assert.Equal("Alice", first.DisplayName);
        Assert.Equal("a@example.com", first.Email);
        Assert.False(first.RequiresTotp);
        Assert.Null(first.LoginChallengeId);
        Assert.False(first.RequiresTotpSetup);

        var second = await sut.PollSessionAsync(session.Id, "2.2.2.2", CancellationToken.None);
        Assert.Equal("consumed", second.Status);
        Assert.Null(second.Token);
        h.TokenService.Verify(
            t => t.IssueAsync(42, null, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "consumed", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_ByUserCode_WorksAfterViewed()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(userCode: "777888", status: TvLoginSessionStatus.Viewed);
        h.UserQuery.Setup(u => u.GetById(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
            {
                session.ApprovedUserId = id;
                return new User { Id = 7, DisplayName = "Bob" };
            });
        var sut = h.CreateSut();

        var result = await sut.ApproveAsync(
            new ApproveTvLoginSessionRequest { UserCode = "777-888" },
            7,
            "8.8.8.8",
            CancellationToken.None);

        Assert.Equal("approved", result.Status);
        Assert.Equal(TvLoginSessionStatus.Approved, session.Status);
    }

    [Fact]
    public async Task ApproveAsync_WhenUserBlocked_ThrowsUnauthorized()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession();
        h.UserQuery.Setup(u => u.GetById(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, DisplayName = "X", IsBlocked = true });
        var sut = h.CreateSut();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.ApproveAsync(
                new ApproveTvLoginSessionRequest { SessionId = session.Id },
                5,
                "1.1.1.1",
                CancellationToken.None));
    }

    [Fact]
    public async Task ApproveAsync_WhenUserMissing_ThrowsUnauthorized()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession();
        h.UserQuery.Setup(u => u.GetById(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var sut = h.CreateSut();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sut.ApproveAsync(
                new ApproveTvLoginSessionRequest { SessionId = session.Id },
                5,
                "1.1.1.1",
                CancellationToken.None));
    }

    [Fact]
    public async Task ApproveAsync_WhenAlreadyApproved_ThrowsNotPending()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Approved, approvedUserId: 1);
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApproveAsync(
                new ApproveTvLoginSessionRequest { SessionId = session.Id },
                1,
                "1.1.1.1",
                CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotPendingMessage, ex.Message);
    }

    [Fact]
    public async Task DenyAsync_FromViewed_SetsDenied_AndNotifiesHub()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Viewed);
        // Explicit deny transition (default heuristic also maps Viewed→Denied when ApprovedUserId is null).
        h.EnqueueUpdate(s =>
        {
            s.Status = TvLoginSessionStatus.Denied;
            s.CompletedAt = DateTimeOffset.UtcNow;
        });
        var sut = h.CreateSut();

        var result = await sut.DenyAsync(
            new DenyTvLoginSessionRequest { SessionId = session.Id },
            9,
            "4.4.4.4",
            CancellationToken.None);

        Assert.Equal("denied", result.Status);
        Assert.Equal(TvLoginSessionStatus.Denied, session.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "denied", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var poll = await sut.PollSessionAsync(session.Id, "4.4.4.4", CancellationToken.None);
        Assert.Equal("denied", poll.Status);
    }

    [Fact]
    public async Task DenyAsync_FromPending_SetsDenied()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Pending);
        h.EnqueueUpdate(s =>
        {
            s.Status = TvLoginSessionStatus.Denied;
            s.CompletedAt = DateTimeOffset.UtcNow;
        });
        var sut = h.CreateSut();

        var result = await sut.DenyAsync(
            new DenyTvLoginSessionRequest { UserCode = session.UserCode },
            3,
            "5.5.5.5",
            CancellationToken.None);

        Assert.Equal("denied", result.Status);
        Assert.Equal(TvLoginSessionStatus.Denied, session.Status);
    }

    [Fact]
    public async Task DenyAsync_WhenSessionMissing_ThrowsNotFound()
    {
        var sut = new TvLoginSessionServiceHarness().CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DenyAsync(
                new DenyTvLoginSessionRequest { SessionId = Guid.NewGuid() },
                1,
                "1.1.1.1",
                CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotFoundMessage, ex.Message);
    }

    // ── Token delivery edge cases ───────────────────────────────────────────

    [Fact]
    public async Task PollApproved_WhenApprovedUserIdMissing_ReturnsExpiredWithoutTokens()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Approved, approvedUserId: null);
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);

        Assert.Equal("expired", poll.Status);
        Assert.Null(poll.Token);
        h.TokenService.Verify(
            t => t.IssueAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PollApproved_WhenUserBlocked_MarksConsumed_ReturnsExpired()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Approved, approvedUserId: 11);
        h.UserQuery.Setup(u => u.GetById(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 11, DisplayName = "Blocked", IsBlocked = true });
        h.EnqueueUpdate(s => s.Status = TvLoginSessionStatus.Consumed);
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);

        Assert.Equal("expired", poll.Status);
        Assert.Null(poll.Token);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "consumed", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PollApproved_WhenClaimLosesRace_ReturnsConsumedWithoutIssuing()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Approved, approvedUserId: 12);
        h.UserQuery.Setup(u => u.GetById(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 12, DisplayName = "Ok" });
        // Simulate lost race: UpdateWhere matches 0 rows.
        h.EnqueueUpdate(_ => { /* no-op; we'll force count via empty match by changing status first */ });
        // Better: change status before UpdateWhere by enqueue that sets Consumed then claim sees Approved→ already consumed
        // Actually UpdateWhere predicate requires Approved; if we set Consumed in enqueue, predicate won't match on second call.
        // Force claimed=0 by making the session no longer Approved before apply: change to Consumed in enqueue
        // but predicate runs first on Approved session...
        // Clear and use custom: set status to Consumed in enqueue (apply after match) — count still 1.
        // To get claimed != 1, enqueue nothing and make UpdateWhere return 0 by having session not match —
        // change session to Consumed before poll so DeliverTokensOnce isn't called... that won't hit the race path.

        // Re-setup: Approved session, but UpdateWhere returns 0 because we dequeue an apply that doesn't change
        // and we temporarily change status so second evaluation... The mock returns matches.Count after apply.
        // Trick: enqueue apply that sets Consumed, then... still count 1.

        // Use a second harness path: make UpdateWhere return 0 by clearing Approved before Apply —
        // Change mock via Enqueue that is empty and mutate Sessions in UserQuery callback:
        h.UserQuery.Setup(u => u.GetById(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, CancellationToken __) =>
            {
                session.Status = TvLoginSessionStatus.Consumed; // race: another poll consumed first
                return new User { Id = 12, DisplayName = "Ok" };
            });
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);

        // After user load, status is Consumed so UpdateWhere matches 0 → "consumed"
        Assert.Equal("consumed", poll.Status);
        Assert.Null(poll.Token);
        h.TokenService.Verify(
            t => t.IssueAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PollApproved_UsesSessionDeviceMetadataForTokenIssue()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(
            status: TvLoginSessionStatus.Approved,
            approvedUserId: 15,
            deviceId: "dev-99",
            userAgent: "ua-99");
        h.UserQuery.Setup(u => u.GetById(15, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 15, DisplayName = "Dev" });
        h.TokenService
            .Setup(t => t.IssueAsync(15, null, "dev-99", "ua-99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenPair("a", DateTimeOffset.UtcNow.AddMinutes(1), "r", DateTimeOffset.UtcNow.AddDays(1)));
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);

        Assert.Equal("approved", poll.Status);
        Assert.Equal("a", poll.Token);
        h.TokenService.Verify(
            t => t.IssueAsync(15, null, "dev-99", "ua-99", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Code helpers ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("482 913", "482913")]
    [InlineData("482-913", "482913")]
    [InlineData("48a2b913", "482913")]
    [InlineData("012345", "012345")]
    public void NormalizeUserCode_KeepsDigitsOnly(string? input, string expected)
    {
        Assert.Equal(expected, TvLoginSessionService.NormalizeUserCode(input));
    }

    [Fact]
    public void FormatUserCode_ReturnsNormalizedAsIs()
    {
        Assert.Equal("012345", TvLoginSessionService.FormatUserCode("012345"));
    }

    // ── Full lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSessionAsync_FallsBackToFrontendBaseUrl()
    {
        var h = new TvLoginSessionServiceHarness(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://app.example.com/",
            })
            .Build());
        var sut = h.CreateSut();

        var result = await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "1.1.1.1", CancellationToken.None);

        Assert.Equal("https://app.example.com/tv/link", result.VerificationUrl);
    }

    [Fact]
    public async Task CreateSessionAsync_WhenPublicWebBaseUrlMissing_Throws()
    {
        var h = new TvLoginSessionServiceHarness(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:TvLoginSessionMinutes"] = "5",
            })
            .Build());
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "1.1.1.1", CancellationToken.None));

        Assert.Contains("Auth:PublicWebBaseUrl is not configured", ex.Message);
    }

    [Fact]
    public async Task CreateSessionAsync_BlankDeviceHeaders_StoredAsNull()
    {
        var h = new TvLoginSessionServiceHarness();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "   ";
        httpContext.Request.Headers["X-Device-Id"] = "  ";
        h.Http.SetupGet(x => x.HttpContext).Returns(httpContext);
        var sut = h.CreateSut();

        await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "1.1.1.1", CancellationToken.None);

        Assert.Null(h.Sessions[0].DeviceId);
        Assert.Null(h.Sessions[0].UserAgent);
    }

    [Fact]
    public async Task CreateSessionAsync_WhenAllCodesCollide_Throws()
    {
        var h = new TvLoginSessionServiceHarness();
        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.AnyActiveByUserCode(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var colliding = new TvLoginSessionService(
            query.Object,
            Mock.Of<ICommandService<TvLoginSession, Guid>>(),
            h.UserQuery.Object,
            h.TokenService.Object,
            h.Hub.Object,
            h.Cache,
            h.Config,
            h.Http.Object,
            NullLogger<TvLoginSessionService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => colliding.CreateSessionAsync(new CreateTvLoginSessionRequest(), "9.9.9.9", CancellationToken.None));
        Assert.Contains("unique TV login code", ex.Message);
    }

    [Fact]
    public async Task ApproveAsync_WhenUpdateAffectsZeroRows_ThrowsNotPending()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession();
        h.ForceUpdateWhereCount = 0;
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApproveAsync(
                new ApproveTvLoginSessionRequest { SessionId = session.Id },
                1,
                "1.1.1.1",
                CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotPendingMessage, ex.Message);
    }

    [Fact]
    public async Task DenyAsync_WhenUpdateAffectsZeroRows_ThrowsNotPending()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Viewed);
        h.ForceUpdateWhereCount = 0;
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DenyAsync(
                new DenyTvLoginSessionRequest { SessionId = session.Id },
                1,
                "1.1.1.1",
                CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotPendingMessage, ex.Message);
    }

    [Fact]
    public async Task PollSessionAsync_UnknownStatus_ReturnsExpired()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: (TvLoginSessionStatus)999);
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);

        Assert.Equal("expired", poll.Status);
    }

    [Fact]
    public async Task GetByUserCodeAsync_WhenStaleBecomesNonOpen_ThrowsExpired()
    {
        var h = new TvLoginSessionServiceHarness();
        // Not returned by GetActive (expired), but still found via GetLatest → SessionExpiredMessage.
        var session = h.AddSession(
            userCode: "444555",
            status: TvLoginSessionStatus.Pending,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetByUserCodeAsync("444555", 1, "1.1.1.1", CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionExpiredMessage, ex.Message);
        Assert.Equal(TvLoginSessionStatus.Pending, session.Status);
    }

    [Fact]
    public async Task GetByUserCodeAsync_WhenActiveThenExpiresDuringEnsure_ThrowsExpired()
    {
        var h = new TvLoginSessionServiceHarness();
        // Force GetActive to return a stale pending row (bypass ExpiresAt filter in mock).
        var sessionId = Guid.NewGuid();
        var session = new TvLoginSession
        {
            Id = sessionId,
            UserCode = "666777",
            Status = TvLoginSessionStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        h.Sessions.Add(session);

        var query = new Mock<ITvLoginSessionQueryService>();
        query.Setup(q => q.GetActiveByUserCode("666777", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        query.Setup(q => q.GetById(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var command = new Mock<ICommandService<TvLoginSession, Guid>>();
        command.Setup(c => c.UpdateWhere(
                It.IsAny<System.Linq.Expressions.Expression<Func<TvLoginSession, bool>>>(),
                It.IsAny<Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TvLoginSession>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                System.Linq.Expressions.Expression<Func<TvLoginSession, bool>> pred,
                Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<TvLoginSession>> _,
                CancellationToken __) =>
            {
                var matches = h.Sessions.Where(pred.Compile()).ToList();
                foreach (var s in matches)
                    s.Status = TvLoginSessionStatus.Expired;
                return matches.Count;
            });

        var sut = new TvLoginSessionService(
            query.Object,
            command.Object,
            h.UserQuery.Object,
            h.TokenService.Object,
            h.Hub.Object,
            h.Cache,
            h.Config,
            h.Http.Object,
            NullLogger<TvLoginSessionService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetByUserCodeAsync("666777", 1, "1.1.1.1", CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionExpiredMessage, ex.Message);
        Assert.Equal(TvLoginSessionStatus.Expired, session.Status);
    }

    [Fact]
    public async Task PollApproved_WhenUserMissing_MarksConsumed_ReturnsExpired()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Approved, approvedUserId: 88);
        h.UserQuery.Setup(u => u.GetById(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        h.EnqueueUpdate(s => s.Status = TvLoginSessionStatus.Consumed);
        var sut = h.CreateSut();

        var poll = await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);

        Assert.Equal("expired", poll.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "consumed", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(TvLoginSessionStatus.Denied, TvLoginSessionService.SessionDeniedMessage)]
    [InlineData(TvLoginSessionStatus.Approved, TvLoginSessionService.SessionAlreadyCompletedMessage)]
    [InlineData(TvLoginSessionStatus.Consumed, TvLoginSessionService.SessionAlreadyCompletedMessage)]
    public async Task GetByUserCodeAsync_WhenLatestClosed_ThrowsSpecificMessage(
        TvLoginSessionStatus status,
        string expectedMessage)
    {
        var h = new TvLoginSessionServiceHarness();
        h.AddSession(userCode: "777888", status: status, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetByUserCodeAsync("777888", 1, "1.1.1.1", CancellationToken.None));

        Assert.Equal(expectedMessage, ex.Message);
    }

    [Fact]
    public async Task GetByUserCodeAsync_WhenViewedRaceLosesUpdate_StillReturnsPreview_WithoutNotify()
    {
        var h = new TvLoginSessionServiceHarness();
        h.AddSession(userCode: "121212", status: TvLoginSessionStatus.Pending);
        h.ForceUpdateWhereCount = 0;
        var sut = h.CreateSut();

        var preview = await sut.GetByUserCodeAsync("121212", 9, "9.9.9.9", CancellationToken.None);

        Assert.Equal("pending", preview.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(It.IsAny<Guid>(), "viewed", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByUserCodeAsync_WhenPreviewRateLimited_Throws()
    {
        var h = new TvLoginSessionServiceHarness();
        h.AddSession(userCode: "343434");
        var sut = h.CreateSut();

        for (var i = 0; i < 60; i++)
            await sut.GetByUserCodeAsync("343434", 3, "3.3.3.3", CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetByUserCodeAsync("343434", 3, "3.3.3.3", CancellationToken.None));
        Assert.Equal(TvLoginSessionService.RateLimitMessage, ex.Message);
    }

    [Fact]
    public async Task PollSessionAsync_WhenRateLimited_Throws()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession();
        var sut = h.CreateSut();

        for (var i = 0; i < 200; i++)
            await sut.PollSessionAsync(session.Id, "8.8.8.8", CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.PollSessionAsync(session.Id, "8.8.8.8", CancellationToken.None));
        Assert.Equal(TvLoginSessionService.RateLimitMessage, ex.Message);
    }

    [Fact]
    public async Task PollSessionAsync_RateLimit_IsIsolatedPerIp()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession();
        var sut = h.CreateSut();

        for (var i = 0; i < 200; i++)
            await sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.PollSessionAsync(session.Id, "1.1.1.1", CancellationToken.None));

        var other = await sut.PollSessionAsync(session.Id, "2.2.2.2", CancellationToken.None);
        Assert.Equal("pending", other.Status);
    }

    [Fact]
    public async Task CreateSessionAsync_NullIp_SharesUnknownRateLimitBucket()
    {
        var h = new TvLoginSessionServiceHarness();
        var sut = h.CreateSut();

        for (var i = 0; i < 10; i++)
            await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), null, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "  ", CancellationToken.None));
        Assert.Equal(TvLoginSessionService.RateLimitMessage, ex.Message);
    }

    [Fact]
    public async Task ApproveAsync_WhenRateLimited_Throws()
    {
        var h = new TvLoginSessionServiceHarness();
        var sut = h.CreateSut();

        for (var i = 0; i < 30; i++)
        {
            var s = h.AddSession(userCode: $"{i:D6}");
            await sut.ApproveAsync(
                new ApproveTvLoginSessionRequest { SessionId = s.Id },
                5,
                "5.5.5.5",
                CancellationToken.None);
        }

        var last = h.AddSession(userCode: "999001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApproveAsync(
                new ApproveTvLoginSessionRequest { SessionId = last.Id },
                5,
                "5.5.5.5",
                CancellationToken.None));
        Assert.Equal(TvLoginSessionService.RateLimitMessage, ex.Message);
    }

    [Fact]
    public async Task DenyAsync_WhenRateLimited_Throws()
    {
        var h = new TvLoginSessionServiceHarness();
        var sut = h.CreateSut();

        for (var i = 0; i < 30; i++)
        {
            var s = h.AddSession(userCode: $"{200000 + i:D6}");
            await sut.DenyAsync(
                new DenyTvLoginSessionRequest { SessionId = s.Id },
                11,
                "11.11.11.11",
                CancellationToken.None);
        }

        var next = h.AddSession(userCode: "299999");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DenyAsync(
                new DenyTvLoginSessionRequest { SessionId = next.Id },
                11,
                "11.11.11.11",
                CancellationToken.None));
        Assert.Equal(TvLoginSessionService.RateLimitMessage, ex.Message);
    }

    [Fact]
    public async Task ApproveAsync_WhenOpenButExpired_MarksExpired_ThenThrowsNotPending()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(
            status: TvLoginSessionStatus.Viewed,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApproveAsync(
                new ApproveTvLoginSessionRequest { SessionId = session.Id },
                1,
                "1.1.1.1",
                CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotPendingMessage, ex.Message);
        Assert.Equal(TvLoginSessionStatus.Expired, session.Status);
        h.Hub.Verify(
            x => x.NotifyStatusAsync(session.Id, "expired", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DenyAsync_WhenOpenButExpired_MarksExpired_ThenThrowsNotPending()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(
            status: TvLoginSessionStatus.Pending,
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(-5));
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DenyAsync(
                new DenyTvLoginSessionRequest { SessionId = session.Id },
                1,
                "1.1.1.1",
                CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotPendingMessage, ex.Message);
        Assert.Equal(TvLoginSessionStatus.Expired, session.Status);
    }

    [Theory]
    [InlineData(TvLoginSessionStatus.Denied)]
    [InlineData(TvLoginSessionStatus.Approved)]
    [InlineData(TvLoginSessionStatus.Consumed)]
    public async Task DenyAsync_WhenAlreadyClosed_ThrowsNotPending(TvLoginSessionStatus status)
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: status, approvedUserId: status == TvLoginSessionStatus.Pending ? null : 1);
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DenyAsync(
                new DenyTvLoginSessionRequest { SessionId = session.Id },
                1,
                "1.1.1.1",
                CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionNotPendingMessage, ex.Message);
    }

    [Fact]
    public async Task ApproveAsync_WhenSessionIdAndUserCodeMismatch_Throws()
    {
        var h = new TvLoginSessionServiceHarness();
        var a = h.AddSession(userCode: "111111");
        h.AddSession(userCode: "222222");
        var sut = h.CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApproveAsync(
                new ApproveTvLoginSessionRequest { SessionId = a.Id, UserCode = "222222" },
                1,
                "1.1.1.1",
                CancellationToken.None));

        Assert.Equal(TvLoginSessionService.SessionCodeMismatchMessage, ex.Message);
    }

    [Fact]
    public async Task ApproveAsync_WhenSessionIdEmpty_ResolvesByUserCode()
    {
        var h = new TvLoginSessionServiceHarness();
        h.AddSession(userCode: "454545", status: TvLoginSessionStatus.Viewed);
        var sut = h.CreateSut();

        var result = await sut.ApproveAsync(
            new ApproveTvLoginSessionRequest { SessionId = Guid.Empty, UserCode = "454545" },
            2,
            "1.1.1.1",
            CancellationToken.None);

        Assert.Equal("approved", result.Status);
    }

    [Fact]
    public async Task PollApproved_UserAgentFallback_UsesClientThenAndroidTv()
    {
        var h = new TvLoginSessionServiceHarness();
        var withClient = h.AddSession(
            status: TvLoginSessionStatus.Approved,
            approvedUserId: 1,
            client: "fire-tv",
            userAgent: null,
            deviceId: "d1");
        h.TokenService
            .Setup(t => t.IssueAsync(1, null, "d1", "fire-tv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenPair("a", DateTimeOffset.UtcNow.AddMinutes(1), "r", DateTimeOffset.UtcNow.AddDays(1)));
        var sut = h.CreateSut();

        await sut.PollSessionAsync(withClient.Id, "1.1.1.1", CancellationToken.None);
        h.TokenService.Verify(t => t.IssueAsync(1, null, "d1", "fire-tv", It.IsAny<CancellationToken>()), Times.Once);

        var h2 = new TvLoginSessionServiceHarness();
        var bare = h2.AddSession(
            status: TvLoginSessionStatus.Approved,
            approvedUserId: 2,
            client: null,
            userAgent: null,
            deviceId: null);
        h2.TokenService
            .Setup(t => t.IssueAsync(2, null, null, "android-tv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenPair("a2", DateTimeOffset.UtcNow.AddMinutes(1), "r2", DateTimeOffset.UtcNow.AddDays(1)));
        var sut2 = h2.CreateSut();

        await sut2.PollSessionAsync(bare.Id, "1.1.1.1", CancellationToken.None);
        h2.TokenService.Verify(t => t.IssueAsync(2, null, null, "android-tv", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollApproved_ConcurrentPolls_IssueTokensOnlyOnce()
    {
        var h = new TvLoginSessionServiceHarness();
        var session = h.AddSession(status: TvLoginSessionStatus.Approved, approvedUserId: 7);
        h.TokenService
            .Setup(t => t.IssueAsync(7, null, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenPair("tok", DateTimeOffset.UtcNow.AddMinutes(1), "ref", DateTimeOffset.UtcNow.AddDays(1)));
        var sut = h.CreateSut();

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => sut.PollSessionAsync(session.Id, "7.7.7.7", CancellationToken.None))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.Token == "tok"));
        Assert.Equal(7, results.Count(r => r.Status == "consumed" && r.Token is null));
        h.TokenService.Verify(
            t => t.IssueAsync(7, null, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(TvLoginSessionStatus.Consumed, session.Status);
    }

    [Fact]
    public async Task CreateSessionAsync_TruncatesDeviceIdAndUserAgent()
    {
        var h = new TvLoginSessionServiceHarness();
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Device-Id"] = new string('d', 200);
        http.Request.Headers.UserAgent = new string('u', 600);
        h.Http.SetupGet(x => x.HttpContext).Returns(http);
        var sut = h.CreateSut();

        var created = await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "1.1.1.1", CancellationToken.None);
        var row = h.Sessions.Single(s => s.Id == created.SessionId);

        Assert.Equal(128, row.DeviceId!.Length);
        Assert.Equal(512, row.UserAgent!.Length);
    }

    [Fact]
    public async Task CreateSessionAsync_NegativeTtl_FallsBackToFiveMinutes()
    {
        var h = new TvLoginSessionServiceHarness(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PublicWebBaseUrl"] = "https://tv-link.test",
                ["Auth:TvLoginSessionMinutes"] = "-3",
            })
            .Build());
        var sut = h.CreateSut();
        var before = DateTimeOffset.UtcNow;

        var result = await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "1.1.1.1", CancellationToken.None);

        Assert.InRange(result.ExpiresAt, before.AddMinutes(4.5), before.AddMinutes(5.5));
    }

    [Fact]
    public async Task CreateSessionAsync_PrefersPublicWebBaseUrlOverFrontend()
    {
        var h = new TvLoginSessionServiceHarness(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PublicWebBaseUrl"] = "https://public.example",
                ["Frontend:BaseUrl"] = "https://frontend.example",
            })
            .Build());
        var sut = h.CreateSut();

        var result = await sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "1.1.1.1", CancellationToken.None);

        Assert.Equal("https://public.example/tv/link", result.VerificationUrl);
    }

    [Fact]
    public async Task CreateSessionAsync_WhenPublicWebMissing_DoesNotInsertSession()
    {
        var h = new TvLoginSessionServiceHarness(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:TvLoginSessionMinutes"] = "5",
            })
            .Build());
        var sut = h.CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CreateSessionAsync(new CreateTvLoginSessionRequest(), "1.1.1.1", CancellationToken.None));

        Assert.Empty(h.Sessions);
    }
}