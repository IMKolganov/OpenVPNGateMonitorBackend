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
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.VpnServerId, src => src.VpnServerId)
            .Map(dest => dest.ExternalId, src => src.ExternalId)
            .Map(dest => dest.CommonName, src => src.CommonName)
            .Map(dest => dest.CertId, src => src.CertId)
            .Map(dest => dest.FileName, src => src.FileName)
            .Map(dest => dest.FilePath, src => src.FilePath)
            .Map(dest => dest.IssuedAt, src => src.IssuedAt)
            .Map(dest => dest.IssuedTo, src => src.IssuedTo)
            .Map(dest => dest.PemFilePath, src => src.PemFilePath)
            .Map(dest => dest.CertFilePath, src => src.CertFilePath)
            .Map(dest => dest.KeyFilePath, src => src.KeyFilePath)
            .Map(dest => dest.ReqFilePath, src => src.ReqFilePath)
            .Map(dest => dest.IsRevoked, src => src.IsRevoked)
            .Map(dest => dest.Message, src => src.Message)
            .Map(dest => dest.LastUpdate, src => src.LastUpdate)
            .Map(dest => dest.CreateDate, src => src.CreateDate);


        config.NewConfig<RevokeOvpnFileRequest, IssuedOvpnFile>()
            .Map(dest => dest.VpnServerId, src => src.VpnServerId)
            .Map(dest => dest.CommonName, src => src.CommonName);
    }
}