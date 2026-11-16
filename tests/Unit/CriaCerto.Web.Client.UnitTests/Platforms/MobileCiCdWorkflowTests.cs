using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Platforms;

public class MobileCiCdWorkflowTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void WorkflowFile_ShouldExistAtCanonicalPath()
    {
        var workflowPath = Path.Combine(RepoRoot, ".github/workflows/mobile-build.yml");

        File.Exists(workflowPath).Should().BeTrue("O arquivo de workflow .github/workflows/mobile-build.yml deve existir no repositório");
    }

    [Fact]
    public void Workflow_ShouldDeclareRequiredTriggersAndPaths()
    {
        var workflowPath = Path.Combine(RepoRoot, ".github/workflows/mobile-build.yml");
        var content = File.ReadAllText(workflowPath);

        content.Should().Contain("push:");
        content.Should().Contain("pull_request:");
        content.Should().Contain("workflow_dispatch:");

        content.Should().Contain("src/Mobile/**", "Alterações no código mobile devem acionar o workflow");
        content.Should().Contain("src/Web/CriaCerto.Shared.UI/**", "Alterações na RCL compartilhada devem acionar o workflow");
        content.Should().Contain(".github/workflows/mobile-build.yml", "Alterações no próprio workflow devem acioná-lo");
    }

    [Fact]
    public void AndroidJob_ShouldRunOnUbuntuWithMauiAndroidWorkload()
    {
        var workflowPath = Path.Combine(RepoRoot, ".github/workflows/mobile-build.yml");
        var content = File.ReadAllText(workflowPath);

        content.Should().Contain("build-android:", "Deve haver um job dedicado para Android");
        content.Should().Contain("runs-on: ubuntu-latest", "O build de Android deve rodar em runner Linux ubuntu-latest");
        content.Should().Contain("actions/setup-java@v4", "O build de Android necessita do SDK Java");
        content.Should().Contain("dotnet workload install maui-android", "O job deve instalar o workload maui-android");
        content.Should().Contain("-f net10.0-android", "O comando de build/publish deve mirar no TFM net10.0-android");
        content.Should().Contain("-c Release", "A compilação de validação de pacote deve ser em configuração Release");
    }

    [Fact]
    public void IosJob_ShouldRunOnMacOsWithMauiIosWorkload()
    {
        var workflowPath = Path.Combine(RepoRoot, ".github/workflows/mobile-build.yml");
        var content = File.ReadAllText(workflowPath);

        content.Should().Contain("build-ios:", "Deve haver um job dedicado para iOS");
        content.Should().Contain("runs-on: macos-latest", "O build de iOS deve rodar em runner macos-latest com Xcode");
        content.Should().Contain("dotnet workload install maui-ios", "O job deve instalar o workload maui-ios");
        content.Should().Contain("-f net10.0-ios", "O comando de build deve mirar no TFM net10.0-ios");
        content.Should().Contain("-c Release", "A compilação de validação de pacote deve ser em configuração Release");
    }

    [Fact]
    public void ReferencedProjectPaths_ShouldExistInRepository()
    {
        var workflowPath = Path.Combine(RepoRoot, ".github/workflows/mobile-build.yml");
        var content = File.ReadAllText(workflowPath);

        // Encontra todas as menções a arquivos .csproj no workflow
        var csprojMatches = Regex.Matches(content, @"(?<=[\s""'\b])src/[^\s""']+\.csproj\b");
        csprojMatches.Should().NotBeEmpty("O workflow deve referenciar projetos .csproj existentes");

        foreach (Match match in csprojMatches)
        {
            var relativePath = match.Value;
            var fullPath = Path.Combine(RepoRoot, relativePath);

            File.Exists(fullPath).Should().BeTrue($"O projeto '{relativePath}' referenciado no workflow deve existir no repositório");
        }
    }

    [Fact]
    public void IosSigningDocumentation_ShouldSpecifyRequiredSecrets()
    {
        var workflowPath = Path.Combine(RepoRoot, ".github/workflows/mobile-build.yml");
        var content = File.ReadAllText(workflowPath);

        content.Should().Contain("APPLE_CERTIFICATE_BASE64", "O workflow deve documentar o secret para o certificado Apple");
        content.Should().Contain("APPLE_CERTIFICATE_PASSWORD", "O workflow deve documentar o secret para a senha do certificado Apple");
        content.Should().Contain("APPLE_PROVISIONING_PROFILE_BASE64", "O workflow deve documentar o secret para o provisioning profile Apple");
    }

    [Fact]
    public void SolutionFile_ShouldExcludeMobileFromDefaultBuild_ToPreventCiFailure()
    {
        var slnxPath = Path.Combine(RepoRoot, "CriaCerto.slnx");
        var content = File.ReadAllText(slnxPath);

        content.Should().Contain("src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj", "O projeto mobile deve constar na solução para desenvolvimento");
        content.Should().Contain("<Build Solution=\"*|*\" Project=\"false\" />",
            "O projeto mobile deve estar configurado com Build false na solução para não quebrar runners de CI backend sem workloads MAUI");
    }

    [Fact]
    public void GlobalJson_ShouldPinDotNet10Sdk()
    {
        var globalJsonPath = Path.Combine(RepoRoot, "global.json");
        File.Exists(globalJsonPath).Should().BeTrue("O arquivo global.json deve existir na raiz do repositório");

        var content = File.ReadAllText(globalJsonPath);
        content.Should().Contain("\"version\": \"10.0.100\"", "Deve fixar o SDK .NET 10");
        content.Should().Contain("\"rollForward\": \"latestFeature\"", "Deve permitir roll forward seguro apenas dentro do .NET 10");
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
