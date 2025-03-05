using System.IO.Compression;
using System.Text;

namespace OpenVPNGateMonitor.Services.Untils;

public static class GZip
{
    public static void ExtractTarGz(string filePath, string outputDir)
    {
        using (var stream = File.OpenRead(filePath))
        using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
        using (var tarStream = new MemoryStream())
        {
            gzipStream.CopyTo(tarStream);
            tarStream.Position = 0;
            ExtractTar(tarStream, outputDir);
        }
    }

    private static void ExtractTar(Stream tarStream, string outputDir)
    {
        using (var reader = new BinaryReader(tarStream))
        {
            while (tarStream.Position < tarStream.Length)
            {
                var header = reader.ReadBytes(512);
                if (header.All(b => b == 0)) break;

                string name = Encoding.ASCII.GetString(header, 0, 100).Trim('\0');
                if (string.IsNullOrWhiteSpace(name)) break;

                byte typeFlag = header[156];
                if (typeFlag == 49)
                {
                    continue;
                }

                string sizeString = Encoding.ASCII.GetString(header, 124, 12).Trim('\0').Trim();
                long size = Convert.ToInt64(sizeString, 8);

                string outputFile = Path.GetFullPath(Path.Combine(outputDir, name.Replace('/', Path.DirectorySeparatorChar)));

                if (!outputFile.StartsWith(Path.GetFullPath(outputDir)))
                {
                    throw new InvalidOperationException($"Invalid tar path detected: {name}");
                }

                if (name.EndsWith("/") || name.EndsWith("\\"))
                {
                    Directory.CreateDirectory(outputFile);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputFile) ?? throw new InvalidOperationException());

                using (var output = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[512];
                    long remaining = size;
                    while (remaining > 0)
                    {
                        int read = tarStream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        output.Write(buffer, 0, read);
                        remaining -= read;
                    }
                }

                long padding = (512 - (size % 512)) % 512;
                tarStream.Seek(padding, SeekOrigin.Current);
            }
        }
    }
}
