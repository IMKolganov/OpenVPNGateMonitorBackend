using Mapster;
using OpenVPNGateMonitor.Models;
using OpenVPNGateMonitor.Models.Helpers.Services;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Requests;
using OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Responses;

namespace OpenVPNGateMonitor.SharedModels.OpenVpnFiles.Mappings;

public class OvpnFileMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<OvpnFileResult, DownloadOvpnFileResponse>()
            .MapWith(src => new DownloadOvpnFileResponse
            {
                FileStream = src.FileStream!,
                FileName = src.FileName
            });

        config.NewConfig<IssuedOvpnFile, OvpnFileResponse>()
            .Map(dest => dest.ServerId, src => src.ServerId)
            .Map(dest => dest.ExternalId, src => src.ExternalId)
            .Map(dest => dest.CommonName, src => src.CommonName)
            .Map(dest => dest.FileName, src => src.FileName)
            .Map(dest => dest.IssuedAt, src => src.IssuedAt)
            .Map(dest => dest.IssuedTo, src => src.IssuedTo)
            .Map(dest => dest.IsRevoked, src => src.IsRevoked)
            .Map(dest => dest.Message, src => src.Message);
        
        config.NewConfig<RevokeOvpnFileRequest, IssuedOvpnFile>()
            .Map(dest => dest.ServerId, src => src.ServerId)
            .Map(dest => dest.CommonName, src => src.CommonName)
            .Map(dest => dest.ExternalId, src => src.ExternalId);
    }
}