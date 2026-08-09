using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DataGateMonitor.Models.Helpers;
using DataGateMonitor.Services.TelegramBot;

namespace DataGateMonitor.Tests.Services.TelegramBot;

public class TelegramDirectMessageSenderTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();

    private TelegramDirectMessageSender CreateSut(string? botToken = "test-token")
        => new(
            _httpClientFactory.Object,
            Options.Create(new TelegramChannelSettings { BotToken = botToken }),
            Mock.Of<ILogger<TelegramDirectMessageSender>>());

    private void SetupHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var client = new HttpClient(new FakeHttpMessageHandler(handler));
        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
    }

    [Fact]
    public async Task TrySendMessageAsync_WhenBotTokenMissing_ReturnsFalse()
    {
        var sut = CreateSut(botToken: null);

        var result = await sut.TrySendMessageAsync(123, "hello", CancellationToken.None);

        Assert.False(result);
        _httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TrySendMessageAsync_WhenChatIdInvalid_ReturnsFalse()
    {
        var sut = CreateSut();

        var result = await sut.TrySendMessageAsync(0, "hello", CancellationToken.None);

        Assert.False(result);
        _httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TrySendMessageAsync_WhenTelegramReturnsOk_ReturnsTrue()
    {
        SetupHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"result":{}}"""),
        }));

        var sut = CreateSut();
        var result = await sut.TrySendMessageAsync(123, "hello", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TrySendMessageAsync_WhenTelegramReturnsNotOk_ReturnsFalse()
    {
        SetupHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"ok":false,"description":"Forbidden: bot was blocked by the user"}"""),
        }));

        var sut = CreateSut();
        var result = await sut.TrySendMessageAsync(123, "hello", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task TrySendPhotoAsync_WhenPhotoMissing_FallsBackToText()
    {
        SetupHttpClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"result":{}}"""),
        }));

        var sut = CreateSut();
        var result = await sut.TrySendPhotoAsync(123, null, "caption only", ct: CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TrySendPhotoAsync_WhenTelegramAcceptsPhoto_ReturnsTrue()
    {
        SetupHttpClient(req =>
        {
            Assert.Contains("sendPhoto", req.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"result":{}}"""),
            });
        });

        var sut = CreateSut();
        var result = await sut.TrySendPhotoAsync(
            123,
            [0xFF, 0xD8, 0xFF],
            "with photo",
            ct: CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TrySendMessageAsync_WhenHttpThrows_ReturnsFalseWithoutThrowing()
    {
        SetupHttpClient(_ => throw new HttpRequestException("network down"));

        var sut = CreateSut();
        var result = await sut.TrySendMessageAsync(123, "hello", CancellationToken.None);

        Assert.False(result);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
