﻿using Mapster;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers;
using OpenVPNGateMonitor.Models.Helpers.Services;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Responses;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnServerCerts.Mappings;

public class VpnServerCertificateMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CertificateBuildResult, VpnServerCertificateResponse>();

        config.NewConfig<OpenVpnServerCertConfig, ServerCertConfigResponse>();

        config.NewConfig<CertificateRevokeResult, RevokeCertificateResponse>()
            .Map(dest => dest.IsRevoked, src => src.IsRevoked)
            .Map(dest => dest.Message, src => src.Message)
            .Map(dest => dest.CertificatePath, src => src.CertificatePath);
        
        config.NewConfig<OpenVpnServerCertConfig, UpdateServerCertConfigResponse>()
            .Map(dest => dest.VpnServerId, src => src.VpnServerId)
            .Map(dest => dest.Success, _ => true)
            .Map(dest => dest.Message, _ => "Server certificate configuration updated successfully.");
        
        config.NewConfig<UpdateServerCertConfigRequest, OpenVpnServerCertConfigInfo>();//todo: check

    }
}