using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Seeders;

public sealed record MasterAdminOptions(
    string? Email = null,
    string? Password = null,
    string? Name = null);

public static class BackofficeDataSeeder
{
    public const string DefaultMasterAdminEmail = "admin@criacerto.com.br";
    public const string DefaultMasterAdminName = "Administrador Mestre";
    public const string DefaultMasterAdminPassword = "AdminPassword123!";

    public const string MasterAdminEmail = DefaultMasterAdminEmail;
    public const string MasterAdminName = DefaultMasterAdminName;
    public const string MasterAdminPassword = DefaultMasterAdminPassword;

    public static Task SeedAsync(
        BackofficeDbContext dbContext,
        IPasswordHasherService passwordHasher,
        ILogger? logger,
        bool resetBootstrapAdminPassword,
        CancellationToken cancellationToken) =>
        SeedAsync(dbContext, passwordHasher, logger, resetBootstrapAdminPassword, null, cancellationToken);

    public static async Task SeedAsync(
        BackofficeDbContext dbContext,
        IPasswordHasherService passwordHasher,
        ILogger? logger = null,
        bool resetBootstrapAdminPassword = false,
        MasterAdminOptions? masterAdminOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        var iamResult = await SeedIamAsync(
            dbContext,
            passwordHasher,
            resetBootstrapAdminPassword,
            masterAdminOptions,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var plansCreated = await SeedPlansAsync(dbContext, logger, cancellationToken);
        var (tiersCreated, modulesCreated) = await SeedModularCatalogAsync(dbContext, logger, cancellationToken);
        var flagsCreated = await SeedFeatureFlagsAsync(dbContext, logger, cancellationToken);

        if (plansCreated > 0 || tiersCreated > 0 || modulesCreated > 0 || flagsCreated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger?.LogInformation(
            "Backoffice seed completed. PermissionsCreated={PermissionsCreated}, RolesCreated={RolesCreated}, AdminCreated={AdminCreated}, AdminRoleRepaired={AdminRoleRepaired}, AdminPasswordReset={AdminPasswordReset}, PlansCreated={PlansCreated}, TiersCreated={TiersCreated}, ModulesCreated={ModulesCreated}, FlagsCreated={FlagsCreated}.",
            iamResult.PermissionsCreated,
            iamResult.RolesCreated,
            iamResult.AdminCreated,
            iamResult.AdminRoleRepaired,
            iamResult.AdminPasswordReset,
            plansCreated,
            tiersCreated,
            modulesCreated,
            flagsCreated);
    }

    public static Task<IamSeedResult> SeedIamAsync(
        BackofficeDbContext dbContext,
        IPasswordHasherService passwordHasher,
        bool resetBootstrapAdminPassword,
        CancellationToken cancellationToken) =>
        SeedIamAsync(dbContext, passwordHasher, resetBootstrapAdminPassword, null, cancellationToken);

    public static async Task<IamSeedResult> SeedIamAsync(
        BackofficeDbContext dbContext,
        IPasswordHasherService passwordHasher,
        bool resetBootstrapAdminPassword = false,
        MasterAdminOptions? masterAdminOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        var (permissionsCreated, permissionsMap) = await SeedPermissionsAsync(dbContext, cancellationToken);
        var (rolesCreated, rolesMap) = await SeedRolesAsync(dbContext, permissionsMap, cancellationToken);
        var (adminCreated, adminRoleRepaired, adminPasswordReset) = await SeedMasterAdminAsync(
            dbContext,
            passwordHasher,
            rolesMap,
            resetBootstrapAdminPassword,
            masterAdminOptions,
            cancellationToken);

        return new IamSeedResult(
            permissionsCreated,
            rolesCreated,
            adminCreated,
            adminRoleRepaired,
            adminPasswordReset);
    }

    public sealed record IamSeedResult(
        int PermissionsCreated,
        int RolesCreated,
        bool AdminCreated,
        bool AdminRoleRepaired,
        bool AdminPasswordReset);

    private static async Task<(int Created, Dictionary<string, Permission> PermissionsMap)> SeedPermissionsAsync(
        BackofficeDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingPermissions = await dbContext.Permissions.ToListAsync(cancellationToken);
        var permissionsMap = existingPermissions.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var created = 0;

        foreach (var permName in BackofficePermissions.AllPermissions)
        {
            if (permissionsMap.ContainsKey(permName))
            {
                continue;
            }

            var permResult = Permission.Create(
                name: permName,
                description: GetPermissionDescription(permName),
                scope: BackofficePermissions.ScopeGlobal);

            if (permResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Failed to create backoffice permission '{permName}': {permResult.Error.Message}");
            }

            dbContext.Permissions.Add(permResult.Value);
            permissionsMap[permName] = permResult.Value;
            created++;
        }

        return (created, permissionsMap);
    }

    private static async Task<(int Created, Dictionary<string, AdminRole> RolesMap)> SeedRolesAsync(
        BackofficeDbContext dbContext,
        IReadOnlyDictionary<string, Permission> permissionsMap,
        CancellationToken cancellationToken)
    {
        var existingRoles = await dbContext.AdminRoles
            .Include(r => r.Permissions)
            .ToListAsync(cancellationToken);
        var rolesMap = existingRoles.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        var created = 0;

        foreach (var roleName in BackofficeRoles.AllRoles)
        {
            if (!rolesMap.TryGetValue(roleName, out var role))
            {
                var roleResult = AdminRole.Create(
                    name: roleName,
                    description: GetRoleDescription(roleName));

                if (roleResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Failed to create backoffice role '{roleName}': {roleResult.Error.Message}");
                }

                role = roleResult.Value;
                dbContext.AdminRoles.Add(role);
                rolesMap[roleName] = role;
                created++;
            }

            var defaultPermNames = BackofficeRoles.GetDefaultPermissionsForRole(roleName);
            foreach (var permName in defaultPermNames)
            {
                if (!permissionsMap.TryGetValue(permName, out var permEntity))
                {
                    throw new InvalidOperationException(
                        $"Missing permission '{permName}' required by role '{roleName}'.");
                }

                var addResult = role.AddPermission(permEntity);
                if (addResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Failed to assign permission '{permName}' to role '{roleName}': {addResult.Error.Message}");
                }
            }
        }

        return (created, rolesMap);
    }

    private static async Task<(bool AdminCreated, bool AdminRoleRepaired, bool AdminPasswordReset)> SeedMasterAdminAsync(
        BackofficeDbContext dbContext,
        IPasswordHasherService passwordHasher,
        IReadOnlyDictionary<string, AdminRole> rolesMap,
        bool resetBootstrapAdminPassword,
        MasterAdminOptions? masterAdminOptions,
        CancellationToken cancellationToken)
    {
        if (!rolesMap.TryGetValue(BackofficeRoles.PlatformOwner, out var platformOwnerRole))
        {
            throw new InvalidOperationException(
                $"Required role '{BackofficeRoles.PlatformOwner}' was not seeded.");
        }

        var effectiveEmail = !string.IsNullOrWhiteSpace(masterAdminOptions?.Email)
            ? masterAdminOptions.Email.Trim()
            : DefaultMasterAdminEmail;

        var effectivePassword = !string.IsNullOrWhiteSpace(masterAdminOptions?.Password)
            ? masterAdminOptions.Password
            : DefaultMasterAdminPassword;

        var effectiveName = !string.IsNullOrWhiteSpace(masterAdminOptions?.Name)
            ? masterAdminOptions.Name.Trim()
            : DefaultMasterAdminName;

        var normalizedEmail = effectiveEmail.ToLowerInvariant();
        var existingAdmin = await dbContext.AdminUsers
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (existingAdmin is not null)
        {
            var adminRoleRepaired = false;
            var adminPasswordReset = false;

            if (!existingAdmin.IsActive)
            {
                existingAdmin.Activate();
            }

            var hasPlatformOwner = existingAdmin.Roles.Any(r =>
                r.Name.Equals(BackofficeRoles.PlatformOwner, StringComparison.OrdinalIgnoreCase));

            if (!hasPlatformOwner)
            {
                var assignResult = existingAdmin.AssignRole(platformOwnerRole);
                if (assignResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Failed to repair bootstrap admin role: {assignResult.Error.Message}");
                }

                adminRoleRepaired = true;
            }

            if (resetBootstrapAdminPassword)
            {
                var passwordHash = passwordHasher.HashPassword(effectivePassword);
                var updateResult = existingAdmin.UpdatePasswordHash(passwordHash);
                if (updateResult.IsFailure)
                {
                    throw new InvalidOperationException(
                        $"Failed to reset bootstrap admin password: {updateResult.Error.Message}");
                }

                adminPasswordReset = true;
            }

            return (false, adminRoleRepaired, adminPasswordReset);
        }

        var newPasswordHash = passwordHasher.HashPassword(effectivePassword);
        var userResult = AdminUser.Create(
            name: effectiveName,
            email: effectiveEmail,
            passwordHash: newPasswordHash,
            mustChangePasswordOnNextLogin: false);

        if (userResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to create bootstrap admin user: {userResult.Error.Message}");
        }

        var adminUser = userResult.Value;
        var roleResult = adminUser.AssignRole(platformOwnerRole);
        if (roleResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to assign PlatformOwner to bootstrap admin: {roleResult.Error.Message}");
        }

        dbContext.AdminUsers.Add(adminUser);
        return (true, false, false);
    }

    private static string GetPermissionDescription(string permissionName) => permissionName switch
    {
        BackofficePermissions.TenantsRead => "Permissão de leitura de tenants e clientes",
        BackofficePermissions.TenantsWrite => "Permissão de escrita e edição de dados de tenants",
        BackofficePermissions.TenantsSuspend => "Permissão para suspender ou reativar tenants",
        BackofficePermissions.PlansRead => "Permissão de visualização do catálogo de planos",
        BackofficePermissions.PlansWrite => "Permissão de edição do catálogo de planos",
        BackofficePermissions.PlansPublish => "Permissão de publicação de versões de planos",
        BackofficePermissions.SubscriptionsRead => "Permissão de visualização de assinaturas",
        BackofficePermissions.SubscriptionsManage => "Permissão de alteração e gerenciamento de assinaturas",
        BackofficePermissions.ImpersonationStart => "Permissão de início de sessão assistida (impersonação)",
        BackofficePermissions.ImpersonationStop => "Permissão de encerramento de sessão assistida (impersonação)",
        BackofficePermissions.AuditRead => "Permissão de leitura das trilhas de auditoria",
        BackofficePermissions.UsersAdminManage => "Permissão de gerenciamento completo de usuários administrativos",
        BackofficePermissions.ObservabilityRead => "Permissão de visualização de métricas e alertas operacionais",
        BackofficePermissions.ObservabilityManage => "Permissão para gerenciar e resolver alertas operacionais e executar simulações",
        _ => $"Permissão global para o recurso {permissionName}"
    };

    private static string GetRoleDescription(string roleName) => roleName switch
    {
        BackofficeRoles.PlatformOwner => "Superusuário com acesso irrestrito a todas as operações do Backoffice",
        BackofficeRoles.SupportN1 => "Atendimento N1: Visualização de clientes, assinaturas e auditoria",
        BackofficeRoles.SupportN2 => "Atendimento N2: Operações avançadas de suporte e impersonação auditada",
        BackofficeRoles.FinanceOps => "Operações Financeiras: Gestão de planos, assinaturas e cobrança",
        BackofficeRoles.ReadOnlyAuditor => "Auditor Somente-Leitura: Acesso exclusivo para consulta de logs e relatórios",
        _ => $"Função administrativa {roleName}"
    };

    private static async Task<int> SeedPlansAsync(
        BackofficeDbContext dbContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var existingPlans = await dbContext.PlanCatalogs.ToListAsync(cancellationToken);
        if (existingPlans.Count > 0)
        {
            return 0;
        }

        var bootstrapAdminId = Guid.NewGuid();
        var created = 0;

        created += TrySeedPlan(
            dbContext,
            logger,
            PlanCatalog.Create(
                "starter",
                "Plano Starter",
                "Ideal para pequenas propriedades com controle inicial de reprodução e parto.",
                "PeDistributed"),
            bootstrapAdminId,
            "v1.0 - Lançamento",
            149.90m,
            119.90m,
            500,
            3,
            1,
            new[]
            {
                PlanFeature.Create("Modules.Cows", "Módulo de Plantel Bovino & Matrizes (Cows)", true),
                PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                PlanFeature.Create("Modules.Calving", "Módulo de Partos & Bezerreiro", true)
            },
            new[]
            {
                PlanLimit.Create("MaxCattleHeads", 500, "cabeças")
            });

        created += TrySeedPlan(
            dbContext,
            logger,
            PlanCatalog.Create(
                "pro",
                "Plano Profissional",
                "Para médias e grandes fazendas com controle sanitário, nutricional e crescimento.",
                "PeDistributed"),
            bootstrapAdminId,
            "v1.0 - Lançamento",
            349.90m,
            299.90m,
            2500,
            10,
            5,
            new[]
            {
                PlanFeature.Create("Modules.Cows", "Módulo de Plantel Bovino & Matrizes (Cows)", true),
                PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                PlanFeature.Create("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                PlanFeature.Create("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                PlanFeature.Create("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                PlanFeature.Create("Modules.Nutrition", "Módulo Nutricional & Suplementação", true)
            },
            new[]
            {
                PlanLimit.Create("MaxCattleHeads", 2500, "cabeças")
            });

        created += TrySeedPlan(
            dbContext,
            logger,
            PlanCatalog.Create(
                "enterprise",
                "Plano Enterprise",
                "Solução completa para grandes grupos pecuários com cabeças ilimitadas e analytics.",
                "Enterprise"),
            bootstrapAdminId,
            "v1.0 - Lançamento",
            799.90m,
            699.90m,
            999999,
            50,
            20,
            new[]
            {
                PlanFeature.Create("Modules.Cows", "Módulo de Plantel Bovino & Matrizes (Cows)", true),
                PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                PlanFeature.Create("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                PlanFeature.Create("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                PlanFeature.Create("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                PlanFeature.Create("Modules.Nutrition", "Módulo Nutricional & Suplementação", true),
                PlanFeature.Create("Modules.Feedlot", "Módulo de Confinamento & Trato TMR", true),
                PlanFeature.Create("Modules.Analytics", "Zootecnia Avançada & Indicadores", true),
                PlanFeature.Create("PwaOfflineMode", "Modo Offline PWA em Curral", true)
            },
            new[]
            {
                PlanLimit.Create("MaxCattleHeads", 999999, "cabeças")
            });

        return created;
    }

    private static int TrySeedPlan(
        BackofficeDbContext dbContext,
        ILogger? logger,
        Result<PlanCatalog> planResult,
        Guid bootstrapAdminId,
        string versionName,
        decimal monthlyPrice,
        decimal annualPriceMonthly,
        int headCapacityLimit,
        int? maxUsers,
        int? maxProductionUnits,
        IEnumerable<PlanFeature> features,
        IEnumerable<PlanLimit> limits)
    {
        if (planResult.IsFailure)
        {
            logger?.LogWarning(
                "Skipping backoffice plan seed: catalog creation failed with {ErrorCode}.",
                planResult.Error.Code);
            return 0;
        }

        var plan = planResult.Value;
        var versionResult = plan.CreateVersion(
            versionName,
            monthlyPrice,
            annualPriceMonthly,
            headCapacityLimit,
            maxUsers,
            maxProductionUnits,
            features,
            limits);

        if (versionResult.IsFailure)
        {
            logger?.LogWarning(
                "Skipping backoffice plan seed for catalog {PlanCode}: version creation failed with {ErrorCode}.",
                plan.Code,
                versionResult.Error.Code);
            return 0;
        }

        var publishResult = plan.PublishVersion(versionResult.Value.Id, bootstrapAdminId, "Seed inicial de produção");
        if (publishResult.IsFailure)
        {
            logger?.LogWarning(
                "Skipping backoffice plan seed for catalog {PlanCode}: publish failed with {ErrorCode}.",
                plan.Code,
                publishResult.Error.Code);
            return 0;
        }

        dbContext.PlanCatalogs.Add(plan);
        return 1;
    }

    public static async Task<(int TiersCreated, int ModulesCreated)> SeedModularCatalogAsync(
        BackofficeDbContext dbContext,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var existingTiers = await dbContext.HeadTierCatalogs.ToListAsync(cancellationToken);
        var tiersMap = existingTiers.ToDictionary(t => t.TierCode, StringComparer.OrdinalIgnoreCase);
        var tiersCreated = 0;

        var defaultTiers = new (string TierCode, string DisplayName, int MinHeads, int MaxHeads, int SortOrder)[]
        {
            ("TIER_500", "Até 500 cabeças", 1, 500, 1),
            ("TIER_1500", "De 501 a 1.500 cabeças", 501, 1500, 2),
            ("TIER_3000", "De 1.501 a 3.000 cabeças", 1501, 3000, 3),
            ("TIER_5000_PLUS", "Acima de 3.000 cabeças", 3001, 10000, 4)
        };

        foreach (var (tierCode, displayName, minHeads, maxHeads, sortOrder) in defaultTiers)
        {
            if (tiersMap.ContainsKey(tierCode))
            {
                continue;
            }

            var tierResult = HeadTierCatalog.Create(tierCode, displayName, minHeads, maxHeads, sortOrder);
            if (tierResult.IsFailure)
            {
                logger?.LogWarning(
                    "Skipping head tier seed {TierCode}: failed with {ErrorCode}.",
                    tierCode,
                    tierResult.Error.Code);
                continue;
            }

            dbContext.HeadTierCatalogs.Add(tierResult.Value);
            tiersMap[tierCode] = tierResult.Value;
            tiersCreated++;
        }

        var existingModules = await dbContext.ModuleCatalogs
            .Include(m => m.TierPrices)
            .ToListAsync(cancellationToken);
        var modulesMap = existingModules.ToDictionary(m => m.ModuleKey, StringComparer.OrdinalIgnoreCase);
        var modulesCreated = 0;
        var bootstrapAdminId = Guid.NewGuid();

        var defaultModules = new (
            string ModuleKey,
            string Name,
            string Description,
            string Icon,
            bool IsCore,
            int SortOrder,
            (string TierCode, decimal MonthlyPrice)[] Prices)[]
        {
            (
                "Cows",
                "Gestão do Plantel (Base)",
                "Gestão cadastral do rebanho, lotes básicos, unidades produtivas e dashboard executivo.",
                "pets",
                true,
                1,
                [
                    ("TIER_500", 79m),
                    ("TIER_1500", 139m),
                    ("TIER_3000", 219m),
                    ("TIER_5000_PLUS", 349m)
                ]
            ),
            (
                "Breeding",
                "Reprodução & IATF",
                "Protocolos de IATF, inseminação artificial, monta natural e diagnóstico de gestação.",
                "favorite",
                false,
                2,
                [
                    ("TIER_500", 49m),
                    ("TIER_1500", 89m),
                    ("TIER_3000", 139m),
                    ("TIER_5000_PLUS", 199m)
                ]
            ),
            (
                "Calving",
                "Maternidade & Bezerreiro",
                "Registro de partos, identificação de bezerros, desmama e peso ajustado aos 205 dias.",
                "child_care",
                false,
                3,
                [
                    ("TIER_500", 39m),
                    ("TIER_1500", 69m),
                    ("TIER_3000", 109m),
                    ("TIER_5000_PLUS", 159m)
                ]
            ),
            (
                "Growth",
                "Recria, Engorda & Pastos",
                "Pesagens, cálculo de GPD/@, movimentação de lotes e taxa de lotação de pastos (UA/ha).",
                "trending_up",
                false,
                4,
                [
                    ("TIER_500", 59m),
                    ("TIER_1500", 99m),
                    ("TIER_3000", 159m),
                    ("TIER_5000_PLUS", 229m)
                ]
            ),
            (
                "Sanitary",
                "Sanitário & Carência",
                "Campanhas de vacinação, vermifugação, tratamentos veterinários e bloqueio de carência de abate.",
                "vaccines",
                false,
                5,
                [
                    ("TIER_500", 49m),
                    ("TIER_1500", 89m),
                    ("TIER_3000", 139m),
                    ("TIER_5000_PLUS", 199m)
                ]
            ),
            (
                "Nutrition",
                "Nutrição & Confinamento",
                "Suplementação a pasto, estoque de silos, carregamento de trato TMR e conversão alimentar.",
                "restaurant",
                false,
                6,
                [
                    ("TIER_500", 79m),
                    ("TIER_1500", 149m),
                    ("TIER_3000", 239m),
                    ("TIER_5000_PLUS", 349m)
                ]
            ),
            (
                "Analytics",
                "BI & Analytics Avançado",
                "Relatórios zootécnicos avançados, inteligência de rebanho e indicadores executivos.",
                "insights",
                false,
                7,
                [
                    ("TIER_500", 49m),
                    ("TIER_1500", 79m),
                    ("TIER_3000", 129m),
                    ("TIER_5000_PLUS", 189m)
                ]
            )
        };

        foreach (var (moduleKey, name, description, icon, isCore, sortOrder, prices) in defaultModules)
        {
            if (!modulesMap.TryGetValue(moduleKey, out var module))
            {
                var moduleResult = ModuleCatalog.Create(moduleKey, name, description, icon, isCore, sortOrder);
                if (moduleResult.IsFailure)
                {
                    logger?.LogWarning(
                        "Skipping module catalog seed {ModuleKey}: failed with {ErrorCode}.",
                        moduleKey,
                        moduleResult.Error.Code);
                    continue;
                }

                module = moduleResult.Value;
                dbContext.ModuleCatalogs.Add(module);
                modulesMap[moduleKey] = module;
                modulesCreated++;
            }
            else if (module.Icon.StartsWith("bi-", StringComparison.OrdinalIgnoreCase))
            {
                module.UpdateDetails(module.Name, module.Description, icon, module.IsCore, module.SortOrder, module.IsActive);
            }

            foreach (var (tierCode, monthlyPrice) in prices)
            {
                if (!tiersMap.TryGetValue(tierCode, out var tier))
                {
                    continue;
                }

                if (module.TierPrices.Any(p => p.HeadTierCatalogId == tier.Id))
                {
                    continue;
                }

                var annualPriceMonthly = Math.Round(monthlyPrice * 0.80m, 2);
                var stripePriceMonthly = $"price_{moduleKey.ToLowerInvariant()}_{tierCode.ToLowerInvariant()}_monthly";
                var stripePriceAnnual = $"price_{moduleKey.ToLowerInvariant()}_{tierCode.ToLowerInvariant()}_annual";

                var draftResult = module.CreateTierPriceDraft(
                    tier.Id,
                    monthlyPrice,
                    annualPriceMonthly,
                    stripePriceMonthly,
                    stripePriceAnnual);

                if (draftResult.IsSuccess)
                {
                    module.PublishTierPrice(draftResult.Value.Id, bootstrapAdminId, "Seed inicial catálogo modular v1.0");
                }
            }
        }

        return (tiersCreated, modulesCreated);
    }

    private static async Task<int> SeedFeatureFlagsAsync(
        BackofficeDbContext dbContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var existingKeys = await dbContext.FeatureFlags
            .Select(f => f.Key)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var created = 0;

        var defaultFlags = new (string Key, string Name, string Description, CriaCerto.Modules.Backoffice.Application.Domain.Enums.FeatureFlagCategory Category, CriaCerto.Modules.Backoffice.Application.Domain.Enums.RolloutRing Ring, int Percentage)[]
        {
            ("backoffice.feature.impersonation", "Suporte Assistido com Impersonação", "Acesso temporário ao ambiente de produtores para suporte técnico assistido.", CriaCerto.Modules.Backoffice.Application.Domain.Enums.FeatureFlagCategory.SupportTools, CriaCerto.Modules.Backoffice.Application.Domain.Enums.RolloutRing.Ring1_EarlyAdopters, 100),
            ("backoffice.feature.plan_publishing", "Publicação de Planos e Preços", "Publicação e versionamento de catálogos comerciais e tabelas de planos.", CriaCerto.Modules.Backoffice.Application.Domain.Enums.FeatureFlagCategory.CommercialAndPlans, CriaCerto.Modules.Backoffice.Application.Domain.Enums.RolloutRing.Ring1_EarlyAdopters, 100),
            ("backoffice.feature.tenant_suspension", "Suspensão e Bloqueio de Inquilinos", "Suspensão, cancelamento e arquivamento de inquilinos com impacto de acesso.", CriaCerto.Modules.Backoffice.Application.Domain.Enums.FeatureFlagCategory.CriticalOperation, CriaCerto.Modules.Backoffice.Application.Domain.Enums.RolloutRing.Ring1_EarlyAdopters, 100),
            ("backoffice.feature.remediation_execution", "Execução de Remediações de Suporte", "Execução de rotinas e scripts de remediação operacional assistida.", CriaCerto.Modules.Backoffice.Application.Domain.Enums.FeatureFlagCategory.SupportTools, CriaCerto.Modules.Backoffice.Application.Domain.Enums.RolloutRing.Ring1_EarlyAdopters, 100),
            ("backoffice.feature.compliance_unmasking", "Desmascaramento Just-In-Time LGPD", "Revelação contextual de dados pessoais sensíveis (PII) com auditoria criptográfica.", CriaCerto.Modules.Backoffice.Application.Domain.Enums.FeatureFlagCategory.ComplianceAndPrivacy, CriaCerto.Modules.Backoffice.Application.Domain.Enums.RolloutRing.Ring0_Canary, 100)
        };

        foreach (var (key, name, description, category, ring, percentage) in defaultFlags)
        {
            if (existingSet.Contains(key))
            {
                continue;
            }

            var flagResult = FeatureFlag.Create(
                key: key,
                name: name,
                description: description,
                category: category,
                initialRing: ring,
                initialPercentage: percentage,
                createdBy: "system_seeder"
            );

            if (flagResult.IsSuccess)
            {
                dbContext.FeatureFlags.Add(flagResult.Value);
                created++;
            }
        }

        return created;
    }
}
