using System.Globalization;
using System.IO;
using System.Windows;
using AstroPlanner.Core.Models;
using AstroPlanner.UI.Converters;
using AstroPlanner.UI.Services;
using FluentAssertions;

namespace AstroPlanner.Tests;

/// <summary>Detalles de la versión 1.0: versiones con RC, enlaces, posición de la ventana y textos en español.</summary>
public class ReleaseTests
{
    // ─── Versiones ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.0.0", "1.0.0", null)]
    [InlineData("v1.0.0-rc.1", "1.0.0", "rc.1")]
    [InlineData("1.0.0-rc.1+3f2a9c1", "1.0.0", "rc.1")]   // metadatos de compilación del SDK
    [InlineData("0.8", "0.8.0", null)]
    [InlineData("1.0.0.0", "1.0.0", null)]
    public void AppVersion_ParsesReleaseAndPreReleaseTags(string text, string number, string? preRelease)
    {
        var version = AppVersion.Parse(text);

        version.Number.Should().Be(Version.Parse(number));
        version.PreRelease.Should().Be(preRelease);
        version.IsPreRelease.Should().Be(preRelease is not null);
    }

    [Theory]
    [InlineData("1.0.0-rc.1", "1.0.0")]        // la RC va antes que la final
    [InlineData("1.0.0-rc.2", "1.0.0-rc.10")]  // identificadores numéricos como números
    [InlineData("1.0.0-beta", "1.0.0-rc.1")]
    [InlineData("0.8.0", "1.0.0-rc.1")]
    [InlineData("1.0.0", "1.0.1")]
    public void AppVersion_OrdersLikeSemver(string lower, string higher)
    {
        (AppVersion.Parse(lower) < AppVersion.Parse(higher)).Should().BeTrue();
        (AppVersion.Parse(higher) > AppVersion.Parse(lower)).Should().BeTrue();
    }

    [Fact]
    public void AppVersion_Current_ComesFromTheBuild()
    {
        AppVersion.Current.Number.Should().BeGreaterThanOrEqualTo(new Version(1, 0, 0));
        AppVersion.Current.ToString().Should().NotContain("+", "los metadatos de compilación no se muestran");
    }

    private static UpdateInfo? Newest(string current, params (string Tag, bool PreRelease)[] releases) =>
        GitHubUpdateChecker.Newest(
            releases.Select(r => new GitHubUpdateChecker.GitHubRelease
            {
                TagName = r.Tag,
                Name = "AstroPlanner " + r.Tag,
                HtmlUrl = "https://github.com/Yamato6/AstroPlanner/releases/tag/" + r.Tag,
                PreRelease = r.PreRelease
            }),
            AppVersion.Parse(current));

    [Fact]
    public void UpdateChecker_OffersTheReleaseCandidateTo0xUsers()
    {
        Newest("0.8.0", ("v0.8.0", true), ("v1.0.0-rc.1", true))!
            .Version.ToString().Should().Be("1.0.0-rc.1");
    }

    [Fact]
    public void UpdateChecker_OffersTheFinalReleaseToReleaseCandidateUsers()
    {
        Newest("1.0.0-rc.1", ("v1.0.0-rc.1", true), ("v1.0.0", false))!
            .Version.ToString().Should().Be("1.0.0");
    }

    [Fact]
    public void UpdateChecker_StableUsersNeverGetPreReleases()
    {
        // Ni por el sufijo del tag ni por la marca "pre-release" de GitHub.
        Newest("1.0.0", ("v1.1.0-rc.1", false), ("v1.0.5", true)).Should().BeNull();
        Newest("1.0.0", ("v1.1.0-rc.1", false), ("v1.0.1", false))!
            .Version.ToString().Should().Be("1.0.1");
    }

    // ─── Enlaces ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://github.com/Yamato6/AstroPlanner/releases/tag/v1.0.0", true)]
    [InlineData("https://open-meteo.com/", true)]
    [InlineData("https://api.open-meteo.com/v1/forecast", true)]
    [InlineData("http://github.com/Yamato6/AstroPlanner", false)]     // sin https
    [InlineData("https://github.com.evil.example/", false)]           // dominio parecido
    [InlineData("https://evilgithub.com/", false)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    public void SafeLinks_OnlyAllowsHttpsLinksToKnownSites(string url, bool allowed)
    {
        SafeLinks.IsAllowed(new Uri(url)).Should().Be(allowed);
    }

    // ─── Posición de la ventana ─────────────────────────────────────────

    private static readonly Rect Screen = new(0, 0, 1920, 1080);
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void WindowPlacement_KeepsAPositionThatIsOnScreen()
    {
        var saved = new WindowPlacement(100, 80, 1400, 800, false);

        saved.FitTo(Screen, WorkArea, 1080, 640).Should().Be(saved);
    }

    [Fact]
    public void WindowPlacement_CentersAWindowLeftOnADisconnectedMonitor()
    {
        // Estaba en un segundo monitor a la derecha que ya no existe.
        var fitted = new WindowPlacement(2500, 100, 1400, 800, true).FitTo(Screen, WorkArea, 1080, 640);

        fitted.Left.Should().Be(260);
        fitted.Top.Should().Be(120);
        fitted.IsMaximized.Should().BeTrue();
    }

    [Fact]
    public void WindowPlacement_ShrinksToTheDesktopAndRespectsTheMinimumSize()
    {
        new WindowPlacement(0, 0, 3000, 2000, false).FitTo(Screen, WorkArea, 1080, 640)
            .Should().Match<WindowPlacement>(p => p.Width == 1920 && p.Height == 1080);
        new WindowPlacement(0, 0, 300, 200, false).FitTo(Screen, WorkArea, 1080, 640)
            .Should().Match<WindowPlacement>(p => p.Width == 1080 && p.Height == 640);
    }

    [Fact]
    public void WindowPlacement_KeepsALargeWindowOnALargerSecondMonitor()
    {
        // Principal 1920×1080 y, a su derecha, un segundo monitor de 2560×1440.
        var desktop = new Rect(0, 0, 1920 + 2560, 1440);
        var saved = new WindowPlacement(2000, 20, 2400, 1300, false);

        saved.FitTo(desktop, WorkArea, 1080, 640).Should().Be(saved);
    }

    [Fact]
    public void WindowPlacementStore_RoundTripsAndIgnoresCorruptFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"astroplanner-window-{Guid.NewGuid():N}.json");
        try
        {
            var store = new WindowPlacementStore(path);
            store.Load().Should().BeNull();

            var placement = new WindowPlacement(10, 20, 1300, 700, true);
            store.Save(placement);
            store.Load().Should().Be(placement);

            File.WriteAllText(path, "{ no es json");
            store.Load().Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─── Textos en español ──────────────────────────────────────────────

    [Fact]
    public void DisplayNames_EveryObjectTypeHasASpanishName()
    {
        foreach (var type in Enum.GetValues<CelestialObjectType>())
        {
            DisplayNames.Of(type).Should().NotBe(type.ToString(), $"{type} debe mostrarse en español");
        }

        DisplayNames.Of(CelestialObjectType.GlobularCluster).Should().Be("Cúmulo globular");
    }

    [Fact]
    public void DisplayNameConverter_ShowsObjectTypesInSpanish()
    {
        new DisplayNameConverter().Convert(CelestialObjectType.OpenCluster, typeof(string), null!, CultureInfo.InvariantCulture)
            .Should().Be("Cúmulo abierto");
    }
}
