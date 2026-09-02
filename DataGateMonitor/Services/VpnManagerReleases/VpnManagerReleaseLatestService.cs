using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DataGateMonitor.Services.Helpers;
using DataGateMonitor.SharedModels.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DataGateMonitor.Services.VpnManagerReleases;

public sealed class VpnManagerReleaseLatestService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<VpnManagerReleasesOptions> options,
    ILogger<VpnManagerReleaseLatestService> logger) : IVpnManagerReleaseLatestService
{
    public const string HttpClientName = "VpnManagerGitHubReleases";

    public async Task<VpnManagerReleaseLatestInfo?> GetLatestAsync(
        VpnServerType serverType,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var repo = opts.Resolve(serverType);
        if (repo is null
            || string.IsNullOrWhiteSpace(repo.Owner)
            || string.IsNullOrWhiteSpace(repo.Repo))
        {
            return null;
        }

        var cacheKey = $"vpn-manager-release:latest:{serverType}:{repo.Owner}/{repo.Repo}";
        if (memoryCache.TryGetValue(cacheKey, out VpnManagerReleaseLatestInfo? cached) && cached is not null)
            return cached;

        var cacheMinutes = Math.Clamp(opts.CacheMinutes, 1, 24 * 60);
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"repos/{repo.Owner.Trim()}/{repo.Repo.Trim()}/releases/latest");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "GitHub latest release lookup failed for {Owner}/{Repo}: {StatusCode}",
                    repo.Owner, repo.Repo, (int)response.StatusCode);
                // Negative cache briefly to avoid hammering on outages.
                memoryCache.Set(cacheKey, (VpnManagerReleaseLatestInfo?)null, TimeSpan.FromMinutes(5));
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GitHubLatestReleaseDto>(cancellationToken);
            var version = NormalizeVersionTag(payload?.TagName);
            if (string.IsNullOrWhiteSpace(version))
            {
                logger.LogWarning(
                    "GitHub latest release for {Owner}/{Repo} had no usable tag_name.",
                    repo.Owner, repo.Repo);
                memoryCache.Set(cacheKey, (VpnManagerReleaseLatestInfo?)null, TimeSpan.FromMinutes(5));
                return null;
            }

            var info = new VpnManagerReleaseLatestInfo
            {
                ServerType = serverType,
                Version = version,
                ReleaseUrl = string.IsNullOrWhiteSpace(payload?.HtmlUrl) ? null : payload!.HtmlUrl.Trim(),
            };
            memoryCache.Set(cacheKey, info, TimeSpan.FromMinutes(cacheMinutes));
            return info;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "GitHub latest release lookup threw for {Owner}/{Repo}",
                repo.Owner, repo.Repo);
            memoryCache.Set(cacheKey, (VpnManagerReleaseLatestInfo?)null, TimeSpan.FromMinutes(5));
            return null;
        }
    }

    /// <summary>Strips a leading <c>v</c>/<c>V</c> from release tags.</summary>
    public static string? NormalizeVersionTag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var tag = raw.Trim();
        if (tag.Length > 1 && (tag[0] is 'v' or 'V') && char.IsDigit(tag[1]))
            tag = tag[1..];

        return string.IsNullOrWhiteSpace(tag) ? null : tag;
    }

    public static bool IsUpdateAvailable(string? installedVersion, string? latestVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion) || string.IsNullOrWhiteSpace(latestVersion))
            return false;

        var installed = NormalizeVersionTag(installedVersion);
        var latest = NormalizeVersionTag(latestVersion);
        if (installed is null || latest is null)
            return false;

        return DotVersionComparer.Compare(latest, installed) > 0;
    }

    private sealed class GitHubLatestReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }
}
