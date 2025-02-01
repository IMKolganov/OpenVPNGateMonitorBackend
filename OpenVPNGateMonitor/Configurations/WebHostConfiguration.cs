﻿using System.Security.Cryptography.X509Certificates;
using OpenVPNGateMonitor.Models.Helpers;

namespace OpenVPNGateMonitor.Configurations;

public static class WebHostConfiguration
{
    public static void ConfigureWebHost(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel((context, options) =>
        {
            options.Configure(context.Configuration.GetSection("Kestrel"));
        });
    }
}