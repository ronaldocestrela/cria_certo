using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Platforms;

public class IosConfigurationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void InfoPlist_ShouldContainRequiredMetadata()
    {
        var plistPath = Path.Combine(
            RepoRoot,
            "src/Mobile/CriaCerto.Mobile/Platforms/iOS/Info.plist");

        File.Exists(plistPath).Should().BeTrue("O arquivo Info.plist deve existir em Platforms/iOS/");

        var doc = XDocument.Load(plistPath);
        var dict = doc.Root?.Element("dict");
        dict.Should().NotBeNull("Info.plist deve possuir um nó raiz <dict>");

        // CFBundleDisplayName
        var displayNameVal = GetDictValue(dict!, "CFBundleDisplayName");
        displayNameVal.Should().NotBeNull();
        displayNameVal!.Value.Should().Be("CriaCerto", "CFBundleDisplayName deve ser CriaCerto");

        // CFBundleIdentifier
        var bundleIdVal = GetDictValue(dict!, "CFBundleIdentifier");
        bundleIdVal.Should().NotBeNull();
        bundleIdVal!.Value.Should().Be("br.com.criacerto.app", "CFBundleIdentifier deve ser br.com.criacerto.app");

        // UILaunchStoryboardName
        var storyboardVal = GetDictValue(dict!, "UILaunchStoryboardName");
        storyboardVal.Should().NotBeNull("Info.plist deve declarar UILaunchStoryboardName");
        storyboardVal!.Value.Should().Be("MauiSplash", "UILaunchStoryboardName deve apontar para o storyboard MauiSplash gerado pelo MAUI");

        // ITSAppUsesNonExemptEncryption
        var encryptionVal = GetDictValue(dict!, "ITSAppUsesNonExemptEncryption");
        encryptionVal.Should().NotBeNull("Info.plist deve declarar ITSAppUsesNonExemptEncryption para dispensa de conformidade de exportação");
        encryptionVal!.Name.LocalName.Should().Be("false", "ITSAppUsesNonExemptEncryption deve ser false");
    }

    [Fact]
    public void InfoPlist_ShouldConfigureAppTransportSecurityForLocalNetworking()
    {
        var plistPath = Path.Combine(
            RepoRoot,
            "src/Mobile/CriaCerto.Mobile/Platforms/iOS/Info.plist");

        File.Exists(plistPath).Should().BeTrue();

        var doc = XDocument.Load(plistPath);
        var dict = doc.Root?.Element("dict");
        dict.Should().NotBeNull();

        var atsDict = GetDictValue(dict!, "NSAppTransportSecurity");
        atsDict.Should().NotBeNull("Info.plist deve declarar o dicionário NSAppTransportSecurity para governança de rede");
        atsDict!.Name.LocalName.Should().Be("dict");

        // NSAllowsLocalNetworking deve ser true (libera http://localhost no simulador)
        var localNetworkingVal = GetDictValue(atsDict, "NSAllowsLocalNetworking");
        localNetworkingVal.Should().NotBeNull("NSAppTransportSecurity deve declarar NSAllowsLocalNetworking");
        localNetworkingVal!.Name.LocalName.Should().Be("true", "NSAllowsLocalNetworking deve ser true para permitir depuração local");

        // NSAllowsArbitraryLoads NÃO deve ser true para não comprometer tráfego de produção
        var arbitraryLoadsVal = GetDictValue(atsDict, "NSAllowsArbitraryLoads");
        arbitraryLoadsVal?.Name.LocalName.Should().NotBe("true", "NSAllowsArbitraryLoads não deve ser true em ambiente seguro");
    }

    [Fact]
    public void EntitlementsPlist_ShouldExistAndDeclareKeychainAccessGroups()
    {
        var entitlementsPath = Path.Combine(
            RepoRoot,
            "src/Mobile/CriaCerto.Mobile/Platforms/iOS/Entitlements.plist");

        File.Exists(entitlementsPath).Should().BeTrue("O arquivo Entitlements.plist deve existir em Platforms/iOS/");

        var doc = XDocument.Load(entitlementsPath);
        var dict = doc.Root?.Element("dict");
        dict.Should().NotBeNull("Entitlements.plist deve possuir um nó raiz <dict>");

        var keychainGroups = GetDictValue(dict!, "keychain-access-groups");
        keychainGroups.Should().NotBeNull("Entitlements.plist deve declarar keychain-access-groups para suportar o SecureStorage.Default");
        keychainGroups!.Name.LocalName.Should().Be("array");

        var strings = keychainGroups.Elements("string").Select(s => s.Value.Trim()).ToList();
        strings.Should().Contain("$(AppIdentifierPrefix)br.com.criacerto.app",
            "keychain-access-groups deve declarar $(AppIdentifierPrefix)br.com.criacerto.app para isolamento de credenciais no Keychain");
    }

    [Fact]
    public void MobileCsproj_ShouldReferenceIosCodesignEntitlements()
    {
        var csprojPath = Path.Combine(
            RepoRoot,
            "src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj");

        File.Exists(csprojPath).Should().BeTrue("CriaCerto.Mobile.csproj deve existir");

        var content = File.ReadAllText(csprojPath);
        content.Should().Contain("CodesignEntitlements",
            "CriaCerto.Mobile.csproj deve configurar CodesignEntitlements para plataformas iOS");
        content.Should().Contain(@"Platforms\iOS\Entitlements.plist",
            "CodesignEntitlements deve apontar para Platforms\\iOS\\Entitlements.plist");
    }

    private static XElement? GetDictValue(XElement dict, string keyName)
    {
        var keyElement = dict.Elements("key").FirstOrDefault(k => k.Value.Trim() == keyName);
        return keyElement?.ElementsAfterSelf().FirstOrDefault();
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

