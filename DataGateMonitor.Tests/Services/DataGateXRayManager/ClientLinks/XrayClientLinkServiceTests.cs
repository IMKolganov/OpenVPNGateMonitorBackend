using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTable;
using DataGateMonitor.DataBase.Services.Query.IssuedXrayClientLinkTokenTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Models;
using DataGateMonitor.Services.DataGateXRayManager.ClientLinks;
using DataGateMonitor.Services.Others.Notifications.OvpnFileApi;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.SharedModels.Auth;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Requests;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.Tests.Services.DataGateXRayManager.ClientLinks;

public class XrayClientLinkServiceTests
{
    [Fact]
    public async Task GetAllByExternalId_WithTelegramId_ReturnsRowsFromAllLinkedIdentityIds()
    {
        const string telegramId = "123456789";
        const string googleSub = "accounts.google.com:sub-xray";
        var googleLink = new IssuedXrayClientLink
        {
            Id = 1,
            ExternalId = googleSub,
            VpnServerId = 2,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };
        var legacyLink = new IssuedXrayClientLink
        {
            Id = 2,
            ExternalId = telegramId,
            VpnServerId = 2,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };

        var linkQuery = new Mock<IIssuedXrayClientLinkQueryService>(MockBehavior.Strict);
        linkQuery.Setup(q => q.GetAllByExternalId(googleSub, It.IsAny<CancellationToken>()))
            .ReturnsAsync([googleLink]);
        linkQuery.Setup(q => q.GetAllByExternalId(telegramId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([legacyLink]);

        var identityLinkQuery = CreateMergedUserIdentityLinkQuery(telegramId, googleSub);
        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyReadByExternalId(
                googleSub, 2, It.IsAny<CancellationToken>(), VpnProfileNotificationStack.Xray))
            .Returns(Task.CompletedTask);

        var sut = CreateService(linkQuery: linkQuery, notification: notification, identityLinkQuery: identityLinkQuery);
        var result = await sut.GetAllByExternalId(telegramId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(l => l.Id).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task GetAllByExternalIdAndVpnServerId_WithTelegramId_QueriesBothLinkedIdentityIds()
    {
        const string telegramId = "444555666";
        const string googleSub = "google-sub-xray-server";
        const int vpnServerId = 9;

        var xrayServer = new VpnServer
        {
            Id = vpnServerId,
            ServerType = VpnServerType.Xray,
            ServerName = "xray-9",
            ApiUrl = "https://xray.test/",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };

        var serverQuery = new Mock<IVpnServerQueryService>(MockBehavior.Strict);
        serverQuery.Setup(q => q.GetById(vpnServerId, It.IsAny<CancellationToken>())).ReturnsAsync(xrayServer);

        var linkQuery = new Mock<IIssuedXrayClientLinkQueryService>(MockBehavior.Strict);
        linkQuery.Setup(q => q.GetAllByVpnServerIdAndExternalIdAndIsRevoked(
                vpnServerId, googleSub, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        linkQuery.Setup(q => q.GetAllByVpnServerIdAndExternalIdAndIsRevoked(
                vpnServerId, telegramId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new IssuedXrayClientLink
                {
                    Id = 7,
                    ExternalId = telegramId,
                    VpnServerId = vpnServerId,
                    CreateDate = DateTimeOffset.UtcNow,
                    LastUpdate = DateTimeOffset.UtcNow,
                },
            ]);

        var identityLinkQuery = CreateMergedUserIdentityLinkQuery(telegramId, googleSub);
        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyReadByExternalIdAndVpnServerId(
                vpnServerId, googleSub, 1, false, It.IsAny<CancellationToken>(), VpnProfileNotificationStack.Xray))
            .Returns(Task.CompletedTask);

        var sut = CreateService(
            linkQuery: linkQuery,
            serverQuery: serverQuery,
            notification: notification,
            identityLinkQuery: identityLinkQuery);

        var result = await sut.GetAllByExternalIdAndVpnServerId(vpnServerId, telegramId, CancellationToken.None);

        result.Should().ContainSingle(l => l.Id == 7);
    }

    [Fact]
    public async Task AddClientLink_WithTelegramId_PersistsGoogleSub()
    {
        const string telegramId = "777888999";
        const string googleSub = "accounts.google.com:sub-xray-add";
        const int vpnServerId = 4;
        const string commonName = "xray-bot-profile";

        var xrayServer = new VpnServer
        {
            Id = vpnServerId,
            ServerType = VpnServerType.Xray,
            ServerName = "xray-4",
            ApiUrl = "https://xray.test/",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };
        var exportConfig = new VpnServerOvpnFileConfig
        {
            VpnServerId = vpnServerId,
            ConfigTemplate = "vless-template",
            VpnServerIp = "vpn.example.com",
            VpnServerPort = 443,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };

        var serverQuery = new Mock<IVpnServerQueryService>(MockBehavior.Strict);
        serverQuery.Setup(q => q.GetById(vpnServerId, It.IsAny<CancellationToken>())).ReturnsAsync(xrayServer);

        var configQuery = new Mock<IVpnServerOvpnFileConfigQueryService>(MockBehavior.Strict);
        configQuery.Setup(q => q.GetByVpnServerIdId(vpnServerId, It.IsAny<CancellationToken>())).ReturnsAsync(exportConfig);

        var linkQuery = new Mock<IIssuedXrayClientLinkQueryService>(MockBehavior.Strict);
        linkQuery.Setup(q => q.ExistsActiveByVpnServerIdAndCommonName(vpnServerId, commonName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        linkQuery.Setup(q => q.GetByVpnServerIdAndCommonName(51, vpnServerId, commonName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, int _, string _, CancellationToken _) => new IssuedXrayClientLink
            {
                Id = id,
                ExternalId = googleSub,
                CommonName = commonName,
                VpnServerId = vpnServerId,
                CreateDate = DateTimeOffset.UtcNow,
                LastUpdate = DateTimeOffset.UtcNow,
            });

        var microserviceClient = new Mock<IXrayClientLinkMicroserviceClient>(MockBehavior.Strict);
        microserviceClient.Setup(c => c.AddClientLink(
                vpnServerId,
                It.Is<GenerateClientLinkMicroserviceRequest>(r => r.CommonName == commonName),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientLinkMetadataDto
            {
                CommonName = commonName,
                FileName = $"{commonName}.txt",
                FilePath = $"/links/{commonName}.txt",
                IssuedAt = DateTime.UtcNow,
                IssuedTo = "bot",
            });

        IssuedXrayClientLink? captured = null;
        var linkCommand = new Mock<ICommandService<IssuedXrayClientLink, int>>(MockBehavior.Strict);
        linkCommand.Setup(c => c.Add(It.IsAny<IssuedXrayClientLink>(), true, It.IsAny<CancellationToken>()))
            .Callback<IssuedXrayClientLink, bool, CancellationToken>((entity, _, _) => captured = entity)
            .ReturnsAsync((IssuedXrayClientLink entity, bool _, CancellationToken _) =>
            {
                entity.Id = 51;
                return entity;
            });

        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyIssued(
                vpnServerId, 51, $"{commonName}.txt", googleSub, It.IsAny<CancellationToken>(),
                VpnProfileNotificationStack.Xray))
            .Returns(Task.CompletedTask);

        var identityLinkQuery = CreateMergedUserIdentityLinkQuery(telegramId, googleSub);
        var userQuotaPlanQuery = new Mock<IUserQuotaPlanQueryService>(MockBehavior.Strict);
        userQuotaPlanQuery.Setup(q => q.GetActiveByUserId(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { UserId = 10, QuotaPlanId = 8 });
        var quotaPlanAllowedServerQuery = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        quotaPlanAllowedServerQuery.Setup(q => q.GetByQuotaPlanIdAndServerId(8, vpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaPlanAllowedServer { QuotaPlanId = 8, VpnServerId = vpnServerId });
        var sut = CreateService(
            linkQuery: linkQuery,
            serverQuery: serverQuery,
            configQuery: configQuery,
            microserviceClient: microserviceClient,
            linkCommand: linkCommand,
            notification: notification,
            identityLinkQuery: identityLinkQuery,
            userQuotaPlanQuery: userQuotaPlanQuery,
            quotaPlanAllowedServerQuery: quotaPlanAllowedServerQuery);

        var request = new AddFileRequest
        {
            VpnServerId = vpnServerId,
            CommonName = commonName,
            ExternalId = telegramId,
            IssuedTo = $"telegram user {telegramId}",
        };

        var result = await sut.AddClientLink(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ExternalId.Should().Be(googleSub);
        request.ExternalId.Should().Be(googleSub);
        result.ExternalId.Should().Be(googleSub);
        quotaPlanAllowedServerQuery.VerifyAll();
    }

    [Fact]
    public async Task AddClientLink_WhenTargetUserPlanDoesNotAllowServer_ThrowsBeforeIssuing()
    {
        const string externalId = "telegram-xray-locked";
        const int userId = 321;
        const int quotaPlanId = 45;
        const int vpnServerId = 100;

        var identityLinkQuery = new Mock<IUserIdentityLinkQueryService>(MockBehavior.Strict);
        identityLinkQuery.Setup(q => q.GetByExternalId(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink
            {
                UserId = userId,
                Provider = AuthIdentityProviders.Telegram,
                ExternalId = externalId,
            });
        identityLinkQuery.Setup(q => q.GetListByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserIdentityLink
            {
                UserId = userId,
                Provider = AuthIdentityProviders.Telegram,
                ExternalId = externalId,
            }]);

        var userQuotaPlanQuery = new Mock<IUserQuotaPlanQueryService>(MockBehavior.Strict);
        userQuotaPlanQuery.Setup(q => q.GetActiveByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { UserId = userId, QuotaPlanId = quotaPlanId });
        var quotaPlanAllowedServerQuery = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        quotaPlanAllowedServerQuery.Setup(q => q.GetByQuotaPlanIdAndServerId(quotaPlanId, vpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuotaPlanAllowedServer?)null);

        var microserviceClient = new Mock<IXrayClientLinkMicroserviceClient>(MockBehavior.Strict);
        var linkCommand = new Mock<ICommandService<IssuedXrayClientLink, int>>(MockBehavior.Strict);
        var sut = CreateService(
            microserviceClient: microserviceClient,
            linkCommand: linkCommand,
            identityLinkQuery: identityLinkQuery,
            userQuotaPlanQuery: userQuotaPlanQuery,
            quotaPlanAllowedServerQuery: quotaPlanAllowedServerQuery);

        var act = () => sut.AddClientLink(new AddFileRequest
        {
            VpnServerId = vpnServerId,
            CommonName = "locked-xray-cn",
            ExternalId = externalId,
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("VpnServerNotAllowedByQuotaPlan");
        microserviceClient.Verify(c => c.AddClientLink(It.IsAny<int>(), It.IsAny<GenerateClientLinkMicroserviceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        linkCommand.Verify(c => c.Add(It.IsAny<IssuedXrayClientLink>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IUserIdentityLinkQueryService> CreateMergedUserIdentityLinkQuery(
        string telegramId,
        string googleSub,
        int userId = 10)
    {
        var identityLinkQuery = new Mock<IUserIdentityLinkQueryService>(MockBehavior.Strict);
        identityLinkQuery.Setup(q => q.GetByExternalId(telegramId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink
            {
                UserId = userId,
                Provider = AuthIdentityProviders.Telegram,
                ExternalId = telegramId,
            });
        identityLinkQuery.Setup(q => q.GetByExternalId(googleSub, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentityLink
            {
                UserId = userId,
                Provider = AuthIdentityProviders.Google,
                ExternalId = googleSub,
            });
        identityLinkQuery.Setup(q => q.GetListByUserId(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new UserIdentityLink { Provider = AuthIdentityProviders.Telegram, ExternalId = telegramId },
                new UserIdentityLink { Provider = AuthIdentityProviders.Google, ExternalId = googleSub },
            ]);
        return identityLinkQuery;
    }

    private static XrayClientLinkService CreateService(
        Mock<IXrayClientLinkMicroserviceClient>? microserviceClient = null,
        Mock<IIssuedXrayClientLinkQueryService>? linkQuery = null,
        Mock<IIssuedXrayClientLinkTokenQueryService>? tokenQuery = null,
        Mock<IVpnServerOvpnFileConfigQueryService>? configQuery = null,
        Mock<ICommandService<IssuedXrayClientLink, int>>? linkCommand = null,
        Mock<IVpnServerQueryService>? serverQuery = null,
        Mock<IOvpnFileNotificationService>? notification = null,
        Mock<IUserIdentityLinkQueryService>? identityLinkQuery = null,
        Mock<IUserQuotaPlanQueryService>? userQuotaPlanQuery = null,
        Mock<IQuotaPlanAllowedServerQueryService>? quotaPlanAllowedServerQuery = null,
        Mock<IUserQueryService>? userQuery = null)
    {
        microserviceClient ??= new Mock<IXrayClientLinkMicroserviceClient>(MockBehavior.Loose);
        var logger = Mock.Of<ILogger<XrayClientLinkService>>();
        var config = new ConfigurationBuilder().Build();
        configQuery ??= new Mock<IVpnServerOvpnFileConfigQueryService>(MockBehavior.Loose);
        linkQuery ??= new Mock<IIssuedXrayClientLinkQueryService>(MockBehavior.Loose);
        tokenQuery ??= new Mock<IIssuedXrayClientLinkTokenQueryService>(MockBehavior.Loose);
        linkCommand ??= new Mock<ICommandService<IssuedXrayClientLink, int>>(MockBehavior.Loose);
        var tokenCommand = new Mock<ICommandService<IssuedXrayClientLinkToken, int>>(MockBehavior.Loose);
        serverQuery ??= new Mock<IVpnServerQueryService>(MockBehavior.Loose);
        notification ??= new Mock<IOvpnFileNotificationService>(MockBehavior.Loose);
        identityLinkQuery ??= new Mock<IUserIdentityLinkQueryService>(MockBehavior.Loose);
        userQuotaPlanQuery ??= new Mock<IUserQuotaPlanQueryService>(MockBehavior.Loose);
        quotaPlanAllowedServerQuery ??= new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Loose);
        userQuery ??= new Mock<IUserQueryService>(MockBehavior.Loose);

        IVpnServerQuotaPlanAccessGuard accessGuard = new VpnServerQuotaPlanAccessGuard(
            identityLinkQuery.Object,
            userQuotaPlanQuery.Object,
            quotaPlanAllowedServerQuery.Object,
            userQuery.Object);

        return new XrayClientLinkService(
            microserviceClient.Object,
            logger,
            config,
            configQuery.Object,
            linkQuery.Object,
            tokenQuery.Object,
            identityLinkQuery.Object,
            accessGuard,
            linkCommand.Object,
            tokenCommand.Object,
            serverQuery.Object,
            notification.Object);
    }
}
