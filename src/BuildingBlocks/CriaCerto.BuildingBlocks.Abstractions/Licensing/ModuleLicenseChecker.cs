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

        if (string.Equals(trimmed, "tenancy", StringComparison.OrdinalIgnoreCase))
        {
            return "Tenancy";
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

    public static bool HasModuleAccess(IEnumerable<string>? subscribedModules, string? requiredModule)
    {
        var normalizedRequired = NormalizeModule(requiredModule);
        if (string.IsNullOrWhiteSpace(normalizedRequired))
        {
            return false;
        }

        // O módulo "Cows" (Plantel Base) e "Tenancy" são sempre permitidos para qualquer tenant ativo/em trial
        if (string.Equals(normalizedRequired, "Cows", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedRequired, "Tenancy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (subscribedModules is null)
        {
            return false;
        }

        foreach (var module in subscribedModules)
        {
            var normalizedSubscribed = NormalizeModule(module);
            if (string.Equals(normalizedSubscribed, normalizedRequired, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
