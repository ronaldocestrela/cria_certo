namespace CriaCerto.Web.Client.Models;

public record CookieConsentCategory(
    string Id,
    string Name,
    string Description,
    bool IsRequired,
    string StorageKeyOrTechnology
);

public class CookieConsentState
{
    public const string DefaultVersion = "1.0";
    public const int DefaultExpirationDays = 180;

    public bool IsAccepted { get; set; }
    public DateTime AcceptedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Version { get; set; } = DefaultVersion;

    public bool Essential { get; set; } = true;
    public bool OfflineStorage { get; set; } = true;
    public bool ReferralTracking { get; set; } = true;

    public bool IsValid(DateTime? currentUtcTime = null, string expectedVersion = DefaultVersion)
    {
        if (!IsAccepted)
        {
            return false;
        }

        if (Version != expectedVersion)
        {
            return false;
        }

        var now = currentUtcTime ?? DateTime.UtcNow;
        return ExpiresAtUtc > now;
    }

    public static CookieConsentState CreateDefaultAccepted(DateTime? nowUtc = null, int expirationDays = DefaultExpirationDays, string version = DefaultVersion)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        return new CookieConsentState
        {
            IsAccepted = true,
            AcceptedAtUtc = now,
            ExpiresAtUtc = now.AddDays(expirationDays),
            Version = version,
            Essential = true,
            OfflineStorage = true,
            ReferralTracking = true
        };
    }
}

public static class CookieConsentCatalog
{
    public const string StorageKey = "cria_certo_cookie_consent_v1";

    public static readonly IReadOnlyList<CookieConsentCategory> Categories = new List<CookieConsentCategory>
    {
        new(
            Id: "essential",
            Name: "Sessão e Segurança",
            Description: "Armazena credenciais e tokens JWT de autenticação para manter a sessão ativa com integridade e segurança.",
            IsRequired: true,
            StorageKeyOrTechnology: "localStorage (authToken, impersonation_session)"
        ),
        new(
            Id: "pwa_offline",
            Name: "PWA e Manejo Offline no Curral",
            Description: "Permite o registro contínuo de pesagens, manejos sanitários e IATF no pasto ou curral sem conexão com a internet.",
            IsRequired: true,
            StorageKeyOrTechnology: "IndexedDB & localStorage (offline_animals, offline_movements)"
        ),
        new(
            Id: "referral",
            Name: "Atribuição de Parceiro Comercial",
            Description: "Registra a indicação de parceiros autorizados para aplicação de benefícios comerciais e descontos na assinatura.",
            IsRequired: false,
            StorageKeyOrTechnology: "localStorage (cc_referral_code)"
        )
    };
}
