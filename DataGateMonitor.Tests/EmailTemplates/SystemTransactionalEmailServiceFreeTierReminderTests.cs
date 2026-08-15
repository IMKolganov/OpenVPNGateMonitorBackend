using System.Linq.Expressions;
using DataGateMonitor.DataBase.Services.Query;
using DataGateMonitor.Models;
using DataGateMonitor.Models.EmailTemplates;
using DataGateMonitor.Services.EmailTemplates;
using Moq;

namespace DataGateMonitor.Tests.EmailTemplates;

public class SystemTransactionalEmailServiceFreeTierReminderTests
{
    [Fact]
    public void IsModernTemplate_True_ForCurrentPlaceholderBody()
    {
        var body = TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminderWithPlaceholders();
        Assert.True(SystemTransactionalEmailService.IsModernFreeTierChannelSubscribeReminderTemplate(body));
    }

    [Fact]
    public void IsModernTemplate_False_ForLegacyChannelOnlyBody()
    {
        const string legacy =
            """
            <p>Hello {{DISPLAY_NAME}},</p>
            <p>please subscribe to our official Telegram channel.</p>
            <p>Channel: {{REQUIRED_CHANNEL}}</p>
            <a href="{{CHANNEL_URL}}">Open Telegram channel</a>
            """;

        Assert.False(SystemTransactionalEmailService.IsModernFreeTierChannelSubscribeReminderTemplate(legacy));
    }

    [Fact]
    public async Task GetReminder_WhenDbTemplateIsLegacy_BuildsCodeTemplateWithDeepLink()
    {
        var query = new Mock<IQueryService<EmailBroadcastTemplate, int>>();
        query.Setup(q => q.FirstOrDefault(
                It.IsAny<Expression<Func<EmailBroadcastTemplate, bool>>>(),
                It.IsAny<Func<IQueryable<EmailBroadcastTemplate>, IOrderedQueryable<EmailBroadcastTemplate>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Expression<Func<EmailBroadcastTemplate, object>>[]>()))
            .ReturnsAsync(new EmailBroadcastTemplate
            {
                Name = SystemEmailTemplateNames.FreeTierChannelSubscribeReminder,
                Subject = "Please subscribe to our Telegram channel — DataGate",
                BodyHtml =
                    """
                    <p class="email-title">Please subscribe to our channel</p>
                    <p class="email-tagline">Free/Default VPN access requires an active Telegram channel subscription.</p>
                    <p class="email-lead">Hello {{DISPLAY_NAME}},</p>
                    <p class="email-body">To keep using Free/Default VPN access, please subscribe to our official Telegram channel.</p>
                    <p class="email-body">After you subscribe, reconnect to the VPN if you were disconnected.</p>
                    <a href="{{CHANNEL_URL}}">Open Telegram channel</a>
                    <div>{{REQUIRED_CHANNEL}}</div>
                    """,
            });

        var sut = new SystemTransactionalEmailService(query.Object);
        var (subject, html) = await sut.GetFreeTierChannelSubscribeReminderAsync(
            "Ivan Kolganov",
            "@datagateapp",
            "https://t.me/datagateapp",
            "ABCD2345",
            "https://t.me/DataGateVPNBot?start=link_ABCD2345",
            15,
            CancellationToken.None);

        Assert.Equal(TransactionalEmailHtml.DefaultFreeTierChannelSubscribeReminderSubject, subject);
        Assert.Contains("Subscribe to our channel and link Telegram", html);
        Assert.Contains("https://t.me/DataGateVPNBot?start=link_ABCD2345", html);
        Assert.Contains("ABCD2345", html);
        Assert.Contains("Open Telegram and link account", html);
        Assert.DoesNotContain("After you subscribe, reconnect to the VPN if you were disconnected.", html);
    }

    [Fact]
    public async Task GetReminder_WhenDbTemplateIsModern_AppliesPlaceholders()
    {
        var modern = TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminderWithPlaceholders();
        var query = new Mock<IQueryService<EmailBroadcastTemplate, int>>();
        query.Setup(q => q.FirstOrDefault(
                It.IsAny<Expression<Func<EmailBroadcastTemplate, bool>>>(),
                It.IsAny<Func<IQueryable<EmailBroadcastTemplate>, IOrderedQueryable<EmailBroadcastTemplate>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Expression<Func<EmailBroadcastTemplate, object>>[]>()))
            .ReturnsAsync(new EmailBroadcastTemplate
            {
                Name = SystemEmailTemplateNames.FreeTierChannelSubscribeReminder,
                Subject = "Custom subject",
                BodyHtml = modern,
            });

        var sut = new SystemTransactionalEmailService(query.Object);
        var (subject, html) = await sut.GetFreeTierChannelSubscribeReminderAsync(
            "Alice",
            "@chan",
            "https://t.me/chan",
            "ABCD2345",
            "https://t.me/Bot?start=link_ABCD2345",
            15,
            CancellationToken.None);

        Assert.Equal("Custom subject", subject);
        Assert.Contains("Alice", html);
        Assert.Contains("ABCD2345", html);
        Assert.Contains("https://t.me/Bot?start=link_ABCD2345", html);
    }
}
