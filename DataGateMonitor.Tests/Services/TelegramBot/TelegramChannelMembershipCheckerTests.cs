using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.TelegramBot;

namespace DataGateMonitor.Tests.Services.TelegramBot;

public class TelegramChannelMembershipCheckerTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly Mock<ILogger<TelegramChannelMembershipChecker>> _logger = new();

    private TelegramChannelMembershipChecker CreateSut(string? botToken = "test-token")
        => new(
            _httpClientFactory.Object,
            Options.Create(new TelegramChannelSettings
            {
                BotToken = botToken,
                RequiredChannelUsername = "datagateapp",
            }),
            _logger.Object);

    private void SetupHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var client = new HttpClient(new FakeHttpMessageHandler(handler));
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
    }

    [Theory]
    [InlineData("Bad Request: PARTICIPANT_ID_INVALID", true)]
    [InlineData("Bad Request: user not found", true)]
    [InlineData("Bad Request: member not found", true)]
    [InlineData("Forbidden: bot is not a member of the channel chat", false)]
    [InlineData("Unauthorized", false)]
    public void IsExpectedNonMembershipDescription_ClassifiesTelegramErrors(string description, bool expected)
        => Assert.Equal(expected, TelegramChannelMembershipChecker.IsExpectedNonMembershipDescription(description));

    [Fact]
    public async Task IsSubscribedAsync_WhenParticipantIdInvalid_ReturnsFalseAndLogsDebug()
    {
        SetupHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"ok":false,"description":"Bad Request: PARTICIPANT_ID_INVALID"}"""),
        }));

        var sut = CreateSut();
        var result = await sut.IsSubscribedAsync(7848353335, CancellationToken.None);

        Assert.False(result);
        _logger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("not a member", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task IsSubscribedAsync_WhenBotForbidden_ReturnsFalseAndLogsWarning()
    {
        SetupHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                """{"ok":false,"description":"Forbidden: bot is not a member of the channel chat"}"""),
        }));

        var sut = CreateSut();
        var result = await sut.IsSubscribedAsync(123, CancellationToken.None);

        Assert.False(result);
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("getChatMember failed", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IsSubscribedAsync_WhenMember_ReturnsTrue()
    {
        SetupHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"result":{"status":"member"}}"""),
        }));

        var sut = CreateSut();
        var result = await sut.IsSubscribedAsync(123, CancellationToken.None);

        Assert.True(result);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
