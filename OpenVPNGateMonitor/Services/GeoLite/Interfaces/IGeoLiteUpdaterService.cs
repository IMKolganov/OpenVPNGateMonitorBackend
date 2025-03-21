﻿using OpenVPNGateMonitor.Models.Helpers.Api;

namespace OpenVPNGateMonitor.Services.GeoLite.Interfaces;

public interface IGeoLiteUpdaterService
{
    Task<GeoLiteUpdateResponse>  DownloadAndUpdateDatabaseAsync(CancellationToken cancellationToken);
}