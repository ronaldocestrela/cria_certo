using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CriaCerto.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateExistingTenantsToModularCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- 1. Atualizar Tenants com base no plano Enterprise
                UPDATE [tenancy].[Tenants]
                SET [HeadTierCode] = 'TIER_5000_PLUS',
                    [Capacity] = 10000,
                    [SizeSegment] = 'Large',
                    [UpdatedAtUtc] = GETUTCDATE()
                WHERE [SubscribedPlan] LIKE '%Enterprise%'
                  AND ([HeadTierCode] <> 'TIER_5000_PLUS' OR [Capacity] <> 10000 OR [SizeSegment] <> 'Large');

                -- 2. Atualizar Tenants com base no plano Pro
                UPDATE [tenancy].[Tenants]
                SET [HeadTierCode] = 'TIER_3000',
                    [Capacity] = 3000,
                    [SizeSegment] = 'Medium',
                    [UpdatedAtUtc] = GETUTCDATE()
                WHERE ([SubscribedPlan] LIKE '%Pro%' OR [SubscribedPlan] LIKE '%Profissional%')
                  AND [SubscribedPlan] NOT LIKE '%Enterprise%'
                  AND ([HeadTierCode] <> 'TIER_3000' OR [Capacity] <> 3000 OR [SizeSegment] <> 'Medium');

                -- 3. Atualizar Tenants com base no plano Starter (e fallbacks)
                UPDATE [tenancy].[Tenants]
                SET [HeadTierCode] = 'TIER_500',
                    [Capacity] = 500,
                    [SizeSegment] = 'Small',
                    [UpdatedAtUtc] = GETUTCDATE()
                WHERE ([SubscribedPlan] LIKE '%Starter%'
                       OR ([SubscribedPlan] NOT LIKE '%Pro%' AND [SubscribedPlan] NOT LIKE '%Profissional%' AND [SubscribedPlan] NOT LIKE '%Enterprise%'))
                  AND ([HeadTierCode] <> 'TIER_500' OR [Capacity] <> 500 OR [SizeSegment] <> 'Small');

                -- 4. Inserir Módulos para Tenants Enterprise (Cows, Breeding, Calving, Growth, Sanitary, Nutrition, Analytics)
                INSERT INTO [tenancy].[TenantSubscribedModules]
                    ([Id], [TenantId], [ModuleKey], [StripeSubscriptionItemId], [ActivatedAtUtc], [MonthlyPriceSnapshot])
                SELECT
                    NEWID(),
                    t.[Id],
                    m.[ModuleKey],
                    NULL,
                    ISNULL(t.[CreatedAtUtc], GETUTCDATE()),
                    0.00
                FROM [tenancy].[Tenants] t
                CROSS JOIN (
                    VALUES 
                        ('Cows'),
                        ('Breeding'),
                        ('Calving'),
                        ('Growth'),
                        ('Sanitary'),
                        ('Nutrition'),
                        ('Analytics')
                ) AS m([ModuleKey])
                WHERE t.[SubscribedPlan] LIKE '%Enterprise%'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [tenancy].[TenantSubscribedModules] sm
                      WHERE sm.[TenantId] = t.[Id]
                        AND sm.[ModuleKey] = m.[ModuleKey]
                  );

                -- 5. Inserir Módulos para Tenants Pro (Cows, Breeding, Calving, Growth, Nutrition)
                INSERT INTO [tenancy].[TenantSubscribedModules]
                    ([Id], [TenantId], [ModuleKey], [StripeSubscriptionItemId], [ActivatedAtUtc], [MonthlyPriceSnapshot])
                SELECT
                    NEWID(),
                    t.[Id],
                    m.[ModuleKey],
                    NULL,
                    ISNULL(t.[CreatedAtUtc], GETUTCDATE()),
                    0.00
                FROM [tenancy].[Tenants] t
                CROSS JOIN (
                    VALUES 
                        ('Cows'),
                        ('Breeding'),
                        ('Calving'),
                        ('Growth'),
                        ('Nutrition')
                ) AS m([ModuleKey])
                WHERE (t.[SubscribedPlan] LIKE '%Pro%' OR t.[SubscribedPlan] LIKE '%Profissional%')
                  AND t.[SubscribedPlan] NOT LIKE '%Enterprise%'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [tenancy].[TenantSubscribedModules] sm
                      WHERE sm.[TenantId] = t.[Id]
                        AND sm.[ModuleKey] = m.[ModuleKey]
                  );

                -- 6. Inserir Módulos para Tenants Starter e fallbacks (Cows, Breeding, Calving)
                INSERT INTO [tenancy].[TenantSubscribedModules]
                    ([Id], [TenantId], [ModuleKey], [StripeSubscriptionItemId], [ActivatedAtUtc], [MonthlyPriceSnapshot])
                SELECT
                    NEWID(),
                    t.[Id],
                    m.[ModuleKey],
                    NULL,
                    ISNULL(t.[CreatedAtUtc], GETUTCDATE()),
                    0.00
                FROM [tenancy].[Tenants] t
                CROSS JOIN (
                    VALUES 
                        ('Cows'),
                        ('Breeding'),
                        ('Calving')
                ) AS m([ModuleKey])
                WHERE (t.[SubscribedPlan] LIKE '%Starter%'
                       OR (t.[SubscribedPlan] NOT LIKE '%Pro%' AND t.[SubscribedPlan] NOT LIKE '%Profissional%' AND t.[SubscribedPlan] NOT LIKE '%Enterprise%'))
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [tenancy].[TenantSubscribedModules] sm
                      WHERE sm.[TenantId] = t.[Id]
                        AND sm.[ModuleKey] = m.[ModuleKey]
                  );

                -- 7. Garantia inviolável do Core Module (Cows) para qualquer tenant remanescente
                INSERT INTO [tenancy].[TenantSubscribedModules]
                    ([Id], [TenantId], [ModuleKey], [StripeSubscriptionItemId], [ActivatedAtUtc], [MonthlyPriceSnapshot])
                SELECT
                    NEWID(),
                    t.[Id],
                    'Cows',
                    NULL,
                    ISNULL(t.[CreatedAtUtc], GETUTCDATE()),
                    0.00
                FROM [tenancy].[Tenants] t
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [tenancy].[TenantSubscribedModules] sm
                    WHERE sm.[TenantId] = t.[Id]
                      AND sm.[ModuleKey] = 'Cows'
                );

                -- 8. Registro de Auditoria e Histórico em TenantSubscriptionHistories (se a tabela existir)
                IF OBJECT_ID(N'tenancy.TenantSubscriptionHistories', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO [tenancy].[TenantSubscriptionHistories]
                        ([Id], [TenantId], [PreviousPlanVersionId], [NewPlanVersionId], [ChangedByAdminUserId], [Justification], [ActionType], [SnapshotHeadCount], [SnapshotUserCount], [SnapshotUnitCount], [ChangedAtUtc])
                    SELECT
                        NEWID(),
                        t.[Id],
                        NULL,
                        '00000000-0000-0000-0000-000000000000',
                        '00000000-0000-0000-0000-000000000000',
                        CONCAT('Migração automática de plano legado (', t.[SubscribedPlan], ') para catálogo modular e faixa de cabeças (', t.[HeadTierCode], ').'),
                        'PlanChanged',
                        0,
                        1,
                        1,
                        GETUTCDATE()
                    FROM [tenancy].[Tenants] t
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [tenancy].[TenantSubscriptionHistories] sh
                        WHERE sh.[TenantId] = t.[Id]
                          AND sh.[Justification] LIKE 'Migração automática de plano legado%'
                    );
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migração de dados: Down preserva os dados convertidos para não causar indisponibilidade
            // de acesso às fazendas que já estejam operando sob o modelo modular.
        }
    }
}

