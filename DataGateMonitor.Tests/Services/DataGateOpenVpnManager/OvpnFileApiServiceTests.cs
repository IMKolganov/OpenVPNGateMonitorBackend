using FluentAssertions;
using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using DataGateMonitor.DataBase.Services.Command.Interfaces;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTable;
using DataGateMonitor.DataBase.Services.Query.IssuedOvpnFileTokenTable;
using DataGateMonitor.DataBase.Services.Query.QuotaPlanAllowedServerTable;
using DataGateMonitor.DataBase.Services.Query.UserIdentityLinkTable;
using DataGateMonitor.DataBase.Services.Query.UserQuotaPlanTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerOvpnFileConfigTable;
using DataGateMonitor.DataBase.Services.Query.VpnServerTable;
using DataGateMonitor.Mapping.DataGateOpenVpnManager.Mappings;
using DataGateMonitor.Models;
using DataGateMonitor.Services.DataGateOpenVpnManager;
using DataGateMonitor.Services.DataGateOpenVpnManager.Interfaces;
using DataGateMonitor.Services.Others.Notifications.OvpnFileApi;
using DataGateMonitor.DataBase.Services.Query.UserTable;
using DataGateMonitor.Services.VpnAccess;
using DataGateMonitor.SharedModels.Auth;
using DataGateMonitor.SharedModels.DataGateMonitor.OpenVpnFiles.Requests;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.OvpnFile.Requests;
using DataGateMonitor.SharedModels.DataGateOpenVpnManager.OvpnFile.Responses;
using DataGateMonitor.SharedModels.Enums;
using Xunit;

namespace DataGateMonitor.Tests.Services.DataGateOpenVpnManager;

public class OvpnFileApiServiceTests
{
    static OvpnFileApiServiceTests()
    {
        new DataGateOpenVpnManagerMapping().Register(TypeAdapterConfig.GlobalSettings);
    }

    [Fact]
    public async Task GetByToken_WhenTokenNotFound_ThrowsInvalidOperationException()
    {
        var tokenQuery = new Mock<IIssuedOvpnFileTokenQueryService>(MockBehavior.Strict);
        tokenQuery.Setup(q => q.GetByToken("bad-token", It.IsAny<CancellationToken>())).ReturnsAsync((IssuedOvpnFileToken?)null);

        var sut = CreateService(tokenQuery: tokenQuery);
        var act = () => sut.GetByToken("bad-token", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IssuedOvpnFileToken not found*");
        tokenQuery.VerifyAll();
    }

    [Fact]
    public async Task GetByToken_WhenTokenAndFileExist_ReturnsFile_AndCallsNotify()
    {
        var token = new IssuedOvpnFileToken { Id = 1, IssuedOvpnFileId = 10, Token = "valid-token", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow };
        var file = new IssuedOvpnFile { Id = 10, VpnServerId = 1, CommonName = "cn", FileName = "f.ovpn", CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow };
        var tokenQuery = new Mock<IIssuedOvpnFileTokenQueryService>(MockBehavior.Strict);
        tokenQuery.Setup(q => q.GetByToken("valid-token", It.IsAny<CancellationToken>())).ReturnsAsync(token);
        var fileQuery = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        fileQuery.Setup(q => q.GetByIdAndIsRevoked(10, false, It.IsAny<CancellationToken>())).ReturnsAsync(file);
        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyReadByToken("valid-token", 10, 1, false, It.IsAny<CancellationToken>(),
            It.IsAny<VpnProfileNotificationStack>())).Returns(Task.CompletedTask);

        var sut = CreateService(tokenQuery: tokenQuery, fileQuery: fileQuery, notification: notification);
        var result = await sut.GetByToken("valid-token", CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(10);
        result.CommonName.Should().Be("cn");
        notification.Verify(
            n => n.NotifyReadByToken("valid-token", 10, 1, false, It.IsAny<CancellationToken>(),
                VpnProfileNotificationStack.OpenVpn), Times.Once);
    }

    [Fact]
    public async Task GetAllByExternalId_ReturnsFiles_AndCallsNotify()
    {
        var files = new List<IssuedOvpnFile>
        {
            new() { Id = 1, ExternalId = "ext1", VpnServerId = 1, CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow },
            new() { Id = 2, ExternalId = "ext1", VpnServerId = 1, CreateDate = DateTimeOffset.UtcNow, LastUpdate = DateTimeOffset.UtcNow }
        };
        var fileQuery = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        fileQuery.Setup(q => q.GetAllByExternalId("ext1", It.IsAny<CancellationToken>())).ReturnsAsync(files);
        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyReadByExternalId("ext1", 2, It.IsAny<CancellationToken>(),
            It.IsAny<VpnProfileNotificationStack>())).Returns(Task.CompletedTask);

        var sut = CreateService(fileQuery: fileQuery, notification: notification);
        var result = await sut.GetAllByExternalId("ext1", CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].ExternalId.Should().Be("ext1");
        notification.Verify(
            n => n.NotifyReadByExternalId("ext1", 2, It.IsAny<CancellationToken>(), VpnProfileNotificationStack.OpenVpn),
            Times.Once);
    }

    [Fact]
    public async Task GetAllByExternalId_WithTelegramId_ReturnsRowsFromAllLinkedIdentityIds()
    {
        const string telegramId = "123456789";
        const string googleSub = "accounts.google.com:sub-abc";
        var googleFile = new IssuedOvpnFile
        {
            Id = 1,
            ExternalId = googleSub,
            VpnServerId = 1,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };
        var legacyFile = new IssuedOvpnFile
        {
            Id = 2,
            ExternalId = telegramId,
            VpnServerId = 1,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };

        var fileQuery = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        fileQuery.Setup(q => q.GetAllByExternalId(googleSub, It.IsAny<CancellationToken>()))
            .ReturnsAsync([googleFile]);
        fileQuery.Setup(q => q.GetAllByExternalId(telegramId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([legacyFile]);

        var identityLinkQuery = CreateMergedUserIdentityLinkQuery(telegramId, googleSub);
        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyReadByExternalId(googleSub, 2, It.IsAny<CancellationToken>(),
            It.IsAny<VpnProfileNotificationStack>())).Returns(Task.CompletedTask);

        var sut = CreateService(fileQuery: fileQuery, notification: notification, identityLinkQuery: identityLinkQuery);
        var result = await sut.GetAllByExternalId(telegramId, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(f => f.Id).Should().BeEquivalentTo([1, 2]);
        fileQuery.Verify(q => q.GetAllByExternalId(googleSub, It.IsAny<CancellationToken>()), Times.Once);
        fileQuery.Verify(q => q.GetAllByExternalId(telegramId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllByExternalIdAndVpnServerId_WithTelegramId_QueriesBothLinkedIdentityIds()
    {
        const string telegramId = "987654321";
        const string googleSub = "google-sub-server";
        const int vpnServerId = 3;

        var fileQuery = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        fileQuery.Setup(q => q.GetAllByVpnServerIdAndExternalIdAndIsRevoked(
                vpnServerId, googleSub, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        fileQuery.Setup(q => q.GetAllByVpnServerIdAndExternalIdAndIsRevoked(
                vpnServerId, telegramId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new IssuedOvpnFile
                {
                    Id = 5,
                    ExternalId = telegramId,
                    VpnServerId = vpnServerId,
                    CreateDate = DateTimeOffset.UtcNow,
                    LastUpdate = DateTimeOffset.UtcNow,
                },
            ]);

        var identityLinkQuery = CreateMergedUserIdentityLinkQuery(telegramId, googleSub);
        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyReadByExternalIdAndVpnServerId(
                vpnServerId, googleSub, 1, false, It.IsAny<CancellationToken>(), It.IsAny<VpnProfileNotificationStack>()))
            .Returns(Task.CompletedTask);

        var sut = CreateService(fileQuery: fileQuery, notification: notification, identityLinkQuery: identityLinkQuery);
        var result = await sut.GetAllByExternalIdAndVpnServerId(vpnServerId, telegramId, CancellationToken.None);

        result.Should().ContainSingle(f => f.Id == 5);
    }

    [Fact]
    public async Task AddOvpnFile_WithTelegramId_PersistsGoogleSub()
    {
        const string telegramId = "555666777";
        const string googleSub = "accounts.google.com:sub-persist";
        const int vpnServerId = 1;
        const string commonName = "adg-1-bot-profile";

        var server = new VpnServer
        {
            Id = vpnServerId,
            ServerType = VpnServerType.OpenVpn,
            ServerName = "s1",
            ApiUrl = "https://vpn.test/",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };
        var ovpnConfig = new VpnServerOvpnFileConfig
        {
            VpnServerId = vpnServerId,
            ConfigTemplate = "template",
            VpnServerIp = "10.0.0.1",
            VpnServerPort = 1194,
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow,
        };

        var serverQuery = new Mock<IVpnServerQueryService>(MockBehavior.Strict);
        serverQuery.Setup(q => q.GetById(vpnServerId, It.IsAny<CancellationToken>())).ReturnsAsync(server);

        var configQuery = new Mock<IVpnServerOvpnFileConfigQueryService>(MockBehavior.Strict);
        configQuery.Setup(q => q.GetByVpnServerIdId(vpnServerId, It.IsAny<CancellationToken>())).ReturnsAsync(ovpnConfig);

        var fileQuery = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        fileQuery.Setup(q => q.ExistsActiveByVpnServerIdAndCommonName(vpnServerId, commonName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        fileQuery.Setup(q => q.GetByVpnServerIdAndCommonName(42, vpnServerId, commonName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, int _, string _, CancellationToken _) => new IssuedOvpnFile
            {
                Id = id,
                ExternalId = googleSub,
                CommonName = commonName,
                VpnServerId = vpnServerId,
                CreateDate = DateTimeOffset.UtcNow,
                LastUpdate = DateTimeOffset.UtcNow,
            });

        var ovpnClient = new Mock<IOvpnFileApiClient>(MockBehavior.Strict);
        ovpnClient.Setup(c => c.AddOvpnFile(vpnServerId, It.IsAny<GenerateOvpnFileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OvpnFileMetadata
            {
                CommonName = commonName,
                FileName = $"{commonName}.ovpn",
                FilePath = $"/pki/{commonName}.ovpn",
                IssuedAt = DateTime.UtcNow,
            });

        IssuedOvpnFile? captured = null;
        var fileCommand = new Mock<ICommandService<IssuedOvpnFile, int>>(MockBehavior.Strict);
        fileCommand.Setup(c => c.Add(It.IsAny<IssuedOvpnFile>(), true, It.IsAny<CancellationToken>()))
            .Callback<IssuedOvpnFile, bool, CancellationToken>((entity, _, _) => captured = entity)
            .ReturnsAsync((IssuedOvpnFile entity, bool _, CancellationToken _) =>
            {
                entity.Id = 42;
                return entity;
            });

        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyIssued(
                vpnServerId,
                It.IsAny<int>(),
                It.IsAny<string>(),
                googleSub,
                It.IsAny<CancellationToken>(),
                VpnProfileNotificationStack.OpenVpn))
            .Returns(Task.CompletedTask);

        var identityLinkQuery = CreateMergedUserIdentityLinkQuery(telegramId, googleSub);
        var userQuotaPlanQuery = new Mock<IUserQuotaPlanQueryService>(MockBehavior.Strict);
        userQuotaPlanQuery.Setup(q => q.GetActiveByUserId(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserQuotaPlan { UserId = 10, QuotaPlanId = 7 });
        var quotaPlanAllowedServerQuery = new Mock<IQuotaPlanAllowedServerQueryService>(MockBehavior.Strict);
        quotaPlanAllowedServerQuery.Setup(q => q.GetByQuotaPlanIdAndServerId(7, vpnServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaPlanAllowedServer { QuotaPlanId = 7, VpnServerId = vpnServerId });
        var sut = CreateService(
            ovpnClient: ovpnClient,
            fileQuery: fileQuery,
            serverQuery: serverQuery,
            notification: notification,
            identityLinkQuery: identityLinkQuery,
            userQuotaPlanQuery: userQuotaPlanQuery,
            quotaPlanAllowedServerQuery: quotaPlanAllowedServerQuery,
            configQuery: configQuery,
            fileCommand: fileCommand);

        var request = new AddFileRequest
        {
            VpnServerId = vpnServerId,
            CommonName = commonName,
            ExternalId = telegramId,
            IssuedTo = $"telegram user {telegramId}",
        };

        var result = await sut.AddOvpnFile(request, CancellationToken.None);

        request.ExternalId.Should().Be(googleSub);
        captured.Should().NotBeNull();
        captured!.ExternalId.Should().Be(googleSub);
        result.ExternalId.Should().Be(googleSub);
        quotaPlanAllowedServerQuery.VerifyAll();
    }

    [Fact]
    public async Task AddOvpnFile_WhenTargetUserPlanDoesNotAllowServer_ThrowsBeforeIssuing()
    {
        const string externalId = "telegram-locked";
        const int userId = 123;
        const int quotaPlanId = 44;
        const int vpnServerId = 99;

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

        var ovpnClient = new Mock<IOvpnFileApiClient>(MockBehavior.Strict);
        var fileCommand = new Mock<ICommandService<IssuedOvpnFile, int>>(MockBehavior.Strict);
        var sut = CreateService(
            ovpnClient: ovpnClient,
            identityLinkQuery: identityLinkQuery,
            userQuotaPlanQuery: userQuotaPlanQuery,
            quotaPlanAllowedServerQuery: quotaPlanAllowedServerQuery,
            fileCommand: fileCommand);

        var act = () => sut.AddOvpnFile(new AddFileRequest
        {
            VpnServerId = vpnServerId,
            CommonName = "locked-cn",
            ExternalId = externalId,
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("VpnServerNotAllowedByQuotaPlan");
        ovpnClient.Verify(c => c.AddOvpnFile(It.IsAny<int>(), It.IsAny<GenerateOvpnFileRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        fileCommand.Verify(c => c.Add(It.IsAny<IssuedOvpnFile>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DownloadOvpnFileByCn_WhenDbRowMissingThenFound_RetriesAndSucceeds()
    {
        var server = new VpnServer
        {
            Id = 75,
            ServerType = VpnServerType.OpenVpn,
            ServerName = "s75",
            ApiUrl = "https://vpn.test/",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };
        var file = new IssuedOvpnFile
        {
            Id = 1369,
            VpnServerId = 75,
            CommonName = "adg-75-local:232-test",
            FileName = "adg-75-local:232-test.ovpn",
            FilePath = "/pki/ovpn_files/adg-75-local:232-test.ovpn",
            ExternalId = "local:232",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };

        var serverQuery = new Mock<IVpnServerQueryService>(MockBehavior.Strict);
        serverQuery.Setup(q => q.GetById(75, It.IsAny<CancellationToken>())).ReturnsAsync(server);

        var fileQuery = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        fileQuery.SetupSequence(q => q.GetByCommonNameAndVpnServerIdAndIsRevoked(
                "adg-75-local:232-test", 75, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IssuedOvpnFile?)null)
            .ReturnsAsync(file);

        var ovpnClient = new Mock<IOvpnFileApiClient>(MockBehavior.Strict);
        ovpnClient.Setup(c => c.DownloadOvpnFile(
                75,
                It.Is<DownloadOvpnFileRequest>(r => r.CommonName == file.CommonName),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OvpnFileDownload
            {
                CommonName = file.CommonName,
                FileName = file.FileName,
                Content = new byte[] { 1, 2, 3 }
            });

        var notification = new Mock<IOvpnFileNotificationService>(MockBehavior.Strict);
        notification.Setup(n => n.NotifyDownloaded(
                75, file.FileName, file.ExternalId, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateService(
            ovpnClient: ovpnClient,
            fileQuery: fileQuery,
            serverQuery: serverQuery,
            notification: notification);

        var result = await sut.DownloadOvpnFileByCn(
            new DownloadFileByCnRequest { VpnServerId = 75, CommonName = "adg-75-local:232-test" },
            CancellationToken.None);

        result.Content.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        fileQuery.Verify(
            q => q.GetByCommonNameAndVpnServerIdAndIsRevoked(
                "adg-75-local:232-test", 75, false, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        ovpnClient.Verify(
            c => c.DownloadOvpnFile(75, It.IsAny<DownloadOvpnFileRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadOvpnFileByCn_WhenAllAttemptsFail_ThrowsAfterFiveDbLookups()
    {
        var server = new VpnServer
        {
            Id = 75,
            ServerType = VpnServerType.OpenVpn,
            ServerName = "s75",
            ApiUrl = "https://vpn.test/",
            CreateDate = DateTimeOffset.UtcNow,
            LastUpdate = DateTimeOffset.UtcNow
        };

        var serverQuery = new Mock<IVpnServerQueryService>(MockBehavior.Strict);
        serverQuery.Setup(q => q.GetById(75, It.IsAny<CancellationToken>())).ReturnsAsync(server);

        var fileQuery = new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Strict);
        fileQuery.Setup(q => q.GetByCommonNameAndVpnServerIdAndIsRevoked(
                "missing-cn", 75, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IssuedOvpnFile?)null);

        var sut = CreateService(fileQuery: fileQuery, serverQuery: serverQuery);
        var act = () => sut.DownloadOvpnFileByCn(
            new DownloadFileByCnRequest { VpnServerId = 75, CommonName = "missing-cn" },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Issued OVPN file not found*");
        fileQuery.Verify(
            q => q.GetByCommonNameAndVpnServerIdAndIsRevoked(
                "missing-cn", 75, false, It.IsAny<CancellationToken>()),
            Times.Exactly(5));
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

    private static OvpnFileApiService CreateService(
        Mock<IOvpnFileApiClient>? ovpnClient = null,
        Mock<IIssuedOvpnFileTokenQueryService>? tokenQuery = null,
        Mock<IIssuedOvpnFileQueryService>? fileQuery = null,
        Mock<IVpnServerQueryService>? serverQuery = null,
        Mock<IOvpnFileNotificationService>? notification = null,
        Mock<IUserIdentityLinkQueryService>? identityLinkQuery = null,
        Mock<IUserQuotaPlanQueryService>? userQuotaPlanQuery = null,
        Mock<IQuotaPlanAllowedServerQueryService>? quotaPlanAllowedServerQuery = null,
        Mock<IUserQueryService>? userQuery = null,
        Mock<IVpnServerOvpnFileConfigQueryService>? configQuery = null,
        Mock<ICommandService<IssuedOvpnFile, int>>? fileCommand = null)
    {
        ovpnClient ??= new Mock<IOvpnFileApiClient>(MockBehavior.Loose);
        var logger = Mock.Of<ILogger<OvpnFileApiService>>();
        var config = new ConfigurationBuilder().Build();
        configQuery ??= new Mock<IVpnServerOvpnFileConfigQueryService>(MockBehavior.Loose);
        serverQuery ??= new Mock<IVpnServerQueryService>(MockBehavior.Loose);
        fileCommand ??= new Mock<ICommandService<IssuedOvpnFile, int>>(MockBehavior.Loose);
        var tokenCommand = new Mock<ICommandService<IssuedOvpnFileToken, int>>(MockBehavior.Loose);

        tokenQuery ??= new Mock<IIssuedOvpnFileTokenQueryService>(MockBehavior.Loose);
        fileQuery ??= new Mock<IIssuedOvpnFileQueryService>(MockBehavior.Loose);
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

        return new OvpnFileApiService(
            ovpnClient.Object,
            logger,
            config,
            configQuery.Object,
            fileQuery.Object,
            tokenQuery.Object,
            identityLinkQuery.Object,
            accessGuard,
            fileCommand.Object,
            tokenCommand.Object,
            serverQuery.Object,
            notification.Object);
    }
}
