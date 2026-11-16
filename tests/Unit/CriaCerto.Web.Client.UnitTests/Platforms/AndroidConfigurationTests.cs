using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Platforms;

public class AndroidConfigurationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void AndroidManifest_ShouldContainRequiredNetworkPermissions()
    {
        var manifestPath = Path.Combine(
            RepoRoot,
            "src/Mobile/CriaCerto.Mobile/Platforms/Android/AndroidManifest.xml");

        File.Exists(manifestPath).Should().BeTrue("O arquivo AndroidManifest.xml deve existir no projeto CriaCerto.Mobile");

        var doc = XDocument.Load(manifestPath);
        var manifest = doc.Root;
        manifest.Should().NotBeNull();

        XNamespace androidNs = "http://schemas.android.com/apk/res/android";

        var permissions = manifest!.Elements("uses-permission")
            .Select(x => x.Attribute(androidNs + "name")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        permissions.Should().Contain("android.permission.ACCESS_NETWORK_STATE",
            "O app precisa de ACCESS_NETWORK_STATE para o IConnectivityService nativo");
        permissions.Should().Contain("android.permission.INTERNET",
            "O app precisa de INTERNET para sincronização e chamadas à API");
    }

    [Fact]
    public void AndroidManifest_ShouldReferenceNetworkSecurityConfig()
    {
        var manifestPath = Path.Combine(
            RepoRoot,
            "src/Mobile/CriaCerto.Mobile/Platforms/Android/AndroidManifest.xml");

        File.Exists(manifestPath).Should().BeTrue();

        var doc = XDocument.Load(manifestPath);
        var manifest = doc.Root;
        manifest.Should().NotBeNull();

        XNamespace androidNs = "http://schemas.android.com/apk/res/android";
        var application = manifest!.Element("application");
        application.Should().NotBeNull("AndroidManifest.xml deve declarar a tag <application>");

        var networkSecurityConfig = application!.Attribute(androidNs + "networkSecurityConfig")?.Value;
        networkSecurityConfig.Should().Be("@xml/network_security_config",
            "O elemento application deve referenciar @xml/network_security_config para governança de tráfego HTTP/HTTPS");
    }

    [Fact]
    public void NetworkSecurityConfig_ShouldExistAndEnforceHttpsWithLocalCleartextBypass()
    {
        var configPath = Path.Combine(
            RepoRoot,
            "src/Mobile/CriaCerto.Mobile/Platforms/Android/Resources/xml/network_security_config.xml");

        File.Exists(configPath).Should().BeTrue("O arquivo network_security_config.xml deve existir em Resources/xml/");

        var doc = XDocument.Load(configPath);
        var root = doc.Root;
        root.Should().NotBeNull();
        root!.Name.LocalName.Should().Be("network-security-config");

        // Valida domain-config com cleartext liberado para emulador e loopback local
        var domainConfig = root.Element("domain-config");
        domainConfig.Should().NotBeNull("Deve haver um <domain-config> permitindo testes locais");
        domainConfig!.Attribute("cleartextTrafficPermitted")?.Value.Should().Be("true");

        var domains = domainConfig.Elements("domain").Select(d => d.Value.Trim()).ToList();
        domains.Should().Contain("10.0.2.2", "10.0.2.2 é o alias do host no emulador Android");
        domains.Should().Contain("localhost", "localhost para desenvolvimento");
        domains.Should().Contain("127.0.0.1", "127.0.0.1 para loopback");

        // Valida base-config bloqueando cleartext para todos os demais domínios (Release / Produção exige HTTPS)
        var baseConfig = root.Element("base-config");
        baseConfig.Should().NotBeNull("Deve haver um <base-config> para impor HTTPS como padrão do sistema");
        baseConfig!.Attribute("cleartextTrafficPermitted")?.Value.Should().Be("false",
            "base-config deve exigir HTTPS (cleartextTrafficPermitted=false) prevenindo tráfego desprotegido em produção");

        var certificates = baseConfig.Descendants("certificates").Select(c => c.Attribute("src")?.Value).ToList();
        certificates.Should().Contain("system", "base-config deve confiar nas CAs do sistema operacional");
    }

    [Fact]
    public void MainActivity_ShouldDeclareAllMandatoryConfigurationChanges()
    {
        var mainActivityPath = Path.Combine(
            RepoRoot,
            "src/Mobile/CriaCerto.Mobile/Platforms/Android/MainActivity.cs");

        File.Exists(mainActivityPath).Should().BeTrue("MainActivity.cs deve existir no host CriaCerto.Mobile");

        var code = File.ReadAllText(mainActivityPath);

        code.Should().Contain("ConfigChanges.ScreenSize");
        code.Should().Contain("ConfigChanges.Orientation");
        code.Should().Contain("ConfigChanges.UiMode");
        code.Should().Contain("ConfigChanges.ScreenLayout");
        code.Should().Contain("ConfigChanges.SmallestScreenSize");
        code.Should().Contain("ConfigChanges.Density");
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

