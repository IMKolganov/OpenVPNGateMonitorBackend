using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.UserVpnServerAccessRuleTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.SharedModels.DataGateMonitor.UserVpnServerAccessRules.Requests;
using DataGateMonitor.SharedModels.Enums;
using Moq;
using Xunit;

namespace DataGateMonitor.Tests.Services.VpnAccess;

public class UserVpnServerAccessRuleServiceTests
{
    [Fact]
    public async Task CreateAsync_WhenMissing_AddsNewRule()
    {
        var query = new Mock<IUserVpnServerAccessRuleQueryService>(MockBehavior.Strict);
        query.Setup(q => q.GetByUserIdAndServerId(5, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVpnServerAccessRule?)null);

        var command = new Mock<ICommandService<UserVpnServerAccessRule, int>>(MockBehavior.Strict);
        command.Setup(c => c.Add(It.IsAny<UserVpnServerAccessRule>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVpnServerAccessRule e, bool _, CancellationToken __) =>
            {
                e.Id = 100;
                return e;
            });

        var sut = new UserVpnServerAccessRuleService(query.Object, command.Object);
        var response = await sut.CreateAsync(new CreateOrUpdateUserVpnServerAccessRuleRequest
        {
            UserId = 5,
            VpnServerId = 9,
            Mode = VpnServerAccessRuleMode.Allow
        }, CancellationToken.None);

        Assert.Equal(100, response.UserVpnServerAccessRule!.Id);
        Assert.Equal(VpnServerAccessRuleMode.Allow, response.UserVpnServerAccessRule.Mode);
        command.Verify(c => c.Add(It.IsAny<UserVpnServerAccessRule>(), true, It.IsAny<CancellationToken>()), Times.Once);
        command.Verify(c => c.Update(It.IsAny<UserVpnServerAccessRule>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenExistingDifferentMode_FlipsModeAndUpdates()
    {
        var existing = new UserVpnServerAccessRule
        {
            Id = 44,
            UserId = 5,
            VpnServerId = 9,
            Mode = VpnServerAccessRuleMode.Allow
        };

        var query = new Mock<IUserVpnServerAccessRuleQueryService>(MockBehavior.Strict);
        query.Setup(q => q.GetByUserIdAndServerId(5, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new Mock<ICommandService<UserVpnServerAccessRule, int>>(MockBehavior.Strict);
        command.Setup(c => c.Update(existing, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var sut = new UserVpnServerAccessRuleService(query.Object, command.Object);
        var response = await sut.CreateAsync(new CreateOrUpdateUserVpnServerAccessRuleRequest
        {
            UserId = 5,
            VpnServerId = 9,
            Mode = VpnServerAccessRuleMode.Deny
        }, CancellationToken.None);

        Assert.Equal(44, response.UserVpnServerAccessRule!.Id);
        Assert.Equal(VpnServerAccessRuleMode.Deny, existing.Mode);
        command.Verify(c => c.Update(existing, true, It.IsAny<CancellationToken>()), Times.Once);
        command.Verify(c => c.Add(It.IsAny<UserVpnServerAccessRule>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenExistingSameMode_DoesNotUpdate()
    {
        var existing = new UserVpnServerAccessRule
        {
            Id = 44,
            UserId = 5,
            VpnServerId = 9,
            Mode = VpnServerAccessRuleMode.Deny
        };

        var query = new Mock<IUserVpnServerAccessRuleQueryService>(MockBehavior.Strict);
        query.Setup(q => q.GetByUserIdAndServerId(5, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new Mock<ICommandService<UserVpnServerAccessRule, int>>(MockBehavior.Strict);

        var sut = new UserVpnServerAccessRuleService(query.Object, command.Object);
        var response = await sut.CreateAsync(new CreateOrUpdateUserVpnServerAccessRuleRequest
        {
            UserId = 5,
            VpnServerId = 9,
            Mode = VpnServerAccessRuleMode.Deny
        }, CancellationToken.None);

        Assert.Equal(44, response.UserVpnServerAccessRule!.Id);
        command.Verify(c => c.Update(It.IsAny<UserVpnServerAccessRule>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        command.Verify(c => c.Add(It.IsAny<UserVpnServerAccessRule>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
