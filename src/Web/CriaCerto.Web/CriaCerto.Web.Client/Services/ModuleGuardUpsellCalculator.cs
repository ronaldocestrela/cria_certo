using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CriaCerto.Web.Client.Services;

public record ModuleGuardFeature(string Title, string Description, string Icon, string BgColor, string Color);

public record ModuleUpsellInfo(
    string ModuleKey,
    string ModuleName,
    string Description,
    string Icon,
    string TierCode,
    string TierName,
    int MaxHeads,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    string ValueProposition,
    IReadOnlyList<ModuleGuardFeature> Features
);

public static class ModuleGuardUpsellCalculator
{
    private static readonly CultureInfo PtBrCulture = new("pt-BR");

    public static string ResolveCommercialModuleKey(string? rawModule)
    {
        if (string.IsNullOrWhiteSpace(rawModule))
        {
            return "Cows";
        }

        var normalized = rawModule.Trim();
        if (normalized.StartsWith("Modules.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Modules.".Length..];
        }

        return normalized.ToLowerInvariant() switch
        {
            "cows" or "cow" or "cattle" or "plantel" or "matrizes" => "Cows",
            "breeding" or "reprodução" or "reproducao" or "iatf" => "Breeding",
            "calving" or "partos" or "maternidade" or "maternity" or "bezerros" or "bezerreiro" => "Calving",
            "growth" or "manejo" or "pesagem" or "recria" or "engorda" or "pastos" or "paddock" => "Growth",
            "sanitary" or "sanidade" or "vacinas" or "campanhas" or "carência" or "carencia" => "Sanitary",
            "nutrition" or "nutrição" or "nutricao" or "feedlot" or "confinamento" or "tmr" or "trato" or "silos" => "Nutrition",
            "analytics" or "indicadores" or "zootecnia" or "dashboard" => "Analytics",
            _ => normalized
        };
    }

    public static ModuleUpsellInfo ResolveUpsellInfo(
        ModularSubscriptionCatalogModel catalog,
        string rawModuleKey,
        string? headTierCode)
    {
        var commercialKey = ResolveCommercialModuleKey(rawModuleKey);
        var activeTierCode = string.IsNullOrWhiteSpace(headTierCode) ? "TIER_500" : headTierCode.Trim();

        var tier = catalog.HeadTiers.FirstOrDefault(t => string.Equals(t.TierCode, activeTierCode, StringComparison.OrdinalIgnoreCase))
                   ?? catalog.HeadTiers.FirstOrDefault()
                   ?? new HeadTierOptionModel("TIER_500", "Até 500 cabeças", 1, 500, 1);

        var moduleOption = catalog.Modules.FirstOrDefault(m => string.Equals(m.ModuleKey, commercialKey, StringComparison.OrdinalIgnoreCase))
                           ?? catalog.Modules.FirstOrDefault(m => string.Equals(m.ModuleKey, "Nutrition", StringComparison.OrdinalIgnoreCase))
                           ?? new CommercialModuleOptionModel(
                               commercialKey,
                               GetReadableModuleName(commercialKey),
                               GetModuleValueProposition(commercialKey),
                               "extension",
                               false,
                               99,
                               new List<ModuleTierPriceOptionModel> { new(tier.TierCode, 59.00m, 50.15m) });

        var priceOption = moduleOption.PricesByTier.FirstOrDefault(p => string.Equals(p.TierCode, tier.TierCode, StringComparison.OrdinalIgnoreCase))
                          ?? moduleOption.PricesByTier.FirstOrDefault()
                          ?? new ModuleTierPriceOptionModel(tier.TierCode, 59.00m, 50.15m);

        return new ModuleUpsellInfo(
            ModuleKey: moduleOption.ModuleKey,
            ModuleName: moduleOption.Name,
            Description: moduleOption.Description,
            Icon: moduleOption.IconName,
            TierCode: tier.TierCode,
            TierName: tier.DisplayName,
            MaxHeads: tier.MaxHeads,
            MonthlyPrice: priceOption.MonthlyPrice,
            AnnualPriceMonthly: priceOption.AnnualPriceMonthly,
            ValueProposition: GetModuleValueProposition(commercialKey),
            Features: GetModuleFeatures(commercialKey)
        );
    }

    public static string FormatUpsellCallout(ModuleUpsellInfo upsell)
    {
        var formattedPrice = upsell.MonthlyPrice.ToString("N2", PtBrCulture);
        var formattedHeads = upsell.MaxHeads.ToString("N0", PtBrCulture);

        return $"Adicione o Módulo de {upsell.ModuleName} por + R$ {formattedPrice}/mês (para sua faixa atual de até {formattedHeads} cabeças)";
    }

    public static List<string> BuildUpdatedModuleKeys(IEnumerable<string>? currentModules, string newModuleKey)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows" };

        if (currentModules != null)
        {
            foreach (var mod in currentModules)
            {
                var canonical = ResolveCommercialModuleKey(mod);
                if (!string.IsNullOrWhiteSpace(canonical))
                {
                    result.Add(canonical);
                }
            }
        }

        var normalizedNew = ResolveCommercialModuleKey(newModuleKey);
        if (!string.IsNullOrWhiteSpace(normalizedNew))
        {
            result.Add(normalizedNew);
        }

        return result.ToList();
    }

    public static string GetReadableModuleName(string moduleKey)
    {
        return ResolveCommercialModuleKey(moduleKey) switch
        {
            "Cows" => "Gestão do Plantel & Matrizes",
            "Breeding" => "Reprodução & IATF",
            "Calving" => "Maternidade & Bezerreiro",
            "Growth" => "Recria & Engorda",
            "Sanitary" => "Sanitário & Carência",
            "Nutrition" => "Nutrição & Confinamento",
            "Analytics" => "Zootecnia & Analytics",
            _ => moduleKey
        };
    }

    public static string GetModuleValueProposition(string moduleKey)
    {
        return ResolveCommercialModuleKey(moduleKey) switch
        {
            "Cows" => "Gerencie matrizes, reprodutores, genealogia, escore corporal e identificação individual de animais com total rastreabilidade zootécnica.",
            "Breeding" => "Monitore protocolos completos de IATF, diagnósticos de gestação precoces, taxas de prenhez e histórico reprodutivo individual.",
            "Calving" => "Acompanhe nascimentos, peso ajustado aos 205 dias (P205), taxas de desmama e prevenção de mortalidade pré-desmama na maternidade.",
            "Growth" => "Controle ganho de peso diário (GPD), pesagens em balança de curral, evolução de arrobas (@) e taxa de lotação UA/ha por piquete.",
            "Sanitary" => "Planeje campanhas obrigatórias e voluntárias de vacinação, histórico clínico de tratamentos e bloqueio automático de carência para abate.",
            "Nutrition" => "Desbloqueie formulação de rações, controle de silagem e insumos, registro de bateladas diárias TMR, leitura de cocho e custo por kg e @ produzida.",
            "Analytics" => "Acesse painéis executivos com gráficos zootécnicos avançados, estimativa de IEP, benchmarking e indicadores consolidados de rentabilidade.",
            _ => "Otimize o manejo e a produtividade da sua fazenda ativando este módulo add-on sob medida para o seu rebanho."
        };
    }

    public static IReadOnlyList<ModuleGuardFeature> GetModuleFeatures(string moduleKey)
    {
        return ResolveCommercialModuleKey(moduleKey) switch
        {
            "Nutrition" => new List<ModuleGuardFeature>
            {
                new("Trato TMR & Cocho", "Registro digital de bateladas e escore diário de cocho em tempo real.", "agriculture", "#eff7ef", "#2e5f46"),
                new("Conversão Alimentar", "Cálculo de consumo em matéria seca (MS) e eficiência de ganho de peso.", "monitoring", "#ffdbd0", "#832600"),
                new("Custo por @ Produzida", "Rentabilidade precisa em reais por arroba no período de confinamento e pasto.", "payments", "#d8e3fb", "#111c2d")
            },
            "Breeding" => new List<ModuleGuardFeature>
            {
                new("Protocolos IATF", "Planejamento e acompanhamento das etapas de inseminação artificial com lotes.", "science", "#eff7ef", "#2e5f46"),
                new("Diagnóstico Gestacional", "Confirmação de prenhez via ultrassom ou palpação com dias em aberto.", "assignment_turned_in", "#ffdbd0", "#832600"),
                new("IEP & Taxa de Concepção", "Indicadores de fertilidade e intervalo entre partos projetado.", "favorite", "#d8e3fb", "#111c2d")
            },
            "Calving" => new List<ModuleGuardFeature>
            {
                new("Registro de Nascimentos", "Peso ao nascer, sexo, identificação de mãe e vigor do bezerro.", "child_care", "#eff7ef", "#2e5f46"),
                new("Desmama Ajustada P205", "Cálculo automático de peso padronizado aos 205 dias.", "scale", "#ffdbd0", "#832600"),
                new("Mortalidade & Sobrevivência", "Controle de sanidade e índice de desmama por matriz.", "health_and_safety", "#d8e3fb", "#111c2d")
            },
            "Growth" => new List<ModuleGuardFeature>
            {
                new("Pesagem Rápida em Curral", "Entrada ágil de peso com cálculo imediato de GPD.", "speed", "#eff7ef", "#2e5f46"),
                new("Lotação UA/ha", "Controle de suporte de pasto e rotação de piquetes em tempo real.", "grass", "#ffdbd0", "#832600"),
                new("Curvas de Engorda", "Projeção de ponto de abate e ganho de arrobas (@).", "trending_up", "#d8e3fb", "#111c2d")
            },
            "Sanitary" => new List<ModuleGuardFeature>
            {
                new("Campanhas de Vacinação", "Cronograma de Febre Aftosa, Brucelose, Raiva e Clostridiose.", "vaccines", "#eff7ef", "#2e5f46"),
                new("Bloqueio de Carência", "Prevenção de envio ao frigorífico de animais sob efeito medicamentoso.", "block", "#ffdbd0", "#832600"),
                new("Histórico Clínico", "Rastreabilidade de lotes e tratamentos veterinários individuais.", "medical_services", "#d8e3fb", "#111c2d")
            },
            "Analytics" => new List<ModuleGuardFeature>
            {
                new("Dashboard Executivo", "Gráficos zootécnicos interativos e visão consolidada da fazenda.", "dashboard", "#eff7ef", "#2e5f46"),
                new("Benchmarking & Metas", "Comparação de índices reprodutivos e de engorda contra metas do setor.", "insights", "#ffdbd0", "#832600"),
                new("Exportação & Relatórios", "Relatórios executivos e operacionais para tomada de decisão ágil.", "analytics", "#d8e3fb", "#111c2d")
            },
            _ => new List<ModuleGuardFeature>
            {
                new("Gestão Especializada", "Ferramentas dedicadas e otimizadas para a rotina no campo.", "verified", "#eff7ef", "#2e5f46"),
                new("PWA Offline em Curral", "Opere no campo sem internet com sincronização automática.", "wifi_off", "#ffdbd0", "#832600")
            }
        };
    }
}
