using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.DataBase.ConfigurationModels.Seeds;

public static class SettingSeedData
{ 
    public static readonly Setting[] Data =
    {
        new Setting
        {
            Id = 1,
            Key = "OpenVPN_Polling_Interval",
            ValueType = "int",
            IntValue = 120
        },
        new Setting
        {
            Id = 2, 
            Key = "OpenVPN_Polling_Interval_Type",
            ValueType = "string",
            StringValue = "int"
        },
        new Setting
        {
            Id = 3, 
            Key = "OpenVPN_Polling_Interval_Unit",
            ValueType = "string",
            StringValue = "seconds"
        },
        new Setting
        {
            Id = 4, 
            Key = "OpenVPN_Polling_Interval_Unit_Type",
            ValueType = "string",
            StringValue = "string"
        }
    };
}