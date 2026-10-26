namespace CriaCerto.Web.Client.Models;

public record LegalSectionItem(string Id, string Title, string Summary);

public record DpoContactInfo(
    string Name,
    string Role,
    string Email,
    int MaxResponseDays,
    string PhysicalAddress
);

public record LegalDocumentItem(
    string Title,
    string Route,
    string Version,
    string EffectiveDate,
    string Summary,
    IReadOnlyList<LegalSectionItem> Sections
);

public static class LegalDocumentCatalog
{
    private static readonly DpoContactInfo DpoInfo = new(
        Name: "Encarregado pelo Tratamento de Dados Pessoais (DPO)",
        Role: "DPO Oficial CriaCerto Bovino SaaS",
        Email: "privacidade@criacerto.com.br",
        MaxResponseDays: 15,
        PhysicalAddress: "Av. do Agronegócio, 1500 - Cuiabá/MT - CEP 78000-000"
    );

    private static readonly List<LegalSectionItem> TermsSections = new()
    {
        new("objeto-licenca", "1. Objeto e Licenciamento de Uso", "Condições de acesso e concessão de licença SaaS para gestão pecuária."),
        new("pwa-offline", "2. Operação PWA Offline e Sincronização", "Diretrizes de uso no mangueiro/curral sem internet e sincronização de dados."),
        new("responsabilidade-zootecnica-sanitaria", "3. Responsabilidade Zootécnica e Sanitária", "Ferramentas de auxílio à decisão, trava de carência e autonomia veterinária."),
        new("controlador-operador-termos", "4. Papéis LGPD e Dados de Terceiros", "Produtor como Controlador dos dados da fazenda e CriaCerto como Operadora."),
        new("propriedade-dados", "5. Propriedade Intelectual e Propriedade dos Dados", "Titularidade absoluta dos dados zootécnicos do produtor e portabilidade."),
        new("sla-rescisao", "6. SLA, Suporte, Suspensão e Rescisão", "Garantias de continuidade, transição de dados e carência contratual.")
    };

    private static readonly List<LegalSectionItem> PrivacySections = new()
    {
        new("controlador-operador", "1. Papéis sob a LGPD (Controlador e Operador)", "Definição de escopo conforme Art. 5º da Lei Geral de Proteção de Dados."),
        new("dados-coletados", "2. Categorias de Dados Coletados", "Mapeamento de dados cadastrais, da fazenda, do rebanho e técnicos."),
        new("bases-legais", "3. Bases Legais de Tratamento", "Enquadramento nos artigos 7º e 11 da LGPD para cada finalidade."),
        new("subprocessadores", "4. Compartilhamento e Subprocessadores", "Parceiros essenciais de infraestrutura, cobrança (Stripe) e e-mails (Resend)."),
        new("seguranca-dados", "5. Medidas de Segurança da Informação", "Mascaramento PII, hash forense SHA-256 e criptografia em repouso e trânsito."),
        new("retencao-descarte", "6. Retenção e Descarte de Dados", "Ciclo de vida dos dados e prazos legais regulatórios do agronegócio."),
        new("direitos-titular", "7. Direitos do Titular (Art. 18)", "Mecanismos para requisição de acesso, correção, anonimização e exclusão."),
        new("canal-dpo", "8. Canal Oficial do DPO e Atendimento ANPD", "Informações de contato e prazos regulamentares de atendimento.")
    };

    private static readonly List<LegalDocumentItem> Documents = new()
    {
        new(
            Title: "Termos de Uso da Plataforma",
            Route: "termos-de-uso",
            Version: "1.0",
            EffectiveDate: "26 de Outubro de 2026",
            Summary: "Condições gerais de licenciamento, uso de módulos agropecuários e responsabilidades no CriaCerto.",
            Sections: TermsSections
        ),
        new(
            Title: "Política de Privacidade e Proteção de Dados",
            Route: "politica-de-privacidade",
            Version: "1.0",
            EffectiveDate: "26 de Outubro de 2026",
            Summary: "Normas de transparência, tratamento de dados de produtores rurais e direitos dos titulares conforme a LGPD.",
            Sections: PrivacySections
        )
    };

    public static IReadOnlyList<LegalDocumentItem> GetAllDocuments() => Documents.AsReadOnly();

    public static LegalDocumentItem? GetByRoute(string route)
    {
        var cleanRoute = route.TrimStart('/');
        return Documents.FirstOrDefault(d => d.Route.Equals(cleanRoute, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<LegalSectionItem> GetTermsOfServiceSections() => TermsSections.AsReadOnly();

    public static IReadOnlyList<LegalSectionItem> GetPrivacyPolicySections() => PrivacySections.AsReadOnly();

    public static DpoContactInfo GetDpoContactInfo() => DpoInfo;
}
