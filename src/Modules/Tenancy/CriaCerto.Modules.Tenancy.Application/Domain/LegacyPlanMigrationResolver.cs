using System;
using System.Collections.Generic;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;

namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed record LegacyPlanMigrationDefinition(
    string CanonicalPlan,
    string HeadTierCode,
    int Capacity,
    string SizeSegment,
    IReadOnlyList<string> ModuleKeys);

public static class LegacyPlanMigrationResolver
{
    private static readonly IReadOnlyList<string> StarterModules =
    [
        "Cows",
        "Breeding",
        "Calving"
    ];

    private static readonly IReadOnlyList<string> ProModules =
    [
        "Cows",
        "Breeding",
        "Calving",
        "Growth",
        "Nutrition"
    ];

    private static readonly IReadOnlyList<string> EnterpriseModules =
    [
        "Cows",
        "Breeding",
        "Calving",
        "Growth",
        "Sanitary",
        "Nutrition",
        "Analytics"
    ];

    public static LegacyPlanMigrationDefinition ResolveLegacyPlan(string? plan, int? currentCapacity = null)
    {
        var normalized = ModuleLicenseChecker.NormalizePlan(plan);

        return normalized switch
        {
            ModuleLicenseChecker.EnterprisePlan => new LegacyPlanMigrationDefinition(
                CanonicalPlan: ModuleLicenseChecker.EnterprisePlan,
                HeadTierCode: HeadTierCapacityLimits.Tier5000Plus,
                Capacity: HeadTierCapacityLimits.Tier5000PlusMaxHeads,
                SizeSegment: TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(HeadTierCapacityLimits.Tier5000PlusMaxHeads),
                ModuleKeys: EnterpriseModules),

            ModuleLicenseChecker.ProPlan => new LegacyPlanMigrationDefinition(
                CanonicalPlan: ModuleLicenseChecker.ProPlan,
                HeadTierCode: HeadTierCapacityLimits.Tier3000,
                Capacity: HeadTierCapacityLimits.Tier3000MaxHeads,
                SizeSegment: TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(HeadTierCapacityLimits.Tier3000MaxHeads),
                ModuleKeys: ProModules),

            _ => new LegacyPlanMigrationDefinition(
                CanonicalPlan: ModuleLicenseChecker.StarterPlan,
                HeadTierCode: HeadTierCapacityLimits.Tier500,
                Capacity: HeadTierCapacityLimits.Tier500MaxHeads,
                SizeSegment: TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(HeadTierCapacityLimits.Tier500MaxHeads),
                ModuleKeys: StarterModules)
        };
    }
}

