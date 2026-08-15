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
    public async Task GetReminder_WhenDbTemplateExists_AppliesPlaceholdersFromDb()
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
        Assert.Contains("Open Telegram and link account", html);
    }

    [Fact]
    public async Task GetReminder_WhenNoDbTemplate_BuildsFromCode()
    {
        var query = new Mock<IQueryService<EmailBroadcastTemplate, int>>();
        query.Setup(q => q.FirstOrDefault(
                It.IsAny<Expression<Func<EmailBroadcastTemplate, bool>>>(),
                It.IsAny<Func<IQueryable<EmailBroadcastTemplate>, IOrderedQueryable<EmailBroadcastTemplate>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Expression<Func<EmailBroadcastTemplate, object>>[]>()))
            .ReturnsAsync((EmailBroadcastTemplate?)null);

        var sut = new SystemTransactionalEmailService(query.Object);
        var (subject, html) = await sut.GetFreeTierChannelSubscribeReminderAsync(
            "Ivan",
            "@datagateapp",
            "https://t.me/datagateapp",
            "ABCD2345",
            "https://t.me/DataGateVPNBot?start=link_ABCD2345",
            15,
            CancellationToken.None);

        Assert.Equal(TransactionalEmailHtml.DefaultFreeTierChannelSubscribeReminderSubject, subject);
        Assert.Contains("Ivan", html);
        Assert.Contains("https://t.me/DataGateVPNBot?start=link_ABCD2345", html);
    }
}
