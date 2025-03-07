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
        },
        
        new Setting
        {
            Id = 5, 
            Key = "GeoIp_Download_Url",
            ValueType = "string",
            StringValue = "https://download.maxmind.com/app/geoip_download?edition_id=GeoLite2-City&license_key={LicenseKey}&suffix=tar.gz"
        },
        new Setting
        {
            Id = 6, 
            Key = "GeoIp_Download_Url_Type",
            ValueType = "string",
            StringValue = "string"
        },
        
        new Setting
        {
            Id = 7, 
            Key = "GeoIp_Db_Path",
            ValueType = "string",
            StringValue = "GeoLite2/GeoLite2-City.mmdb"
        },
        new Setting
        {
            Id = 8, 
            Key = "GeoIp_Db_Path_Type",
            ValueType = "string",
            StringValue = "string"
        },
        
        new Setting
        {
            Id = 9, 
            Key = "GeoIp_Account_ID",
            ValueType = "string",
            StringValue = "YOUR_ACCOUNT_ID"
        },
        new Setting
        {
            Id = 10, 
            Key = "GeoIp_Account_ID_Type",
            ValueType = "string",
            StringValue = "string"
        },
        
        new Setting
        {
            Id = 11, 
            Key = "GeoIp_License_Key",
            ValueType = "string",
            StringValue = "YOUR_LICENSE_KEY"
        },
        new Setting
        {
            Id = 12, 
            Key = "GeoIp_License_Key_Type",
            ValueType = "string",
            StringValue = "string"
        },
    };
}