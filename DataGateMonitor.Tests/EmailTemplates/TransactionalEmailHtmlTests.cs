using DataGateMonitor.Models.EmailTemplates;
using Xunit;

namespace DataGateMonitor.Tests.EmailTemplates;

public class TransactionalEmailHtmlTests
{
    [Fact]
    public void EnsureConfirmEmailAction_WhenUrlAlreadyPresent_ReturnsUnchanged()
    {
        var html = TransactionalEmailHtml.BuildEmailConfirmationWithPlaceholders();

        var result = TransactionalEmailHtml.EnsureConfirmEmailAction(html);

        Assert.Equal(html, result);
        Assert.Contains(TransactionalEmailHtml.DefaultConfirmEmailPageUrl, result);
    }

    [Fact]
    public void EnsureConfirmEmailAction_WhenMissing_InsertsButtonBeforeCodePanel()
    {
        const string templateWithoutButton =
            """
            <div class="code-panel" style="margin:18px 0 12px;">
              <div>{{CODE}}</div>
            </div>
            """;

        var result = TransactionalEmailHtml.EnsureConfirmEmailAction(templateWithoutButton);

        Assert.Contains(TransactionalEmailHtml.DefaultConfirmEmailPageUrl, result);
        Assert.Contains("Enter confirmation code", result);
        Assert.StartsWith(
            """
            <div class="btn-wrap">
            """,
            result[..result.IndexOf("<div class=\"code-panel\"", StringComparison.Ordinal)]);
    }

    [Fact]
    public void EnsureConfirmEmailAction_WhenNoCodePanel_ReturnsUnchanged()
    {
        const string html = "<p>No panel here</p>";

        var result = TransactionalEmailHtml.EnsureConfirmEmailAction(html);

        Assert.Equal(html, result);
    }

    [Fact]
    public void BuildFreeTierGraceDisconnected_IncludesPlanAndChannel()
    {
        var html = TransactionalEmailHtml.BuildFreeTierGraceDisconnected("Free", "@DataGateVPNBot");

        Assert.Contains("Free", html);
        Assert.Contains("@DataGateVPNBot", html);
    }

    [Fact]
    public void ApplyFreeTierGraceDisconnectedPlaceholders_ReplacesBothTokens()
    {
        var template = TransactionalEmailHtml.BuildFreeTierGraceDisconnectedWithPlaceholders();

        var result = TransactionalEmailHtml.ApplyFreeTierGraceDisconnectedPlaceholders(template, "Default", "@DataGateVPNBot");

        Assert.DoesNotContain("{{PLAN_NAME}}", result);
        Assert.DoesNotContain("{{REQUIRED_CHANNEL}}", result);
        Assert.Contains("Default", result);
        Assert.Contains("@DataGateVPNBot", result);
    }

    [Fact]
    public void BuildFreeTierChannelSubscribeReminder_IncludesNameChannelAndUrl()
    {
        var html = TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminder(
            "Tatyana", "@datagateapp", "https://t.me/datagateapp");

        Assert.Contains("Tatyana", html);
        Assert.Contains("@datagateapp", html);
        Assert.Contains("https://t.me/datagateapp", html);
        Assert.Contains("Google", html);
    }

    [Fact]
    public void BuildFreeTierChannelSubscribeReminder_WithLinkCode_IncludesDeepLink()
    {
        var html = TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminder(
            "Tatyana",
            "@datagateapp",
            "https://t.me/datagateapp",
            "ABCD2345",
            "https://t.me/DataGateVPNBot?start=link_ABCD2345",
            15);

        Assert.Contains("ABCD2345", html);
        Assert.Contains("https://t.me/DataGateVPNBot?start=link_ABCD2345", html);
        Assert.Contains("Open Telegram and link account", html);
        Assert.Contains("/link_account ABCD2345", html);
    }

    [Fact]
    public void ApplyFreeTierChannelSubscribeReminderPlaceholders_ReplacesTokens()
    {
        var template = TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminderWithPlaceholders();

        var result = TransactionalEmailHtml.ApplyFreeTierChannelSubscribeReminderPlaceholders(
            template, "Alice", "@chan", "https://t.me/chan", "ABCD2345",
            "https://t.me/Bot?start=link_ABCD2345", 15);

        Assert.DoesNotContain("{{DISPLAY_NAME}}", result);
        Assert.DoesNotContain("{{REQUIRED_CHANNEL}}", result);
        Assert.DoesNotContain("{{CHANNEL_URL}}", result);
        Assert.DoesNotContain("{{LINK_CODE}}", result);
        Assert.DoesNotContain("<!--BEGIN_LINK_ACCOUNT-->", result);
        Assert.Contains("Alice", result);
        Assert.Contains("@chan", result);
        Assert.Contains("https://t.me/chan", result);
        Assert.Contains("ABCD2345", result);
        Assert.Contains("https://t.me/Bot?start=link_ABCD2345", result);
    }

    [Fact]
    public void ApplyFreeTierChannelSubscribeReminderPlaceholders_WithoutLink_RemovesLinkBlock()
    {
        var template = TransactionalEmailHtml.BuildFreeTierChannelSubscribeReminderWithPlaceholders();

        var result = TransactionalEmailHtml.ApplyFreeTierChannelSubscribeReminderPlaceholders(
            template, "Alice", "@chan", "https://t.me/chan");

        Assert.DoesNotContain("<!--BEGIN_LINK_ACCOUNT-->", result);
        Assert.DoesNotContain("{{LINK_CODE}}", result);
        Assert.Contains("Open Telegram channel", result);
        Assert.DoesNotContain("Open Telegram and link account", result);
    }
}
