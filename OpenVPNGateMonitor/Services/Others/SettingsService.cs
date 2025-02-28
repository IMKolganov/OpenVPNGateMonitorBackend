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

        bool isNewSetting = false;

        if (setting == null)
        {
            setting = new Setting { Key = key };
            isNewSetting = true;
        }

        setting.ValueType = value switch
        {
            int => "int",
            bool => "bool",
            double => "double",
            DateTime => "datetime",
            string => "string",
            null => "null",
            _ => throw new ArgumentException($"Unsupported type {typeof(T).Name}")
        };

        setting.LastUpdate = DateTime.UtcNow;

        setting.IntValue = null;
        setting.BoolValue = null;
        setting.DoubleValue = null;
        setting.DateTimeValue = null;
        setting.StringValue = null;

        if (value is null)
        {
            setting.ValueType = "null";
        }
        else
        {
            switch (value)
            {
                case int intValue:
                    setting.IntValue = intValue;
                    break;
                case bool boolValue:
                    setting.BoolValue = boolValue;
                    break;
                case double doubleValue:
                    setting.DoubleValue = doubleValue;
                    break;
                case DateTime dateTimeValue:
                    setting.DateTimeValue = dateTimeValue;
                    break;
                case string stringValue:
                    setting.StringValue = stringValue;
                    break;
            }
        }

        if (isNewSetting)
        {
            await settingRepository.AddAsync(setting, cancellationToken);
        }
        else
        {
            settingRepository.Update(setting);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
