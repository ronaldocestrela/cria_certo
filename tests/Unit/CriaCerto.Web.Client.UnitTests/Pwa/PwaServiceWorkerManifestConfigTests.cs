using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Pwa;

public class PwaServiceWorkerManifestConfigTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void ClientCsproj_ShouldConfigureServiceWorkerAssetsManifestAndServiceWorkerItem()
    {
        var csprojPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web.Client/CriaCerto.Web.Client.csproj");

        File.Exists(csprojPath).Should().BeTrue();

        var doc = XDocument.Load(csprojPath);

        var manifestValue = doc.Descendants("ServiceWorkerAssetsManifest")
            .Select(x => x.Value)
            .FirstOrDefault();

        manifestValue.Should().Be("service-worker-assets.js");

        var serviceWorkerElement = doc.Descendants("ServiceWorker").FirstOrDefault();
        serviceWorkerElement.Should().NotBeNull();
        serviceWorkerElement!.Attribute("Include")?.Value.Should().Be(@"wwwroot\service-worker.js");
        serviceWorkerElement.Attribute("PublishedContent")?.Value.Should().Be(@"wwwroot\service-worker.published.js");

        var devSwPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.js");
        var publishedSwPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.published.js");

        File.Exists(devSwPath).Should().BeTrue();
        File.Exists(publishedSwPath).Should().BeTrue();
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

