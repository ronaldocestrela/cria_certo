namespace CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo.Models;

/// <summary>
/// Qualificação do vínculo do titular de dados perante o CriaCerto Bovino SaaS.
/// </summary>
public enum DataSubjectRole
{
    /// <summary>Produtor rural, pecuarista ou administrador titular de granja.</summary>
    ProducerAdmin = 1,

    /// <summary>Colaborador de equipe operacional de fazenda (veterinário, zootecnista, vaqueiro).</summary>
    FarmTeamMember = 2,

    /// <summary>Parceiro comercial credenciado / participante de indicações.</summary>
    CommercialPartner = 3,

    /// <summary>Visitante, candidato a vaga ou usuário não cadastrado.</summary>
    VisitorOrCandidate = 4,

    /// <summary>Outra relação de contato ou terceiro interessado.</summary>
    Other = 5
}
