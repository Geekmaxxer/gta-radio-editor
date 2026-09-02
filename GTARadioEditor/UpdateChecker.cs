using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GTARadioEditor;

internal sealed class UpdateCheckResult
{
    public required bool UpdateAvailable { get; init; }
    public required string CurrentVersion { get; init; }
    public required string LatestVersion { get; init; }
    public required string ReleaseUrl { get; init; }
}

internal static class UpdateChecker
{
    private const string Owner = "Geekmaxxer";
    private const string Repo = "gta-radio-editor";


    private static readonly string ApiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    public static readonly string LatestReleaseWebUrl = $"https://github.com/{Owner}/{Repo}/releases/latest";

    public static async Task<UpdateCheckResult?> CheckAsync()
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(6)
            };

            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GTARadioEditor", AppVersion.Current));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await client.GetAsync(ApiUrl).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(true);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(true);

            if (!doc.RootElement.TryGetProperty("tag_name", out var tagElement))
                return null;

            var latestTag = tagElement.GetString();
            if (string.IsNullOrWhiteSpace(latestTag))
                return null;

            var releaseUrl = LatestReleaseWebUrl;
            if (doc.RootElement.TryGetProperty("html_url", out var htmlUrlElement))
            {
                var htmlUrl = htmlUrlElement.GetString();
                if (!string.IsNullOrWhiteSpace(htmlUrl))
                    releaseUrl = htmlUrl;
            }

            return new UpdateCheckResult
            {
                UpdateAvailable = IsNewerVersion(AppVersion.Current, latestTag),
                CurrentVersion = AppVersion.Current,
                LatestVersion = NormalizeTag(latestTag),
                ReleaseUrl = releaseUrl
            };
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeTag(string tag)
    {
        tag = tag.Trim();
        return tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag[1..] : tag;
    }

    internal static bool IsNewerVersion(string currentRaw, string latestRaw)
    {
        var currentNormalized = NormalizeTag(currentRaw);
        var latestNormalized = NormalizeTag(latestRaw);

        var current = ParseVersion(currentNormalized);
        var latest = ParseVersion(latestNormalized);

        if (current is null || latest is null)
            return !string.Equals(currentNormalized, latestNormalized, StringComparison.OrdinalIgnoreCase);

        return latest > current;
    }

    private static readonly Regex VersionPattern = new(@"^\d+(\.\d+){0,3}", RegexOptions.Compiled);

    private static Version? ParseVersion(string value)
    {
        var match = VersionPattern.Match(value);
        if (!match.Success)
            return null;

        var text = match.Value;

        if (!text.Contains('.'))
            text += ".0";

        return Version.TryParse(text, out var version) ? version : null;
    }
}
