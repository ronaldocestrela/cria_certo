namespace CriaCerto.Modules.Backoffice.Application.Features.Compliance.IncidentResponse;

/// <summary>
/// Níveis de severidade de incidentes de privacidade e segurança com dados pessoais
/// em conformidade com o Art. 48 da LGPD e a Resolução CD/ANPD nº 15/2024.
/// </summary>
public enum PrivacyIncidentSeverity
{
    /// <summary>
    /// Incidente pontual ou sem risco aos direitos dos titulares (ex: dados cifrados com AES-256-GCM sem comprometimento de chave).
    /// </summary>
    Low = 1,

    /// <summary>
    /// Potencial risco em investigação que requer análise casuística do DPO.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Dano relevante confirmado aos titulares (ex: exposição de credenciais ou PII em claro de múltiplos titulares).
    /// Comunicação compulsória à ANPD e aos titulares em até 3 dias úteis.
    /// </summary>
    High = 3,

    /// <summary>
    /// Incidente massivo, sistêmico ou crítico com risco iminente de prejuízo patrimonial, fraude ou dano moral irreversível.
    /// Comunicação imediata e mobilização de comitê de crise.
    /// </summary>
    Critical = 4
}
