using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels.Seeds;

public static class SettingSeedData
{
    public static readonly Setting[] Data =
    {
        new Setting
        {
            Key = "OpenVPN_Polling_Interval",
            ValueType = "int",
            IntValue = 30,
            LastUpdate = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow
        },
        new Setting
        {
            Key = "OpenVPN_Polling_Interval_Type",
            ValueType = "string",
            StringValue = "int",
            LastUpdate = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow
        },
        new Setting
        {
            Key = "OpenVPN_Polling_Interval_Unit",
            ValueType = "string",
            StringValue = "seconds",
            LastUpdate = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow
        },
        new Setting
        {
            Key = "OpenVPN_Polling_Interval_Unit_Type",
            ValueType = "string",
            StringValue = "string",
            LastUpdate = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow
        }
    };
}