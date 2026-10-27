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

    private static readonly List<LegalSectionItem> DpaSections = new()
    {
        new("objeto-escopo", "1. Objeto, Escopo e Qualificação das Partes", "Definição do Produtor como Controlador e CriaCerto como Operador para dados de colaboradores e convites."),
        new("instrucoes-licitas", "2. Instruções Lícitas do Controlador", "Tratamento restrito às instruções documentadas e para as finalidades do SaaS de pecuária."),
        new("salvaguardas-seguranca", "3. Salvaguardas Técnicas e Administrativas", "Criptografia AES-256-GCM, blind indexing, isolamento lógico e trilha forense SHA-256."),
        new("confidencialidade", "4. Dever de Confidencialidade da Equipe", "Compromisso de sigilo formal e capacitação contínua da equipe interna CriaCerto."),
        new("subprocessadores", "5. Subprocessadores Autorizados", "Relação com Stripe, Resend e Cloud, com garantias contratuais de proteção equivalente."),
        new("cooperacao-titulares", "6. Cooperação no Atendimento aos Titulares", "Mecanismos para auxílio à fazenda no cumprimento dos direitos dos funcionários (Art. 18)."),
        new("incidentes-seguranca", "7. Notificação e Gestão de Incidentes", "Procedimento e prazo célere de comunicação de incidentes relevantes com dados à fazenda."),
        new("rescisao-descarte", "8. Rescisão Contratual e Descarte Seguro", "Carência de 30 dias para exportação de dados zootécnicos e expurgo irreversível posterior."),
        new("auditoria-conformidade", "9. Relatórios de Conformidade e Auditoria", "Disponibilização de atestados técnicos e sumários de segurança da informação.")
    };

    private static readonly List<LegalSectionItem> DpoSections = new()
    {
        new("identidade-encarregado", "1. Identidade e Qualificação do DPO", "Designação formal e contatos institucionais conforme o Art. 41 da Lei nº 13.709/2018."),
        new("atribuicoes-dpo", "2. Atribuições do Encarregado (Art. 41 § 2º)", "Canal de comunicação com titulares, orientações operacionais e relacionamento com a ANPD."),
        new("direitos-titulares", "3. Catálogo de Direitos dos Titulares (Art. 18)", "Exercício de acesso, retificação, anonimização, portabilidade e revogação de consentimento."),
        new("prazos-atendimento", "4. Prazos Regulamentares de Resposta", "Atendimento fundamentado em até 15 dias úteis (Art. 19, II) com emissão de protocolo seguro."),
        new("formulario-dsr", "5. Formulário Eletrônico de Atendimento (DSR)", "Submissão direta de requisição formal com recibo e rastreamento.")
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
        ),
        new(
            Title: "Aditivo de Processamento de Dados Pessoais (DPA)",
            Route: "dpa",
            Version: "1.0",
            EffectiveDate: "26 de Outubro de 2026",
            Summary: "Termo de Processamento de Dados (Data Processing Agreement) regendo as obrigações do CriaCerto como Operador dos dados de colaboradores da fazenda.",
            Sections: DpaSections
        ),
        new(
            Title: "Canal Oficial do Encarregado de Dados (DPO)",
            Route: "dpo",
            Version: "1.0",
            EffectiveDate: "26 de Outubro de 2026",
            Summary: "Canal oficial de comunicação do Encarregado pelo Tratamento de Dados Pessoais (DPO - LGPD Art. 41) e submissão formal de requisições de titulares.",
            Sections: DpoSections
        )
    };

    public static IReadOnlyList<LegalDocumentItem> GetAllDocuments() => Documents.AsReadOnly();

    public static LegalDocumentItem? GetByRoute(string route)
    {
        var cleanRoute = route.TrimStart('/');
        if (cleanRoute.Equals("aditivo-processamento-dados", StringComparison.OrdinalIgnoreCase))
        {
            cleanRoute = "dpa";
        }
        else if (cleanRoute.Equals("canal-dpo", StringComparison.OrdinalIgnoreCase) ||
                 cleanRoute.Equals("privacidade/canal-dpo", StringComparison.OrdinalIgnoreCase) ||
                 cleanRoute.Equals("privacidade/dpo", StringComparison.OrdinalIgnoreCase))
        {
            cleanRoute = "dpo";
        }
        return Documents.FirstOrDefault(d => d.Route.Equals(cleanRoute, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<LegalSectionItem> GetTermsOfServiceSections() => TermsSections.AsReadOnly();

    public static IReadOnlyList<LegalSectionItem> GetPrivacyPolicySections() => PrivacySections.AsReadOnly();

    public static IReadOnlyList<LegalSectionItem> GetDataProcessingAgreementSections() => DpaSections.AsReadOnly();

    public static IReadOnlyList<LegalSectionItem> GetDpoSections() => DpoSections.AsReadOnly();

    public static DpoContactInfo GetDpoContactInfo() => DpoInfo;
}
