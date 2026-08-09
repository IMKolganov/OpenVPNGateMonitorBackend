using DataGateMonitor.Services.TelegramBot;
using DataGateMonitor.Services.TelegramBot.Interfaces;

namespace DataGateMonitor.Tests.Services.TelegramBot;

public class TelegramAdminAlertServiceTests
{
    [Fact]
    public void BuildAccountsLinkedCaption_IncludesTelegramAndLinkedDetails()
    {
        var text = TelegramAdminAlertService.BuildAccountsLinkedCaption(new TelegramAccountsLinkedAlert
        {
            TelegramId = 1067153884,
            TelegramUsername = "TATYANA_VI",
            TelegramDisplayName = "Tatyana",
            LinkedProviderLabel = "Google",
            LinkedDisplayName = "Tatyana Google",
            LinkedEmail = "tatyana@gmail.com",
            LinkedExternalId = "google-sub",
            SurvivorUserId = 10,
            MergedUserId = 20,
        });

        Assert.Contains("Accounts linked", text);
        Assert.Contains("1067153884", text);
        Assert.Contains("@TATYANA_VI", text);
        Assert.Contains("Google", text);
        Assert.Contains("tatyana@gmail.com", text);
        Assert.Contains("Survivor user #: 10", text);
        Assert.Contains("Merged user #: 20", text);
    }
}
