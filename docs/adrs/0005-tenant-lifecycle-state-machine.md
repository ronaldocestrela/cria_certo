# ADR 0005: Máquina de Estados do Tenant e Governança de Ciclo de Vida

## Status
Aceito — Sub-fase 2.2 Backoffice

## Contexto
O backoffice precisa governar o ciclo de vida operacional dos tenants (trial, ativo, inadimplente, suspenso, cancelado, arquivado) com trilha de auditoria, justificativa obrigatória e proteção contra suspensão indevida.

## Decisão
1. Estados oficiais: `Trial`, `Active`, `PastDue`, `Suspended`, `Cancelled`, `Archived`.
2. Regras de transição centralizadas em `TenantLifecycle` no domínio Tenancy.
3. Métodos de agregado em `Tenant` retornam `Result` (sem exceções).
4. Comandos administrativos em duas camadas: Backoffice (audit) → Tenancy (persistência).
5. Permissão única `tenants.suspend` para todas as ações de ciclo de vida.
6. Flag `IsProtected` impede Suspender/Cancelar/Arquivar.
7. Acesso produtor bloqueado para `Suspended`, `Cancelled`, `Archived`; grace em `PastDue` até Phase 3 (billing).
8. `MarkPastDue` e `Activate` implementados no domínio, sem endpoint backoffice nesta fase (Phase 3).

## Consequências
- `Maintenance` deprecado; migrado para `Suspended`.
- JWT de produtor validado contra status atual via middleware.
- Phase 3 integrará jobs de dunning para `PastDue` automaticamente.
