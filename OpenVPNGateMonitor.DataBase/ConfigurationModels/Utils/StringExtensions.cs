namespace OpenVPNGateMonitor.DataBase.ConfigurationModels.Utils;

public static class StringExtensions
{
    /// <summary>
    /// Normalizes all line endings to Unix-style (\n).
    /// </summary>
    /// <param name="input">The original string.</param>
    /// <returns>String with all line endings replaced by \n.</returns>
    public static string NormalizeUnixLineEndings(this string? input)
    {
        return (input ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
    }
}