# Baseline de migrations EF Core

Estes scripts são uma alternativa operacional ao baseline automático da API. Use-os **uma única vez**
quando for necessário preparar bancos legados em uma janela controlada, antes de iniciar a aplicação.

Por padrão, a própria API reconhece schemas criados pelo bootstrap legado, valida tabelas, colunas,
tipos e nulabilidade e registra o baseline sem alterar dados de negócio. Qualquer drift interrompe
o startup.

## Quando executar

- **Banco master** (`DATABASE_NAME` / catálogo configurado na connection string): [`master-baseline.sql`](master-baseline.sql)
- **Banco tenant** (`criacerto_tenant_{TenantId:N}`): [`tenant-baseline.sql`](tenant-baseline.sql)

Bancos novos **não** precisam de baseline: a API aplica `Database.Migrate()` automaticamente no startup/provisionamento.

## Pré-requisitos

1. Fazer backup completo do catálogo antes de executar.
2. Confirmar que as tabelas esperadas já existem (o script valida e aborta em caso de drift).
3. Parar instâncias concorrentes da API durante a execução.

## Execução (master)

```bash
sqlcmd -S localhost -U sa -P 'Password123!' -C -d criacerto_foundation -i scripts/database/baseline/master-baseline.sql
```

## Execução (tenant)

```bash
sqlcmd -S localhost -U sa -P 'Password123!' -C -d criacerto_tenant_<guid> -i scripts/database/baseline/tenant-baseline.sql
```

## Verificação pós-baseline

```sql
SELECT s.name AS [schema], h.MigrationId, h.ProductVersion
FROM sys.tables h
JOIN sys.schemas s ON h.schema_id = s.schema_id
WHERE h.name = '__EFMigrationsHistory'
ORDER BY s.name;
```

Após o baseline, reinicie a API e confirme que **não** aparecem entradas `Failed executing DbCommand` para `CREATE TABLE` no log.

## Rollback

Restaure o backup do catálogo. O baseline apenas insere registros em `__EFMigrationsHistory` por schema; não altera dados de negócio.
