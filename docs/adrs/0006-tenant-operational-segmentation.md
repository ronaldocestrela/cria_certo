# ADR 0006: Segmentação Operacional de Tenants e Filtros Salvos do Backoffice

## Status
Aceito — Sub-fase 2.3 Backoffice

## Contexto
Operações de suporte, sucesso do cliente e retenção precisam segmentar tenants por porte, região, perfil produtivo, risco de churn e etiquetas operacionais, com listagens performáticas, filtros salvos por admin e exportação de recortes.

## Decisão
1. Taxonomias fechadas no domínio Tenancy (`TenantSegmentationCatalog`), persistidas em colunas do agregado `Tenant`.
2. Etiquetas operacionais como catálogo + N:N (`OperationalTag`, `TenantOperationalTag`) no schema `tenancy`.
3. Filtros salvos (`AdminSavedFilter`) no schema `backoffice`, isolados por `AdminUserId`.
4. Comunicação Backoffice → Tenancy via MediatR; proibido join cross-module.
5. Listagem com filtros combinados, paginação offset + keyset (`CreatedAtUtc`, `Id`), `PageSize` clamp 1–100.
6. Exportação CSV com teto de 10.000 linhas; acima disso `Tenant.ExportLimitExceeded`.
7. Reuso de permissões existentes: `tenants.read` (listar/filtrar/salvar/exportar) e `tenants.write` (segmentação/tags).

## Consequências
- Índices em colunas de segmentação e `(CreatedAtUtc, Id)` para keyset.
- UI Blazor estende `TenantsManagement.razor` com filtros, recortes salvos e exportação.
- Jobs automáticos de churn e campanhas CRM ficam fora de escopo (Phase 3/5).
