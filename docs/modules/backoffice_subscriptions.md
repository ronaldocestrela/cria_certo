# Assinaturas, Upgrade/Downgrade e Regras de Capacidade (`Modules.Backoffice` & `Modules.Tenancy`)

## 1. Visão Geral
A **Sub-fase 3.2** conecta o Catálogo de Planos Versionado ao ciclo de vida das assinaturas dos tenants contratantes no CriaCerto. 

Permite alterações assistidas de plano (Upgrade e Downgrade), recalculando em tempo real os limites operacionais (capacidade de cabeças de gado, usuários e unidades produtivas) e os módulos habilitados por Feature Gating (`Modules.Breeding`, `Modules.Growth`, `Modules.Nutrition`, `Modules.Sanitary`, `Modules.Analytics`).

---

## 2. Fluxo de Mudança de Plano e Grace Period

```
                               ┌───────────────────────────┐
                               │ ChangeTenantPlanCommand   │
                               └─────────────┬─────────────┘
                                             │
                                   ┌─────────▼─────────┐
                                   │ Uso <= Limites?   │
                                   └────┬─────────┬────┘
                                 Sim    │         │ Não
                ┌───────────────────────┘         └───────────────────────┐
                ▼                                                         ▼
┌───────────────────────────────┐                       ┌───────────────────────────────────┐
│ Upgrade ou Downgrade Imediato │                       │ Ativa Grace Period (14 dias)      │
│ - Atualiza ActivePlanVersionId│                       │ - Status: GracePeriodActive       │
│ - Ajusta limites e módulos    │                       │ - Define GracePeriodEndsAtUtc     │
└───────────────────────────────┘                       └─────────────────┬─────────────────┘
                                                                          │
                                                               ┌──────────▼──────────┐
                                                               │ Expira Grace Period │
                                                               └──────────┬──────────┘
                                                                          │
                                                                ┌─────────▼─────────┐
                                                                │ Uso Normalizado?  │
                                                                └────┬─────────┬────┘
                                                              Sim    │         │ Não
                                             ┌───────────────────────┘         └───────────────────────┐
                                             ▼                                                         ▼
                             ┌───────────────────────────────┐                       ┌───────────────────────────────────┐
                             │ Finaliza Downgrade Imediato   │                       │ Bloqueia Operações de Escrita     │
                             │ - Status: Active              │                       │ - Notifica Admin/Tenant           │
                             └───────────────────────────────┘                       └───────────────────────────────────┘
```

### Regras de Negócio
1. **Upgrades:** Aplicados imediatamente, expandindo a capacidade máxima e ativando novos módulos de plano.
2. **Downgrades Sem Excesso:** Aplicados imediatamente caso o uso atual do tenant (número de cabeças, usuários ativos, fazendas) seja menor ou igual aos limites do novo plano.
3. **Downgrades Com Excesso (Grace Period):**
   - Caso o uso atual do tenant exceda os limites do novo plano, a alteração entra em **Grace Period (14 dias corridos)**.
   - O tenant é notificado para adequar seu inventário/usuários.
   - Durante o Grace Period, a operação permanece ativa (`GracePeriodActive`).
   - Após a expiração do prazo, se o uso persistir em excesso, o job de enforcement (`PlanLimitEnforcementJob`) restringe permissões de escrita do tenant e sinaliza o estouro de capacidade.

---

## 3. Entidades e Modelo de Dados

### `TenantSubscription`
- `Id`: Guid
- `TenantId`: Guid
- `PlanCatalogId`: Guid
- `PlanVersionId`: Guid
- `Status`: `Active`, `GracePeriodActive`, `PendingDowngrade`, `Cancelled`
- `GracePeriodStartedAtUtc`: DateTime?
- `GracePeriodEndsAtUtc`: DateTime?
- `PendingPlanVersionId`: Guid?
- `MaxHeadCapacity`: int
- `MaxUsers`: int
- `MaxProductionUnits`: int

### `TenantSubscriptionHistory`
- `Id`: Guid
- `TenantId`: Guid
- `PreviousPlanVersionId`: Guid?
- `NewPlanVersionId`: Guid
- `ChangedByAdminUserId`: Guid
- `Justification`: string (mínimo 15 caracteres)
- `ActionType`: `Upgrade`, `DowngradeImmediate`, `DowngradeGracePeriodStarted`, `GracePeriodResolved`
- `ChangedAtUtc`: DateTime

---

## 4. Endpoints da API

| Método | Endpoint | Permissão Exigida | Descrição |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/backoffice/tenants/{id}/plan-preview` | `tenants.read` | Pré-visualização de impacto (deltas de limite e módulos) |
| `POST` | `/api/v1/backoffice/tenants/{id}/plan` | `tenants.write` | Alteração de plano com justificativa obrigatória |
| `POST` | `/api/v1/backoffice/tenants/{id}/grace-period/resolve` | `tenants.write` | Resolução manual ou verificação de conformidade do Grace Period |
| `POST` | `/api/v1/backoffice/jobs/enforce-plan-limits` | `plans.publish` | Disparo manual/agendado do job de enforcement de limites |

---

## 5. Frontend Blazor WASM
- **`TenantPlanChangeModal.razor`**: Fluxo assistido em 3 passos com comparação visual de limites, aviso destacado de Grace Period e formulário de justificativa.
