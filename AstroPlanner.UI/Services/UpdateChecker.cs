using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AstroPlanner.UI.Services;

/// <summary>Una versión publicada más reciente que la instalada.</summary>
public sealed record UpdateInfo(AppVersion Version, string Name, string Url);

/// <summary>Comprueba si hay una versión nueva publicada.</summary>
public interface IUpdateChecker
{
    /// <summary>
    /// La versión publicada más alta si es mayor que <paramref name="current"/>;
    /// null si no hay ninguna o si no se pudo consultar (nunca lanza por red).
    /// </summary>
    Task<UpdateInfo?> CheckAsync(AppVersion current, CancellationToken cancellationToken = default);
}

/// <summary>
/// Consulta las releases de GitHub (se ignoran los borradores). Solo funciona si
/// el repositorio es público: con uno privado la API responde 404 y simplemente
/// no se avisa de nada.
/// </summary>
public sealed class GitHubUpdateChecker(HttpClient http, IAppLogger logger) : IUpdateChecker
{
    public const string ReleasesUrl = "https://api.github.com/repos/Yamato6/AstroPlanner/releases?per_page=20";

    public async Task<UpdateInfo?> CheckAsync(AppVersion current, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.Info($"Búsqueda de actualizaciones: GitHub respondió {(int)response.StatusCode}.");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, cancellationToken: cancellationToken);
            return Newest(releases ?? [], current);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.Info($"Búsqueda de actualizaciones no disponible: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// La release más alta que supera a <paramref name="current"/>. Las pre-release
    /// (sufijo como "-rc.1" o marcadas así en GitHub) solo se ofrecen a quien usa
    /// una 0.x o una pre-release: quien tiene una versión estable solo ve estables.
    /// </summary>
    internal static UpdateInfo? Newest(IEnumerable<GitHubRelease> releases, AppVersion current)
    {
        var acceptsPreReleases = current.IsPreRelease || current.Number.Major == 0;
        return releases
            .Where(r => !r.Draft && !string.IsNullOrWhiteSpace(r.HtmlUrl))
            .Select(r => (Release: r, Version: ParseTag(r.TagName)))
            .Where(r => r.Version is not null
                        && r.Version > current
                        && (acceptsPreReleases || !(r.Version.IsPreRelease || r.Release.PreRelease)))
            .OrderByDescending(r => r.Version)
            .Select(r => new UpdateInfo(r.Version!, string.IsNullOrWhiteSpace(r.Release.Name) ? r.Release.TagName! : r.Release.Name!, r.Release.HtmlUrl!))
            .FirstOrDefault();
    }

    /// <summary>"v0.8.0" → 0.8.0, "v1.0.0-rc.1" → 1.0.0-rc.1; null si la etiqueta no es una versión (p. ej. "PreRelease").</summary>
    internal static AppVersion? ParseTag(string? tag) => AppVersion.TryParse(tag);

    internal sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool PreRelease { get; set; }
    }
}
