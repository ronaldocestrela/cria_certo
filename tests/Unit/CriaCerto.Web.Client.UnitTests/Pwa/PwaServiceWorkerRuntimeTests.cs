using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Pwa;

public class PwaServiceWorkerRuntimeTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void ServiceWorkerJs_ShouldDefineCacheVersionAndCoreAssets()
    {
        var swPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.js");

        File.Exists(swPath).Should().BeTrue();
        var content = File.ReadAllText(swPath);

        content.Should().Contain("const CACHE_NAME = \"criacerto-shell-v2\";");
        content.Should().Contain("const OFFLINE_URL = \"/offline.html\";");
        content.Should().Contain("\"/manifest.webmanifest\"");
        content.Should().Contain("\"/icon-192.png\"");
        content.Should().Contain("\"/icon-512.png\"");
        content.Should().Contain("\"/favicon.png\"");
        content.Should().Contain("\"/app.css\"");
        content.Should().Contain("\"/js/offlineSync.js\"");
        content.Should().Contain("\"/js/downloadHelper.js\"");
    }

    [Fact]
    public void ServiceWorkerJs_ShouldImplementImmediateLifecycleActivation()
    {
        var swPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.js");

        var content = File.ReadAllText(swPath);

        content.Should().Contain("self.skipWaiting()");
        content.Should().Contain("self.clients.claim()");
        content.Should().Contain("caches.delete(key)");
    }

    [Fact]
    public void ServiceWorkerJs_ShouldNeverInterceptNonGetExternalOrApiAndHealthRoutes()
    {
        var swPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.js");

        var content = File.ReadAllText(swPath);

        content.Should().Contain("event.request.method !== \"GET\"");
        content.Should().Contain("url.origin !== self.location.origin");
        content.Should().Contain("url.pathname.startsWith(\"/api/\")");
        content.Should().Contain("url.pathname.startsWith(\"/health\")");
    }

    [Fact]
    public void ServiceWorkerJs_ShouldProvideNavigationAndStaticAssetOfflineFallbacks()
    {
        var swPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.js");

        var content = File.ReadAllText(swPath);

        content.Should().Contain("event.request.mode === \"navigate\"");
        content.Should().Contain("caches.match(\"/\")");
        content.Should().Contain("caches.match(OFFLINE_URL)");
        content.Should().Contain("status: 503");
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

