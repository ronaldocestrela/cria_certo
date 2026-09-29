using System;
using System.Collections.Generic;

namespace CriaCerto.BuildingBlocks.Abstractions.Licensing;

public static class ModuleLicenseChecker
{
    public const string StarterPlan = "Starter";
    public const string ProPlan = "Pro";
    public const string EnterprisePlan = "Enterprise";

    private static readonly Dictionary<string, string> PlanAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Starter", StarterPlan },
        { "Starter Pecuária", StarterPlan },
        { "Starter Pecuaria", StarterPlan },
        { "Plano Starter", StarterPlan },
        { "Pro", ProPlan },
        { "Pro Fazenda", ProPlan },
        { "Plano Pro", ProPlan },
        { "Enterprise", EnterprisePlan },
        { "Enterprise Confinamento", EnterprisePlan },
        { "Plano Enterprise", EnterprisePlan }
    };

    private static readonly Dictionary<string, HashSet<string>> PlanAccess = new(StringComparer.OrdinalIgnoreCase)
    {
        { StarterPlan, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Breeding", "Calving", "Tenancy", "Cows" } },
        { ProPlan, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Breeding", "Calving", "Tenancy", "Nutrition", "Growth", "Cows" } },
        { EnterprisePlan, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Breeding", "Calving", "Tenancy", "Nutrition", "Sanitary", "Growth", "Feedlot", "Analytics", "Cows" } }
    };

    public static string NormalizeModule(string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return string.Empty;
        }

        var trimmed = module.Trim();
        if (trimmed.StartsWith("Modules.", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["Modules.".Length..];
        }

        if (string.Equals(trimmed, "cows", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "cow", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "cattle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "plantel", StringComparison.OrdinalIgnoreCase))
        {
            return "Cows";
        }

        return trimmed;
    }

    public static string NormalizePlan(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan))
        {
            return StarterPlan;
        }

        var trimmed = plan.Trim();

        if (PlanAliases.TryGetValue(trimmed, out var canonical))
        {
            return canonical;
        }

        // Heurística resiliente para variações de nomenclatura e versões de Backoffice (ex.: "Pro 2026.1")
        if (trimmed.Contains("Enterprise", StringComparison.OrdinalIgnoreCase))
        {
            return EnterprisePlan;
        }

        if (trimmed.Contains("Pro", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Profissional", StringComparison.OrdinalIgnoreCase))
        {
            return ProPlan;
        }

        if (trimmed.Contains("Starter", StringComparison.OrdinalIgnoreCase))
        {
            return StarterPlan;
        }

        return trimmed;
    }

    public static bool HasAccess(string? plan, string? module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return false;
        }

        var canonicalPlan = NormalizePlan(plan);

        if (string.Equals(canonicalPlan, EnterprisePlan, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedModule = NormalizeModule(module);

        if (!PlanAccess.TryGetValue(canonicalPlan, out var allowedModules))
        {
            // Fallback resiliente para planos ou revisões não canônicas: garante acesso aos módulos base (Starter)
            allowedModules = PlanAccess[StarterPlan];
        }

        if (allowedModules.Contains(module) || allowedModules.Contains(normalizedModule))
        {
            return true;
        }

        // Módulo de Matrizes / Plantel (Cows) e Reprodução (Breeding) compartilham reciprocidade de acesso
        if ((string.Equals(normalizedModule, "Cows", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(normalizedModule, "Breeding", StringComparison.OrdinalIgnoreCase)) &&
            (allowedModules.Contains("Breeding") || allowedModules.Contains("Cows")))
        {
            return true;
        }

        return false;
    }
}
