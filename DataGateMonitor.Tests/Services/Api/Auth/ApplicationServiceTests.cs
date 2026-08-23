using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.ClientApplicationTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.Api.Auth;
using Xunit;

namespace DataGateMonitor.Tests.Services.Api.Auth;

public class ApplicationServiceTests
{
    [Fact]
    public async Task RegisterApplicationAsync_When_NameNotExists_CreatesHashedSecretAndReturnsPlaintext()
    {
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByName("MyApp", It.IsAny<CancellationToken>())).ReturnsAsync((ClientApplication?)null);
        ClientApplication? captured = null;
        var command = new Mock<ICommandService<ClientApplication, int>>();
        command
            .Setup(c => c.Add(It.IsAny<ClientApplication>(), true, It.IsAny<CancellationToken>()))
            .Callback<ClientApplication, bool, CancellationToken>((a, _, _) => captured = a)
            .ReturnsAsync((ClientApplication a, bool _, CancellationToken _) => { a.Id = 7; return a; });

        var sut = new ApplicationService(query.Object, command.Object);

        var result = await sut.RegisterApplicationAsync("MyApp", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(7, result.Application.Id);
        Assert.Equal("MyApp", result.Application.Name);
        Assert.False(string.IsNullOrWhiteSpace(result.PlaintextClientSecret));
        Assert.NotNull(captured);
        Assert.True(ClientApplicationSecret.LooksLikeBcrypt(captured!.ClientSecret));
        Assert.NotEqual(result.PlaintextClientSecret, captured.ClientSecret);
        Assert.True(ClientApplicationSecret.Verify(result.PlaintextClientSecret, captured.ClientSecret));
    }

    [Fact]
    public async Task RegisterApplicationAsync_When_NameExists_Throws()
    {
        var existing = new ClientApplication { Id = 1, Name = "MyApp" };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByName("MyApp", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var command = new Mock<ICommandService<ClientApplication, int>>();

        var sut = new ApplicationService(query.Object, command.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterApplicationAsync("MyApp", CancellationToken.None));

        Assert.Equal("An API client with this name already exists.", ex.Message);
        command.Verify(c => c.Add(It.IsAny<ClientApplication>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterApplicationAsync_When_NameBlank_ThrowsArgumentException()
    {
        var query = new Mock<IClientApplicationQueryService>();
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.RegisterApplicationAsync("  ", CancellationToken.None));

        Assert.Equal("API client name is required.", ex.Message);
        command.Verify(c => c.Add(It.IsAny<ClientApplication>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterApplicationAsync_When_NameTooLong_ThrowsArgumentException()
    {
        var query = new Mock<IClientApplicationQueryService>();
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);
        var tooLong = new string('a', ApplicationService.MaxClientNameLength + 1);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.RegisterApplicationAsync(tooLong, CancellationToken.None));

        Assert.Contains("at most", ex.Message);
    }

    [Fact]
    public async Task RegisterApplicationAsync_Trims_Name_Before_Duplicate_Check()
    {
        var existing = new ClientApplication { Id = 1, Name = "MyApp" };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByName("MyApp", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RegisterApplicationAsync("  MyApp  ", CancellationToken.None));
    }

    [Fact]
    public async Task GetApplicationByClientIdAsync_Returns_FromQuery()
    {
        var app = new ClientApplication { Id = 2, Name = "X", ClientId = "cid" };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();

        var sut = new ApplicationService(query.Object, command.Object);

        var result = await sut.GetApplicationByClientIdAsync("cid", CancellationToken.None);

        Assert.Same(app, result);
    }

    [Fact]
    public async Task RevokeApplicationAsync_When_NotFound_ReturnsFalse()
    {
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientApplication?)null);
        var command = new Mock<ICommandService<ClientApplication, int>>();

        var sut = new ApplicationService(query.Object, command.Object);

        var result = await sut.RevokeApplicationAsync("missing", CancellationToken.None);

        Assert.False(result);
        command.Verify(c => c.Update(It.IsAny<ClientApplication>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeApplicationAsync_When_System_Throws()
    {
        var app = new ClientApplication { ClientId = "sys", Name = "Bot", IsSystem = true };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("sys", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();

        var sut = new ApplicationService(query.Object, command.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RevokeApplicationAsync("sys", CancellationToken.None));

        Assert.Equal("System API clients cannot be revoked.", ex.Message);
    }

    [Fact]
    public async Task RevokeApplicationAsync_When_AlreadyRevoked_ReturnsTrueWithoutUpdate()
    {
        var app = new ClientApplication { ClientId = "cid", Name = "X", IsRevoked = true };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();

        var sut = new ApplicationService(query.Object, command.Object);

        var result = await sut.RevokeApplicationAsync("cid", CancellationToken.None);

        Assert.True(result);
        command.Verify(c => c.Update(It.IsAny<ClientApplication>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeApplicationAsync_When_Active_RevokesAndReturnsTrue()
    {
        var app = new ClientApplication { ClientId = "cid", Name = "X", IsRevoked = false };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();

        var sut = new ApplicationService(query.Object, command.Object);

        var result = await sut.RevokeApplicationAsync("cid", CancellationToken.None);

        Assert.True(result);
        Assert.True(app.IsRevoked);
        command.Verify(c => c.Update(app, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateClientAsync_When_MissingOrBlank_ReturnsNull()
    {
        var query = new Mock<IClientApplicationQueryService>();
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        Assert.Null(await sut.AuthenticateClientAsync("", "secret", CancellationToken.None));
        Assert.Null(await sut.AuthenticateClientAsync("cid", "", CancellationToken.None));
        query.Verify(q => q.GetByClientId(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateClientAsync_When_NotFound_ReturnsNull()
    {
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientApplication?)null);
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        Assert.Null(await sut.AuthenticateClientAsync("cid", "secret", CancellationToken.None));
    }

    [Fact]
    public async Task AuthenticateClientAsync_When_Revoked_ReturnsNull()
    {
        var app = new ClientApplication
        {
            ClientId = "cid",
            ClientSecret = ClientApplicationSecret.Hash("secret"),
            IsRevoked = true,
        };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        Assert.Null(await sut.AuthenticateClientAsync("cid", "secret", CancellationToken.None));
        command.Verify(c => c.Update(It.IsAny<ClientApplication>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateClientAsync_When_WrongSecret_ReturnsNull()
    {
        var app = new ClientApplication
        {
            ClientId = "cid",
            ClientSecret = ClientApplicationSecret.Hash("secret"),
        };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        Assert.Null(await sut.AuthenticateClientAsync("cid", "wrong", CancellationToken.None));
    }

    [Fact]
    public async Task AuthenticateClientAsync_When_BcryptSecret_Valid_ReturnsAppWithoutUpdate()
    {
        var app = new ClientApplication
        {
            ClientId = "cid",
            ClientSecret = ClientApplicationSecret.Hash("secret"),
            IsSystem = true,
        };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        var result = await sut.AuthenticateClientAsync("cid", "secret", CancellationToken.None);

        Assert.Same(app, result);
        command.Verify(c => c.Update(It.IsAny<ClientApplication>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateClientAsync_When_LegacyPlaintext_Valid_UpgradesToBcrypt()
    {
        var app = new ClientApplication
        {
            ClientId = "cid",
            ClientSecret = "legacy-plain-secret",
            IsSystem = false,
        };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();
        command
            .Setup(c => c.Update(app, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = new ApplicationService(query.Object, command.Object);

        var result = await sut.AuthenticateClientAsync("cid", "legacy-plain-secret", CancellationToken.None);

        Assert.Same(app, result);
        Assert.True(ClientApplicationSecret.LooksLikeBcrypt(app.ClientSecret));
        Assert.True(ClientApplicationSecret.Verify("legacy-plain-secret", app.ClientSecret));
        command.Verify(c => c.Update(app, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateClientAsync_When_LegacyPlaintext_Wrong_DoesNotUpgrade()
    {
        var app = new ClientApplication
        {
            ClientId = "cid",
            ClientSecret = "legacy-plain-secret",
        };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        Assert.Null(await sut.AuthenticateClientAsync("cid", "nope", CancellationToken.None));
        Assert.Equal("legacy-plain-secret", app.ClientSecret);
        command.Verify(c => c.Update(It.IsAny<ClientApplication>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateClientAsync_Trims_ClientId()
    {
        var app = new ClientApplication
        {
            ClientId = "cid",
            ClientSecret = ClientApplicationSecret.Hash("secret"),
        };
        var query = new Mock<IClientApplicationQueryService>();
        query.Setup(q => q.GetByClientId("cid", It.IsAny<CancellationToken>())).ReturnsAsync(app);
        var command = new Mock<ICommandService<ClientApplication, int>>();
        var sut = new ApplicationService(query.Object, command.Object);

        var result = await sut.AuthenticateClientAsync("  cid  ", "secret", CancellationToken.None);

        Assert.Same(app, result);
    }
}
