namespace OpenVPNGateMonitor.Services.Interfaces;

public interface IOpenVpnParserService
{
    Task ParseAndSaveAsync();
}