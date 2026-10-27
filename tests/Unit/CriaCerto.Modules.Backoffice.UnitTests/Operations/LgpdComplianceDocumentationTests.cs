using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Operations;

/// <summary>
/// Executable living documentation and automated compliance tests verifying that
/// the ROPA (Record of Processing Activities - Art. 37 LGPD) and RIPD (DPIA - Art. 38 LGPD)
/// are formally maintained, complete, and aligned with architectural decisions.
/// </summary>
public class LgpdComplianceDocumentationTests
{
    private readonly string _repoRoot;

    public LgpdComplianceDocumentationTests()
    {
        _repoRoot = FindRepositoryRoot();
    }

    [Fact]
    public void RopaDocument_MustExist_AndContainAllSevenMacroprocessesAndComplianceSafeguards()
    {
        // Arrange
        var ropaPath = Path.Combine(_repoRoot, "docs", "compliance", "ropa_data_inventory.md");

        // Assert existence
        File.Exists(ropaPath).Should().BeTrue("ROPA document must exist at docs/compliance/ropa_data_inventory.md");

        var content = File.ReadAllText(ropaPath);

        // Assert DPO identification and regulatory scope
        content.Should().Contain("ROPA - Art. 37 LGPD");
        content.Should().Contain("dpo@criacerto.com.br");
        content.Should().Contain("/dpo");

        // Assert all 7 macroprocesses are documented
        content.Should().Contain("ROPA-ACT-01", "Macroprocesso 01: Onboarding e Autenticação deve estar inventariado");
        content.Should().Contain("ROPA-ACT-02", "Macroprocesso 02: Cobrança e Assinaturas deve estar inventariado");
        content.Should().Contain("ROPA-ACT-03", "Macroprocesso 03: Gestão Zootécnica e Rebanho deve estar inventariado");
        content.Should().Contain("ROPA-ACT-04", "Macroprocesso 04: Suporte Assistido e Impersonação deve estar inventariado");
        content.Should().Contain("ROPA-ACT-05", "Macroprocesso 05: Programa de Afiliados (Referral) deve estar inventariado");
        content.Should().Contain("ROPA-ACT-06", "Macroprocesso 06: Atendimento a Titulares (DSR) deve estar inventariado");
        content.Should().Contain("ROPA-ACT-07", "Macroprocesso 07: Resposta a Incidentes (Art. 48) deve estar inventariado");

        // Assert essential legal bases and technical safeguards
        content.Should().Contain("Art. 7º, V", "Execução de Contrato deve ser base legal citada");
        content.Should().Contain("Art. 7º, II", "Cumprimento de Obrigação Legal deve ser base legal citada");
        content.Should().Contain("AES-256-GCM", "Criptografia autenticada em repouso deve estar documentada");
        content.Should().Contain("HMAC-SHA256", "Blind Indexing deve estar documentado");
        content.Should().Contain("PBKDF2", "Proteção de senhas deve estar documentada");
        content.Should().Contain("AuditLog", "Trilha forense deve estar documentada");
        content.Should().Contain("Stripe Inc.");
        content.Should().Contain("Resend Inc.");
    }

    [Fact]
    public void RipdDocument_MustExist_AndDocumentImpersonationAndFinancialSafeguards()
    {
        // Arrange
        var ripdPath = Path.Combine(_repoRoot, "docs", "compliance", "ripd_dpia_overview.md");

        // Assert existence
        File.Exists(ripdPath).Should().BeTrue("RIPD document must exist at docs/compliance/ripd_dpia_overview.md");

        var content = File.ReadAllText(ripdPath);

        // Assert regulatory framing
        content.Should().Contain("RIPD / DPIA");
        content.Should().Contain("Art. 38");
        content.Should().Contain("dpo@criacerto.com.br");

        // Assert Impersonation risk analysis and safeguards
        content.Should().Contain("Impersonação de Suporte");
        content.Should().Contain("Dupla Salvaguarda");
        content.Should().Contain("BackofficeAccessMiddleware");
        content.Should().Contain("IPiiDataMasker");
        content.Should().Contain("Kill-Switch");

        // Assert Financial & Referral risk analysis and safeguards
        content.Should().Contain("Processamento Financeiro");
        content.Should().Contain("PCI-DSS");
        content.Should().Contain("AES-256-GCM");
        content.Should().Contain("Blind Indexing");
        content.Should().Contain("4-Eyes");
        content.Should().Contain("Quarentena");

        // Assert Risk matrix and DPO opinion
        content.Should().Contain("Matriz Comparativa Consolidada de Riscos");
        content.Should().Contain("Parecer Conclusivo do Encarregado pelo Tratamento de Dados (DPO)");
        content.Should().Contain("HOMOLOGADA E CONFORME");
    }

    [Fact]
    public void Adr0026_MustExist_AndFormalizeRopaAndRipdGovernance()
    {
        // Arrange
        var adr26Path = Path.Combine(_repoRoot, "docs", "adrs", "0026-lgpd-ropa-data-inventory-and-ripd-governance.md");

        // Assert existence
        File.Exists(adr26Path).Should().BeTrue("ADR 0026 must exist at docs/adrs/0026-lgpd-ropa-data-inventory-and-ripd-governance.md");

        var content = File.ReadAllText(adr26Path);

        content.Should().Contain("ADR 0026");
        content.Should().Contain("Aceito (Accepted)");
        content.Should().Contain("ROPA - Art. 37");
        content.Should().Contain("RIPD - Art. 38");
        content.Should().Contain("docs/compliance/ropa_data_inventory.md");
        content.Should().Contain("docs/compliance/ripd_dpia_overview.md");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "agents.md")) || 
                File.Exists(Path.Combine(current.FullName, "CriaCerto.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root from: " + AppContext.BaseDirectory);
    }
}
