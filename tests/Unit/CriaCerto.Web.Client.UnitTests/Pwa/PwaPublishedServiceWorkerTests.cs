using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Pwa;

public class PwaPublishedServiceWorkerTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly string PublishedSwPath = Path.Combine(
        RepoRoot,
        "src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.published.js");

    [Fact]
    public void PublishedServiceWorker_ShouldImportAssetsManifestResilientlyWhenAvailable()
    {
        File.Exists(PublishedSwPath).Should().BeTrue();
        var content = File.ReadAllText(PublishedSwPath);

        content.Should().MatchRegex(@"self\.importScripts\(\s*['""]\./service-worker-assets\.js['""]\s*\);");
        Regex.IsMatch(
                content,
                @"try\s*\{\s*self\.importScripts\(\s*['""]\./service-worker-assets\.js['""]\s*\);\s*\}\s*catch",
                RegexOptions.Singleline)
            .Should()
            .BeTrue("o service-worker.published.js deve carregar ./service-worker-assets.js de forma resiliente quando disponível em dotnet publish");
    }

    [Fact]
    public void PublishedServiceWorker_ShouldFilterAndPreCacheManifestAssets()
    {
        var content = File.ReadAllText(PublishedSwPath);

        content.Should().Contain("self.assetsManifest");
        content.Should().Contain("self.assetsManifest.assets");
        content.Should().Contain("offlineAssetsInclude");
        content.Should().Contain("offlineAssetsExclude");

        // Extensões exigidas na Subtarefa 3.3.1 (.dll, .pdb, .wasm, .js, .css, .png, .html, .webmanifest)
        content.Should().Contain(@"/\.dll$/");
        content.Should().Contain(@"/\.pdb$/");
        content.Should().Contain(@"/\.wasm$/");
        content.Should().Contain(@"/\.js$/");
        content.Should().Contain(@"/\.css$/");
        content.Should().Contain(@"/\.png$/");
        content.Should().Contain(@"/\.html$/");
        content.Should().Contain(@"/\.webmanifest$/");
        content.Should().Contain(@"/^service-worker\.js$/");
        content.Should().Contain("new Request(asset.url, { cache: \"no-cache\" })");
    }

    [Fact]
    public void PublishedServiceWorker_ShouldImplementImmediateLifecycleAndVersionedCacheCleanup()
    {
        var content = File.ReadAllText(PublishedSwPath);

        content.Should().Contain("const CACHE_NAME_PREFIX = \"criacerto-offline-cache-\";");
        content.Should().Contain("self.assetsManifest?.version");
        content.Should().Contain("self.skipWaiting()");
        content.Should().Contain("self.clients.claim()");
        content.Should().Contain("caches.delete(key)");
    }

    [Fact]
    public void PublishedServiceWorker_ShouldNeverInterceptNonGetExternalOrApiAndHealthRoutes()
    {
        var content = File.ReadAllText(PublishedSwPath);

        content.Should().Contain("event.request.method !== \"GET\"");
        content.Should().Contain("url.origin !== self.location.origin");
        content.Should().Contain("url.pathname.startsWith(\"/api/\")");
        content.Should().Contain("url.pathname.startsWith(\"/health\")");
        content.Should().Contain("event.request.mode === \"navigate\"");
        content.Should().Contain("caches.match(OFFLINE_URL)");
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

