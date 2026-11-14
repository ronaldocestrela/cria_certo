using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Pwa;

public class PwaManifestTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void ManifestWebmanifest_ShouldContainRequiredPwaMetadataInPtBr()
    {
        var manifestPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/manifest.webmanifest");

        File.Exists(manifestPath).Should().BeTrue();

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;

        root.GetProperty("id").GetString().Should().Be("/");
        root.GetProperty("name").GetString().Should().Be("CriaCerto - Gestão Pecuária Inteligente");
        root.GetProperty("short_name").GetString().Should().Be("CriaCerto");
        root.GetProperty("description").GetString().Should().Be("Plataforma SaaS de gestão pecuária de precisão com operação offline no curral.");
        root.GetProperty("lang").GetString().Should().Be("pt-BR");
        root.GetProperty("dir").GetString().Should().Be("ltr");
        root.GetProperty("start_url").GetString().Should().Be("/");
        root.GetProperty("scope").GetString().Should().Be("/");
        root.GetProperty("display").GetString().Should().Be("standalone");
        root.GetProperty("orientation").GetString().Should().Be("portrait-primary");
        root.GetProperty("background_color").GetString().Should().Be("#f5f7f5");
        root.GetProperty("theme_color").GetString().Should().Be("#1f5638");

        var categories = root.GetProperty("categories").EnumerateArray().Select(x => x.GetString()).ToList();
        categories.Should().Equal("business", "productivity", "utilities");

        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        icons.Should().HaveCount(2);

        icons[0].GetProperty("src").GetString().Should().Be("/icon-192.png");
        icons[0].GetProperty("sizes").GetString().Should().Be("192x192");
        icons[0].GetProperty("type").GetString().Should().Be("image/png");
        icons[0].GetProperty("purpose").GetString().Should().Be("any maskable");

        icons[1].GetProperty("src").GetString().Should().Be("/icon-512.png");
        icons[1].GetProperty("sizes").GetString().Should().Be("512x512");
        icons[1].GetProperty("type").GetString().Should().Be("image/png");
        icons[1].GetProperty("purpose").GetString().Should().Be("any maskable");
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "agents.md")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

