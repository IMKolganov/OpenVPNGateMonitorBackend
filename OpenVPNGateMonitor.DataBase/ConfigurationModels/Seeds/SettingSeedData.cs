using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels.Seeds;

public static class SettingSeedData
{
    public static readonly Setting[] Data =
    {
        new Setting
        {
            Id = -1,
            Key = "OpenVPN_Polling_Interval",
            ValueType = "int",
            IntValue = 120,
            LastUpdate = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow
        },
        new Setting
        {
            Id = -2,
            Key = "OpenVPN_Polling_Interval_Type",
            ValueType = "string",
            StringValue = "int",
            LastUpdate = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow
        },
        new Setting
        {
            Id = -3,
            Key = "OpenVPN_Polling_Interval_Unit",
            ValueType = "string",
            StringValue = "seconds",
            LastUpdate = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow
        },
        new Setting
        {
            Id = -4,
            Key = "OpenVPN_Polling_Interval_Unit_Type",
            ValueType = "string",
            StringValue = "string",
            LastUpdate = DateTime.UtcNow,
            CreateDate = DateTime.UtcNow
        }
    };
}