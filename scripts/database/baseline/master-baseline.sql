/*
  Baseline de migrations para o catálogo master (foundation/tenancy/backoffice + módulos tenant no startup).

  Pré-requisitos:
  1. Backup completo do banco antes de executar.
  2. O schema atual deve ter sido criado pelo bootstrap legado (CreateTables).
  3. Executar uma única vez por catálogo master existente.

  Uso:
    sqlcmd -S localhost -U sa -P 'Password123!' -d CriaCertoDb -i master-baseline.sql
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

DECLARE @missing TABLE (SchemaName sysname, TableName sysname);

INSERT INTO @missing (SchemaName, TableName)
SELECT expected.SchemaName, expected.TableName
FROM (VALUES
    ('foundation', 'bovine_breeds'),
    ('tenancy', 'Tenants'),
    ('tenancy', 'Users'),
    ('tenancy', 'ProductionUnits'),
    ('tenancy', 'TeamInvites'),
    ('tenancy', 'UserTenants'),
    ('backoffice', 'AdminRoles'),
    ('backoffice', 'AdminSessions'),
    ('backoffice', 'AdminUsers'),
    ('backoffice', 'AuditLogs'),
    ('backoffice', 'Permissions'),
    ('backoffice', 'AdminRoleAdminUser'),
    ('backoffice', 'AdminRolePermission'),
    ('breeding', 'Bulls'),
    ('breeding', 'Cows'),
    ('breeding', 'IatfProtocols'),
    ('breeding', 'PregnancyDiagnoses'),
    ('breeding', 'SemenBatches'),
    ('calving', 'Calves'),
    ('calving', 'Calvings'),
    ('calving', 'Weanings'),
    ('growth', 'LotMovements'),
    ('growth', 'Lots'),
    ('growth', 'PasturePaddocks'),
    ('growth', 'weighings'),
    ('nutrition', 'DailyFeedBatches'),
    ('nutrition', 'FeedRations'),
    ('nutrition', 'PastureSupplementations'),
    ('nutrition', 'SiloStocks'),
    ('nutrition', 'FeedRationItems'),
    ('sanitary', 'treatment_records'),
    ('sanitary', 'vaccination_campaigns'),
    ('sanitary', 'vaccine_references')
) AS expected(SchemaName, TableName)
WHERE NOT EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = expected.SchemaName
      AND t.name = expected.TableName
);

IF EXISTS (SELECT 1 FROM @missing)
BEGIN
    SELECT SchemaName, TableName FROM @missing ORDER BY SchemaName, TableName;
    RAISERROR('Baseline abortado: schema existente incompatível com InitialCreate.', 16, 1);
END;

-- foundation
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'foundation') EXEC(N'CREATE SCHEMA [foundation]');
IF OBJECT_ID(N'[foundation].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [foundation].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_foundation___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
IF NOT EXISTS (SELECT 1 FROM [foundation].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260813173509_InitialCreate')
    INSERT INTO [foundation].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260813173509_InitialCreate', N'10.0.0');

-- tenancy
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'tenancy') EXEC(N'CREATE SCHEMA [tenancy]');
IF OBJECT_ID(N'[tenancy].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [tenancy].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_tenancy___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
IF NOT EXISTS (SELECT 1 FROM [tenancy].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260813173518_InitialCreate')
    INSERT INTO [tenancy].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260813173518_InitialCreate', N'10.0.0');

-- backoffice
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'backoffice') EXEC(N'CREATE SCHEMA [backoffice]');
IF OBJECT_ID(N'[backoffice].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [backoffice].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_backoffice___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
IF NOT EXISTS (SELECT 1 FROM [backoffice].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260813173522_InitialCreate')
    INSERT INTO [backoffice].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260813173522_InitialCreate', N'10.0.0');

-- breeding
IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON c.object_id = t.object_id
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'breeding' AND t.name = 'Bulls'
      AND c.name = 'BirthDate' AND c.is_nullable = 0)
    ALTER TABLE [breeding].[Bulls] ALTER COLUMN [BirthDate] datetime2 NULL;
IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.tables t ON c.object_id = t.object_id
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE s.name = 'breeding' AND t.name = 'Cows'
      AND c.name = 'BirthDate' AND c.is_nullable = 0)
    ALTER TABLE [breeding].[Cows] ALTER COLUMN [BirthDate] datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'breeding') EXEC(N'CREATE SCHEMA [breeding]');
IF OBJECT_ID(N'[breeding].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [breeding].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_breeding___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
IF NOT EXISTS (SELECT 1 FROM [breeding].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260813173527_InitialCreate')
    INSERT INTO [breeding].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260813173527_InitialCreate', N'10.0.0');

-- calving
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'calving') EXEC(N'CREATE SCHEMA [calving]');
IF OBJECT_ID(N'[calving].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [calving].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_calving___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
IF NOT EXISTS (SELECT 1 FROM [calving].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260813173531_InitialCreate')
    INSERT INTO [calving].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260813173531_InitialCreate', N'10.0.0');

-- growth
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'growth') EXEC(N'CREATE SCHEMA [growth]');
IF OBJECT_ID(N'[growth].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [growth].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_growth___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
IF NOT EXISTS (SELECT 1 FROM [growth].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260813173539_InitialCreate')
    INSERT INTO [growth].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260813173539_InitialCreate', N'10.0.0');

-- nutrition
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'nutrition') EXEC(N'CREATE SCHEMA [nutrition]');
IF OBJECT_ID(N'[nutrition].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [nutrition].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_nutrition___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
IF NOT EXISTS (SELECT 1 FROM [nutrition].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260813173543_InitialCreate')
    INSERT INTO [nutrition].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260813173543_InitialCreate', N'10.0.0');

-- sanitary
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'sanitary') EXEC(N'CREATE SCHEMA [sanitary]');
IF OBJECT_ID(N'[sanitary].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [sanitary].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_sanitary___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
IF NOT EXISTS (SELECT 1 FROM [sanitary].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260813173548_InitialCreate')
    INSERT INTO [sanitary].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260813173548_InitialCreate', N'10.0.0');

COMMIT TRANSACTION;

PRINT 'Baseline master concluído com sucesso.';
