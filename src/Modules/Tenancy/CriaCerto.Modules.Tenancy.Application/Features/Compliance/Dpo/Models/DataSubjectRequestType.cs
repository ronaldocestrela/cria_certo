namespace CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo.Models;

/// <summary>
/// Catálogo dos tipos de requisições de titulares fundamentadas no Artigo 18 da LGPD.
/// </summary>
public enum DataSubjectRequestType
{
    /// <summary>Confirmação da existência de tratamento e acesso integral aos dados (Art. 18, I e II).</summary>
    AccessAndConfirmation = 1,

    /// <summary>Correção de dados incompletos, inexatos ou desatualizados (Art. 18, III).</summary>
    Rectification = 2,

    /// <summary>Anonimização, bloqueio ou eliminação de dados desnecessários ou tratados em desconformidade (Art. 18, IV e VI).</summary>
    AnonymizationOrDeletion = 3,

    /// <summary>Portabilidade dos dados a outro fornecedor de serviço (Art. 18, V).</summary>
    Portability = 4,

    /// <summary>Informação das entidades públicas e privadas com as quais houve compartilhamento de dados (Art. 18, VII).</summary>
    ThirdPartySharingInfo = 5,

    /// <summary>Informação sobre a possibilidade de não fornecer consentimento e revogação deste (Art. 18, VIII e IX).</summary>
    ConsentRevocation = 6,

    /// <summary>Dúvidas gerais sobre privacidade, peticionamento à ANPD ou atendimento direto pelo DPO (Art. 41).</summary>
    GeneralInquiryOrAnpdIssue = 7
}
