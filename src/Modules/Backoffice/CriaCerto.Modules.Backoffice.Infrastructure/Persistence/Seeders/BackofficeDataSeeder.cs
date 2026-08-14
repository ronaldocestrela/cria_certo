using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Seeders;

public static class BackofficeDataSeeder
{
    public const string MasterAdminEmail = "admin@criacerto.com.br";
    public const string MasterAdminName = "Administrador Mestre";
    public const string MasterAdminPassword = "AdminPassword123!";

    public static async Task SeedAsync(
        BackofficeDbContext dbContext,
        IPasswordHasherService passwordHasher,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        var (permissionsCreated, permissionsMap) = await SeedPermissionsAsync(dbContext, cancellationToken);
        var (rolesCreated, rolesMap) = await SeedRolesAsync(dbContext, permissionsMap, cancellationToken);
        var (adminCreated, adminRoleRepaired) = await SeedMasterAdminAsync(
            dbContext,
            passwordHasher,
            rolesMap,
            cancellationToken);

        var plansCreated = await SeedPlansAsync(dbContext, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Backoffice seed completed. PermissionsCreated={PermissionsCreated}, RolesCreated={RolesCreated}, AdminCreated={AdminCreated}, AdminRoleRepaired={AdminRoleRepaired}, PlansCreated={PlansCreated}.",
            permissionsCreated,
            rolesCreated,
            adminCreated,
            adminRoleRepaired,
            plansCreated);
    }

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

    private static async Task<(bool AdminCreated, bool AdminRoleRepaired)> SeedMasterAdminAsync(
        BackofficeDbContext dbContext,
        IPasswordHasherService passwordHasher,
        IReadOnlyDictionary<string, AdminRole> rolesMap,
        CancellationToken cancellationToken)
    {
        if (!rolesMap.TryGetValue(BackofficeRoles.PlatformOwner, out var platformOwnerRole))
        {
            throw new InvalidOperationException(
                $"Required role '{BackofficeRoles.PlatformOwner}' was not seeded.");
        }

        var normalizedEmail = MasterAdminEmail.Trim().ToLowerInvariant();
        var existingAdmin = await dbContext.AdminUsers
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (existingAdmin is not null)
        {
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

                return (false, true);
            }

            return (false, false);
        }

        var passwordHash = passwordHasher.HashPassword(MasterAdminPassword);
        var userResult = AdminUser.Create(
            name: MasterAdminName,
            email: MasterAdminEmail,
            passwordHash: passwordHash,
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
        return (true, false);
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

    private static async Task<int> SeedPlansAsync(BackofficeDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingPlans = await dbContext.PlanCatalogs.ToListAsync(cancellationToken);
        if (existingPlans.Any()) return 0;

        var bootstrapAdminId = Guid.NewGuid();
        var created = 0;

        // 1. Starter Plan
        var starterResult = PlanCatalog.Create("starter", "Plano Starter", "Ideal para pequenas propriedades com controle inicial de reprodução e parto.", "PeDistributed");
        if (starterResult.IsSuccess)
        {
            var plan = starterResult.Value;
            var v1 = plan.CreateVersion(
                "v1.0 - Lançamento",
                149.90m,
                119.90m,
                500,
                3,
                1,
                new[]
                {
                    PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    PlanFeature.Create("Modules.Calving", "Módulo de Partos & Bezerreiro", true)
                },
                new[]
                {
                    PlanLimit.Create("MaxCattleHeads", 500, "cabeças")
                }).Value;
            plan.PublishVersion(v1.Id, bootstrapAdminId, "Seed inicial de produção");
            dbContext.PlanCatalogs.Add(plan);
            created++;
        }

        // 2. Pro Plan
        var proResult = PlanCatalog.Create("pro", "Plano Profissional", "Para médias e grandes fazendas com controle sanitário, nutricional e crescimento.", "PeDistributed");
        if (proResult.IsSuccess)
        {
            var plan = proResult.Value;
            var v1 = plan.CreateVersion(
                "v1.0 - Lançamento",
                349.90m,
                299.90m,
                2500,
                10,
                5,
                new[]
                {
                    PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    PlanFeature.Create("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    PlanFeature.Create("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                    PlanFeature.Create("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                    PlanFeature.Create("Modules.Nutrition", "Módulo Nutricional & Suplementação", true)
                },
                new[]
                {
                    PlanLimit.Create("MaxCattleHeads", 2500, "cabeças")
                }).Value;
            plan.PublishVersion(v1.Id, bootstrapAdminId, "Seed inicial de produção");
            dbContext.PlanCatalogs.Add(plan);
            created++;
        }

        // 3. Enterprise Plan
        var entResult = PlanCatalog.Create("enterprise", "Plano Enterprise", "Solução completa para grandes grupos pecuários com cabeças ilimitadas e analytics.", "Enterprise");
        if (entResult.IsSuccess)
        {
            var plan = entResult.Value;
            var v1 = plan.CreateVersion(
                "v1.0 - Lançamento",
                799.90m,
                699.90m,
                999999,
                50,
                20,
                new[]
                {
                    PlanFeature.Create("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    PlanFeature.Create("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    PlanFeature.Create("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                    PlanFeature.Create("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                    PlanFeature.Create("Modules.Nutrition", "Módulo Nutricional & Suplementação", true),
                    PlanFeature.Create("Modules.Analytics", "Zootecnia Avançada & Indicadores", true),
                    PlanFeature.Create("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                },
                new[]
                {
                    PlanLimit.Create("MaxCattleHeads", 999999, "cabeças")
                }).Value;
            plan.PublishVersion(v1.Id, bootstrapAdminId, "Seed inicial de produção");
            dbContext.PlanCatalogs.Add(plan);
            created++;
        }

        return created;
    }
}
