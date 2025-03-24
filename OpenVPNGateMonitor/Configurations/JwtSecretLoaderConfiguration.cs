using System.Security.Cryptography;
using ILogger = Serilog.ILogger;

namespace OpenVPNGateMonitor.Configurations;

public static class JwtSecretLoaderConfiguration
{
    private const string DockerSecretPath = "/app/secrets/jwt-secret.txt";
    private const string LocalSecretPath = "secrets/jwt-secret.txt";

    public static string LoadOrGenerateSecret(ILogger logger)
    {
        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

        logger.Information("DOTNET_RUNNING_IN_CONTAINER = {IsDocker}", isDocker);

        var relativePath = isDocker ? DockerSecretPath : LocalSecretPath;
        var fullPath = Path.GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        string jwtSecret;

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_SECRET")))
        {
            jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")!;
            File.WriteAllText(fullPath, jwtSecret);
            logger.Information("JWT secret loaded from environment and saved to file at: {Path}", fullPath);
        }
        else if (File.Exists(fullPath))
        {
            jwtSecret = File.ReadAllText(fullPath);
            logger.Information("JWT secret loaded from file at: {Path}", fullPath);
        }
        else
        {
            jwtSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
            File.WriteAllText(fullPath, jwtSecret);
            logger.Information("JWT secret generated and saved to file at: {Path}", fullPath);
        }

        return jwtSecret;
    }
}