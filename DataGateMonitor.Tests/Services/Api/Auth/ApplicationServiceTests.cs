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
    public async Task RegisterApplicationAsync_When_NameNotExists_CreatesAndReturns()
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
        Assert.Equal(7, result.Id);
        Assert.NotNull(captured);
        Assert.Equal("MyApp", captured!.Name);
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
}
