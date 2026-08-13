using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Seeders;

public static class BackofficeDataSeeder
{
    public static async Task SeedAsync(
        BackofficeDbContext dbContext,
        IPasswordHasherService passwordHasher,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        // 1. Seed Catalog Permissions
        var existingPermissions = await dbContext.Permissions.ToListAsync(cancellationToken);
        var permissionsMap = existingPermissions.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var permName in BackofficePermissions.AllPermissions)
        {
            if (!permissionsMap.ContainsKey(permName))
            {
                var permResult = Permission.Create(
                    name: permName,
                    description: GetPermissionDescription(permName),
                    scope: BackofficePermissions.ScopeGlobal);

                if (permResult.IsSuccess)
                {
                    dbContext.Permissions.Add(permResult.Value);
                    permissionsMap[permName] = permResult.Value;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 2. Seed Standard Admin Roles
        var existingRoles = await dbContext.AdminRoles
            .Include(r => r.Permissions)
            .ToListAsync(cancellationToken);
        var rolesMap = existingRoles.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in BackofficeRoles.AllRoles)
        {
            AdminRole role;
            if (!rolesMap.TryGetValue(roleName, out role!))
            {
                var roleResult = AdminRole.Create(
                    name: roleName,
                    description: GetRoleDescription(roleName));

                if (roleResult.IsFailure)
                {
                    continue;
                }

                role = roleResult.Value;
                dbContext.AdminRoles.Add(role);
                rolesMap[roleName] = role;
            }

            // Sync default permissions for role
            var defaultPermNames = BackofficeRoles.GetDefaultPermissionsForRole(roleName);
            foreach (var permName in defaultPermNames)
            {
                if (permissionsMap.TryGetValue(permName, out var permEntity))
                {
                    role.AddPermission(permEntity);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 3. Seed Default Master Admin User (Platform Owner) if no admin user exists
        var hasAdminUser = await dbContext.AdminUsers.AnyAsync(cancellationToken);
        if (!hasAdminUser)
        {
            const string masterEmail = "admin@criacerto.com.br";
            const string masterName = "Administrador Mestre";
            const string masterPasswordRaw = "AdminPassword123!";

            var passwordHash = passwordHasher.HashPassword(masterPasswordRaw);

            var userResult = AdminUser.Create(
                name: masterName,
                email: masterEmail,
                passwordHash: passwordHash,
                mustChangePasswordOnNextLogin: false);

            if (userResult.IsSuccess)
            {
                var adminUser = userResult.Value;

                if (rolesMap.TryGetValue(BackofficeRoles.PlatformOwner, out var platformOwnerRole))
                {
                    adminUser.AssignRole(platformOwnerRole);
                }

                dbContext.AdminUsers.Add(adminUser);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
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
}
