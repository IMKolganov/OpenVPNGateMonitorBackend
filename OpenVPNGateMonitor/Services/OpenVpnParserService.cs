﻿using System.Security.Cryptography;
using System.Text;
using OpenVPNGateMonitor.DataBase.Contexts;
using OpenVPNGateMonitor.DataBase.UnitOfWork;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace OpenVPNGateMonitor.Services;

public class OpenVpnParserService : IOpenVpnParserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IOpenVpnParserService> _logger;
    private readonly string _statusFilePath;

    public OpenVpnParserService(IUnitOfWork unitOfWork, ILogger<IOpenVpnParserService> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _statusFilePath = configuration.GetSection("OpenVpn").Get<OpenVpnSettings>()?.StatusFilePath
                          ?? throw new InvalidOperationException("Failed to load OpenVpnSettings from configuration.");
    }

    public async Task ParseAndSaveAsync()//todo: needed check statistics maybe statistic is incorrect in DB
    {
        if (!File.Exists(_statusFilePath))
        {
            _logger.LogError("OpenVPN status file not found: {FilePath}", _statusFilePath);
            throw new FileNotFoundException($"OpenVPN status file not found: {_statusFilePath}");
        }

        _logger.LogInformation("Started reading the OpenVPN status file: {FilePath}", _statusFilePath);
        var users = ParseOpenVpnStatus(_statusFilePath);

        _logger.LogInformation("Found {UserCount} users in the status file.", users.Count);
        var openVpnUserStatisticRepository = _unitOfWork.GetRepository<OpenVpnUserStatistic>();

        foreach (var user in users)
        {
            var sessionId = GenerateSessionId(user.CommonName, user.RealAddress, user.ConnectedSince);
            
            var existingOpenVpnUserStatistic = await openVpnUserStatisticRepository.Query
                .FirstOrDefaultAsync(x => x.SessionId == sessionId);
            if (existingOpenVpnUserStatistic != null)
            {
                existingOpenVpnUserStatistic.BytesReceived = user.BytesReceived;
                existingOpenVpnUserStatistic.BytesSent = user.BytesSent;
                existingOpenVpnUserStatistic.LastUpdated = DateTime.UtcNow;
                openVpnUserStatisticRepository.Update(existingOpenVpnUserStatistic);
            }
            else
            {
                await openVpnUserStatisticRepository.AddAsync(new OpenVpnUserStatistic
                {
                    SessionId = sessionId,
                    CommonName = user.CommonName,
                    RealAddress = user.RealAddress,
                    BytesReceived = user.BytesReceived,
                    BytesSent = user.BytesSent,
                    ConnectedSince = user.ConnectedSince,
                    LastUpdated = DateTime.UtcNow
                });
            }
        }

        
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Data successfully saved to the database.");
    }

    private Guid GenerateSessionId(string commonName, string realAddress, DateTime connectedSince)
    {
        var sessionString = $"{commonName}-{realAddress}-{connectedSince:o}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sessionString));
        return new Guid(hashBytes.Take(16).ToArray());
    }

    private List<OpenVpnUserStatistic> ParseOpenVpnStatus(string filePath)
    {
        var users = new List<OpenVpnUserStatistic>();

        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            _logger.LogInformation($"Processing line: {line}");

            if (line.StartsWith("CLIENT_LIST"))
            {
                var parts = line.Split('\t');
                _logger.LogInformation($"Split parts: {string.Join(" | ", parts)}");

                if (parts.Length >= 13)
                {
                    try
                    {
                        var connectedSince =
                            DateTime.SpecifyKind(DateTime.Parse(parts[7]), DateTimeKind.Utc);

                        users.Add(new OpenVpnUserStatistic
                        {
                            CommonName = parts[1],
                            RealAddress = parts[2],
                            BytesReceived = long.Parse(parts[5]),
                            BytesSent = long.Parse(parts[6]),
                            ConnectedSince = connectedSince
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error parsing line: {line}");
                        _logger.LogError($"Exception: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Skipping malformed line: {line}");
                }
            }
        }

        return users;
    }

}
