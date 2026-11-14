using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Pwa;

public class PwaAppRazorMetaTagsTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void AppRazor_ShouldContainPwaAndIosMetaTags()
    {
        var appRazorPath = Path.Combine(
            RepoRoot,
            "src/Web/CriaCerto.Web/CriaCerto.Web/Components/App.razor");

        File.Exists(appRazorPath).Should().BeTrue();

        var content = File.ReadAllText(appRazorPath);

        content.Should().Contain("<link rel=\"manifest\" href=\"manifest.webmanifest\" />");
        content.Should().Contain("<link rel=\"apple-touch-icon\" sizes=\"192x192\" href=\"icon-192.png\" />");
        content.Should().Contain("<meta name=\"mobile-web-app-capable\" content=\"yes\" />");
        content.Should().Contain("<meta name=\"apple-mobile-web-app-capable\" content=\"yes\" />");
        content.Should().Contain("<meta name=\"apple-mobile-web-app-status-bar-style\" content=\"default\" />");
        content.Should().Contain("<meta name=\"apple-mobile-web-app-title\" content=\"CriaCerto\" />");
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

