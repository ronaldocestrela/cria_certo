using CriaCerto.Web.Client.Models;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Legal;

public class LegalPagesTests
{
    [Fact]
    public void LegalDocumentCatalog_ShouldContainTermsOfServiceAndPrivacyPolicy()
    {
        // Act
        var documents = LegalDocumentCatalog.GetAllDocuments();

        // Assert
        documents.Should().NotBeEmpty();
        documents.Should().Contain(d => d.Route == "termos-de-uso" || d.Route == "/termos-de-uso");
        documents.Should().Contain(d => d.Route == "politica-de-privacidade" || d.Route == "/politica-de-privacidade");
    }

    [Fact]
    public void LegalDocumentCatalog_ShouldDefineDocumentVersionsAndEffectiveDates()
    {
        // Act
        var terms = LegalDocumentCatalog.GetByRoute("termos-de-uso");
        var privacy = LegalDocumentCatalog.GetByRoute("politica-de-privacidade");

        // Assert
        terms.Should().NotBeNull();
        terms!.Version.Should().Be("1.0");
        terms.EffectiveDate.Should().NotBeNullOrWhiteSpace();

        privacy.Should().NotBeNull();
        privacy!.Version.Should().Be("1.0");
        privacy.EffectiveDate.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PrivacyPolicy_ShouldSpecifyMandatoryLgpdSections()
    {
        // Act
        var sections = LegalDocumentCatalog.GetPrivacyPolicySections();

        // Assert
        sections.Should().NotBeEmpty();
        sections.Should().Contain(s => s.Id == "controlador-operador");
        sections.Should().Contain(s => s.Id == "dados-coletados");
        sections.Should().Contain(s => s.Id == "bases-legais");
        sections.Should().Contain(s => s.Id == "subprocessadores");
        sections.Should().Contain(s => s.Id == "seguranca-dados");
        sections.Should().Contain(s => s.Id == "direitos-titular");
        sections.Should().Contain(s => s.Id == "canal-dpo");
    }

    [Fact]
    public void TermsOfService_ShouldSpecifyMandatoryBovineAndSaaSSections()
    {
        // Act
        var sections = LegalDocumentCatalog.GetTermsOfServiceSections();

        // Assert
        sections.Should().NotBeEmpty();
        sections.Should().Contain(s => s.Id == "objeto-licenca");
        sections.Should().Contain(s => s.Id == "pwa-offline");
        sections.Should().Contain(s => s.Id == "responsabilidade-zootecnica-sanitaria");
        sections.Should().Contain(s => s.Id == "propriedade-dados");
        sections.Should().Contain(s => s.Id == "sla-rescisao");
    }

    [Fact]
    public void DpoContactInfo_ShouldContainOfficialChannel()
    {
        // Act
        var dpoInfo = LegalDocumentCatalog.GetDpoContactInfo();

        // Assert
        dpoInfo.Email.Should().Be("privacidade@criacerto.com.br");
        dpoInfo.MaxResponseDays.Should().Be(15);
        dpoInfo.PhysicalAddress.Should().NotBeNullOrWhiteSpace();
        dpoInfo.Role.Should().Contain("DPO");
    }

    [Fact]
    public void GetByRoute_WithLeadingSlashOrDifferentCasing_ShouldResolveCorrectly()
    {
        // Act
        var terms1 = LegalDocumentCatalog.GetByRoute("termos-de-uso");
        var terms2 = LegalDocumentCatalog.GetByRoute("/termos-de-uso");
        var terms3 = LegalDocumentCatalog.GetByRoute("TERMOS-DE-USO");

        // Assert
        terms1.Should().NotBeNull();
        terms2.Should().Be(terms1);
        terms3.Should().Be(terms1);
    }

    [Fact]
    public void GetByRoute_WithUnknownRoute_ShouldReturnNull()
    {
        // Act
        var result = LegalDocumentCatalog.GetByRoute("rota-inexistente");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void LegalSections_ShouldHaveUniqueIdsAndValidTitles()
    {
        // Act
        var termsSections = LegalDocumentCatalog.GetTermsOfServiceSections();
        var privacySections = LegalDocumentCatalog.GetPrivacyPolicySections();

        // Assert
        termsSections.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        privacySections.Select(s => s.Id).Should().OnlyHaveUniqueItems();

        foreach (var section in termsSections.Concat(privacySections))
        {
            section.Id.Should().NotBeNullOrWhiteSpace();
            section.Title.Should().NotBeNullOrWhiteSpace();
            section.Summary.Should().NotBeNullOrWhiteSpace();
        }
    }
}
