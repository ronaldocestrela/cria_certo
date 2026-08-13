namespace CriaCerto.BuildingBlocks.Infrastructure.Persistence;

public sealed record MigrationBaselineModule(
    string Schema,
    string MigrationId,
    IReadOnlyList<string> RequiredTables);

public static class MigrationBaselineMetadata
{
    public const string ProductVersion = "10.0.0";
    public const string HistoryTableName = SqlServerMigrationExtensions.HistoryTableName;

    public static readonly MigrationBaselineModule Foundation = new(
        "foundation",
        "20260813173509_InitialCreate",
        ["bovine_breeds"]);

    public static readonly MigrationBaselineModule Tenancy = new(
        "tenancy",
        "20260813173518_InitialCreate",
        ["Tenants", "Users", "ProductionUnits", "TeamInvites", "UserTenants"]);

    public static readonly MigrationBaselineModule Backoffice = new(
        "backoffice",
        "20260813173522_InitialCreate",
        ["AdminRoles", "AdminSessions", "AdminUsers", "AuditLogs", "Permissions", "AdminRoleAdminUser", "AdminRolePermission"]);

    public static readonly MigrationBaselineModule Breeding = new(
        "breeding",
        "20260813173527_InitialCreate",
        ["Bulls", "Cows", "IatfProtocols", "PregnancyDiagnoses", "SemenBatches"]);

    public static readonly MigrationBaselineModule Calving = new(
        "calving",
        "20260813173531_InitialCreate",
        ["Calves", "Calvings", "Weanings"]);

    public static readonly MigrationBaselineModule Growth = new(
        "growth",
        "20260813173539_InitialCreate",
        ["LotMovements", "Lots", "PasturePaddocks", "weighings"]);

    public static readonly MigrationBaselineModule Nutrition = new(
        "nutrition",
        "20260813173543_InitialCreate",
        ["DailyFeedBatches", "FeedRations", "PastureSupplementations", "SiloStocks", "FeedRationItems"]);

    public static readonly MigrationBaselineModule Sanitary = new(
        "sanitary",
        "20260813173548_InitialCreate",
        ["treatment_records", "vaccination_campaigns", "vaccine_references"]);

    public static IReadOnlyList<MigrationBaselineModule> MasterModules { get; } =
    [
        Foundation,
        Tenancy,
        Backoffice,
        Breeding,
        Calving,
        Growth,
        Nutrition,
        Sanitary
    ];

    public static IReadOnlyList<MigrationBaselineModule> TenantModules { get; } =
    [
        Breeding,
        Calving,
        Growth,
        Nutrition,
        Sanitary
    ];

    public static MigrationBaselineModule? FindBySchema(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return null;
        }

        return MasterModules.FirstOrDefault(module =>
            string.Equals(module.Schema, schema, StringComparison.OrdinalIgnoreCase));
    }
}
