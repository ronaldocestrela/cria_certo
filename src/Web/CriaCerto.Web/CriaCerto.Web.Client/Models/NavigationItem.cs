namespace CriaCerto.Web.Client.Models;

public record NavigationItem(
    string Title,
    string ShortTitle,
    string Href,
    string Category = "Operações Bovinas",
    string? Icon = null,
    bool MatchExact = false
);

public static class NavigationCatalog
{
    private static readonly List<NavigationItem> Items = new()
    {
        new(
            Title: "Dashboard",
            ShortTitle: "Dashboard",
            Href: "analytics/executive-dashboard",
            Category: "Operações Bovinas",
            Icon: "dashboard",
            MatchExact: true
        ),
        new(
            Title: "Plantel Bovino",
            ShortTitle: "Plantel",
            Href: "breeding/registry",
            Category: "Operações Bovinas",
            Icon: "groups"
        ),
        new(
            Title: "Protocolos IATF",
            ShortTitle: "IATF",
            Href: "breeding/iatf",
            Category: "Operações Bovinas",
            Icon: "science"
        ),
        new(
            Title: "Diagnóstico de Gestação",
            ShortTitle: "Gestação",
            Href: "breeding/diagnostico",
            Category: "Operações Bovinas",
            Icon: "assignment_turned_in"
        ),
        new(
            Title: "Partos & Bezerreiro",
            ShortTitle: "Partos",
            Href: "calving/records",
            Category: "Operações Bovinas",
            Icon: "child_care"
        ),
        new(
            Title: "Pastos & Lotação UA/ha",
            ShortTitle: "Pastos & Lotação",
            Href: "growth/pastures",
            Category: "Operações Bovinas",
            Icon: "grass"
        ),
        new(
            Title: "Pesagem & Arrobas (@)",
            ShortTitle: "Pesagem & @",
            Href: "growth/curral-weighing",
            Category: "Operações Bovinas",
            Icon: "scale"
        ),
        new(
            Title: "Sanitário & Vacinas",
            ShortTitle: "Sanitário",
            Href: "sanitary/campaigns",
            Category: "Operações Bovinas",
            Icon: "vaccines"
        ),
        new(
            Title: "Silos & Estoque Insumos",
            ShortTitle: "Silos & Estoque",
            Href: "nutrition/silos",
            Category: "Operações Bovinas",
            Icon: "warehouse"
        ),
        new(
            Title: "Trato TMR Confinamento",
            ShortTitle: "Trato TMR",
            Href: "nutrition/trough-log",
            Category: "Operações Bovinas",
            Icon: "agriculture"
        ),
        new(
            Title: "Suplementação de Pasto",
            ShortTitle: "Suplementação",
            Href: "nutrition/pasture-supplementation",
            Category: "Operações Bovinas",
            Icon: "nutrition"
        ),
        new(
            Title: "Fazenda & Unidades",
            ShortTitle: "Fazenda",
            Href: "settings/organization",
            Category: "Configurações",
            Icon: "domain"
        ),
        new(
            Title: "Assinatura & Planos",
            ShortTitle: "Planos",
            Href: "settings/subscription",
            Category: "Configurações",
            Icon: "credit_card"
        )
    };

    public static IReadOnlyList<NavigationItem> GetAllItems() => Items.AsReadOnly();

    public static IEnumerable<NavigationItem> GetOperationalItems() =>
        Items.Where(i => i.Category == "Operações Bovinas");

    public static IEnumerable<NavigationItem> GetSettingsItems() =>
        Items.Where(i => i.Category == "Configurações");
}
