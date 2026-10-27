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

        var dpa1 = LegalDocumentCatalog.GetByRoute("dpa");
        var dpa2 = LegalDocumentCatalog.GetByRoute("/dpa");
        var dpa3 = LegalDocumentCatalog.GetByRoute("aditivo-processamento-dados");
        var dpa4 = LegalDocumentCatalog.GetByRoute("/ADITIVO-PROCESSAMENTO-DADOS");

        dpa1.Should().NotBeNull();
        dpa2.Should().Be(dpa1);
        dpa3.Should().Be(dpa1);
        dpa4.Should().Be(dpa1);
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
    public void LegalDocumentCatalog_ShouldContainDataProcessingAgreement()
    {
        // Act
        var documents = LegalDocumentCatalog.GetAllDocuments();

        // Assert
        documents.Should().NotBeEmpty();
        documents.Should().Contain(d => d.Route == "dpa" || d.Route == "/dpa");
    }

    [Fact]
    public void DataProcessingAgreement_ShouldDefineDocumentVersionAndEffectiveDate()
    {
        // Act
        var dpa = LegalDocumentCatalog.GetByRoute("dpa");

        // Assert
        dpa.Should().NotBeNull();
        dpa!.Version.Should().Be("1.0");
        dpa.EffectiveDate.Should().NotBeNullOrWhiteSpace();
        dpa.Title.Should().Contain("Processamento de Dados");
    }

    [Fact]
    public void DataProcessingAgreement_ShouldSpecifyMandatoryB2BLgpdSections()
    {
        // Act
        var sections = LegalDocumentCatalog.GetDataProcessingAgreementSections();

        // Assert
        sections.Should().NotBeEmpty();
        sections.Should().Contain(s => s.Id == "objeto-escopo");
        sections.Should().Contain(s => s.Id == "instrucoes-licitas");
        sections.Should().Contain(s => s.Id == "salvaguardas-seguranca");
        sections.Should().Contain(s => s.Id == "confidencialidade");
        sections.Should().Contain(s => s.Id == "subprocessadores");
        sections.Should().Contain(s => s.Id == "cooperacao-titulares");
        sections.Should().Contain(s => s.Id == "incidentes-seguranca");
        sections.Should().Contain(s => s.Id == "rescisao-descarte");
        sections.Should().Contain(s => s.Id == "auditoria-conformidade");
    }

    [Fact]
    public void LegalDocumentCatalog_ShouldContainDpoPortalDocument()
    {
        // Act
        var dpoDoc = LegalDocumentCatalog.GetByRoute("dpo");
        var aliasDoc1 = LegalDocumentCatalog.GetByRoute("canal-dpo");
        var aliasDoc2 = LegalDocumentCatalog.GetByRoute("privacidade/canal-dpo");

        // Assert
        dpoDoc.Should().NotBeNull();
        dpoDoc!.Route.Should().Be("dpo");
        dpoDoc.Title.Should().Contain("DPO");
        aliasDoc1.Should().Be(dpoDoc);
        aliasDoc2.Should().Be(dpoDoc);
    }

    [Fact]
    public void DpoPortal_ShouldSpecifyMandatoryArt41Sections()
    {
        // Act
        var sections = LegalDocumentCatalog.GetDpoSections();

        // Assert
        sections.Should().NotBeEmpty();
        sections.Should().Contain(s => s.Id == "identidade-encarregado");
        sections.Should().Contain(s => s.Id == "atribuicoes-dpo");
        sections.Should().Contain(s => s.Id == "direitos-titulares");
        sections.Should().Contain(s => s.Id == "prazos-atendimento");
        sections.Should().Contain(s => s.Id == "formulario-dsr");
    }

    [Fact]
    public void DpoContactInfo_ShouldContainOfficialEmailAndResponseDays()
    {
        // Act
        var dpo = LegalDocumentCatalog.GetDpoContactInfo();

        // Assert
        dpo.Should().NotBeNull();
        dpo.Email.Should().Be("privacidade@criacerto.com.br");
        dpo.MaxResponseDays.Should().Be(15);
        dpo.PhysicalAddress.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LegalSections_ShouldHaveUniqueIdsAndValidTitles()
    {
        // Act
        var termsSections = LegalDocumentCatalog.GetTermsOfServiceSections();
        var privacySections = LegalDocumentCatalog.GetPrivacyPolicySections();
        var dpaSections = LegalDocumentCatalog.GetDataProcessingAgreementSections();
        var dpoSections = LegalDocumentCatalog.GetDpoSections();

        // Assert
        termsSections.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        privacySections.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        dpaSections.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        dpoSections.Select(s => s.Id).Should().OnlyHaveUniqueItems();

        foreach (var section in termsSections.Concat(privacySections).Concat(dpaSections).Concat(dpoSections))
        {
            section.Id.Should().NotBeNullOrWhiteSpace();
            section.Title.Should().NotBeNullOrWhiteSpace();
            section.Summary.Should().NotBeNullOrWhiteSpace();
        }
    }
}
