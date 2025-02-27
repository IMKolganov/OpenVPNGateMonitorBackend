using Microsoft.EntityFrameworkCore;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;

namespace OpenVPNGateMonitor.Services.Others;

public class SettingsService : ISettingsService
{
    private readonly IUnitOfWork _unitOfWork;

    public SettingsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<T?> GetValueAsync<T>(string key, CancellationToken cancellationToken)
    {
        var setting = await _unitOfWork.GetQuery<Setting>()
            .AsQueryable()
            .Where(x => x.Key == key)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting == null)
            return default;

        if (setting.ValueType == "null")
            return default;

        return setting.ValueType switch
        {
            "int" => setting.IntValue is { } intValue ? (T)(object)intValue : default,
            "bool" => setting.BoolValue is { } boolValue ? (T)(object)boolValue : default,
            "double" => setting.DoubleValue is { } doubleValue ? (T)(object)doubleValue : default,
            "datetime" => setting.DateTimeValue is { } dateTimeValue ? (T)(object)dateTimeValue : default,
            "string" => setting.StringValue is { } stringValue ? (T)(object)stringValue : default,
            _ => default
        };
    }

    public async Task SetValueAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        var settingRepository = _unitOfWork.GetRepository<Setting>();
        var setting = await settingRepository.Query
            .Where(x => x.Key == key)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting == null)
        {
            setting = new Setting { Key = key };
            await settingRepository.AddAsync(setting, cancellationToken);
        }

        setting.ValueType = typeof(T) switch
        {
            Type t when t == typeof(int) => "int",
            Type t when t == typeof(bool) => "bool",
            Type t when t == typeof(double) => "double",
            Type t when t == typeof(DateTime) => "datetime",
            Type t when t == typeof(string) => "string",
            _ => throw new ArgumentException("Unsupported type")
        };

        setting.LastUpdate = DateTime.UtcNow;

        if (value == null)
        {
            setting.StringValue = null;
            setting.IntValue = null;
            setting.BoolValue = null;
            setting.DoubleValue = null;
            setting.DateTimeValue = null;
            setting.ValueType = "null";
        }
        else
        {
            switch (value)
            {
                case int intValue:
                    setting.IntValue = intValue;
                    setting.StringValue = null;
                    setting.BoolValue = null;
                    setting.DoubleValue = null;
                    setting.DateTimeValue = null;
                    break;
                case bool boolValue:
                    setting.BoolValue = boolValue;
                    setting.StringValue = null;
                    setting.IntValue = null;
                    setting.DoubleValue = null;
                    setting.DateTimeValue = null;
                    break;
                case double doubleValue:
                    setting.DoubleValue = doubleValue;
                    setting.StringValue = null;
                    setting.IntValue = null;
                    setting.BoolValue = null;
                    setting.DateTimeValue = null;
                    break;
                case DateTime dateTimeValue:
                    setting.DateTimeValue = dateTimeValue;
                    setting.StringValue = null;
                    setting.IntValue = null;
                    setting.BoolValue = null;
                    setting.DoubleValue = null;
                    break;
                case string stringValue:
                    setting.StringValue = stringValue;
                    setting.IntValue = null;
                    setting.BoolValue = null;
                    setting.DoubleValue = null;
                    setting.DateTimeValue = null;
                    break;
                default:
                    throw new ArgumentException("Unsupported type");
            }
        }

        settingRepository.Update(setting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
