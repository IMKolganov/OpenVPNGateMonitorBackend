namespace OpenVPNGateMonitor.Models.Helpers;

public class FrontendSettings
{
    public string FrontUrl { get; init; } = "http://localhost:5582";
    public int FrontPort { get; init; } = 3000;

}