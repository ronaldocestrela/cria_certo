using System.Buffers.Binary;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Pwa;

public class PwaIconAssetsTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Theory]
    [InlineData("src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-192.png", 192, 192)]
    [InlineData("src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-512.png", 512, 512)]
    [InlineData("src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/icon-192.png", 192, 192)]
    [InlineData("src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/icon-512.png", 512, 512)]
    public void PwaIcons_ShouldExistAndMatchExactPngDimensions(string relativePath, int expectedWidth, int expectedHeight)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath);
        File.Exists(fullPath).Should().BeTrue($"o ícone PWA '{relativePath}' é obrigatório para instalabilidade");

        var bytes = File.ReadAllBytes(fullPath);
        bytes.Length.Should().BeGreaterThan(33);

        // Assinatura PNG: 89 50 4E 47 0D 0A 1A 0A
        bytes[..8].Should().Equal(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);

        // Chunk IHDR (offset 12..16 = "IHDR", 16..20 = Width, 20..24 = Height)
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));

        width.Should().Be(expectedWidth);
        height.Should().Be(expectedHeight);
    }

    [Fact]
    public void ZoneIdentifierResidualFile_ShouldNotExistInHostWwwroot()
    {
        var zoneFile = Path.Combine(RepoRoot, "src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/CriaCerto.png:Zone.Identifier");
        File.Exists(zoneFile).Should().BeFalse("arquivos residuais Zone.Identifier devem ser removidos do wwwroot");
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

