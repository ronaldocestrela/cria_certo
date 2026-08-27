# Faturamento Operacional e Eventos de Cobrança (`Modules.Backoffice` & `Modules.Tenancy`)

## 1. Visão Geral
A **Sub-fase 3.3** implementa a gestão de faturamento operacional, eventos de cobrança de assinaturas, controle de inadimplência (*aging list*), conciliação financeira assistida e sincronização automática com o ciclo de vida dos tenants e o mecanismo de *Feature Gating* do CriaCerto.

Ela garante previsibilidade de receita recorrente mensal (MRR), transparência na régua de cobrança e governança rigorosa nas ações de baixa e regularização através de trilha de auditoria forense em `AuditLog`.

---

## 2. Ciclo de Vida da Cobrança e Régua de Inadimplência

```
               ┌───────────────────────────────┐
               │    Emissão da Fatura (MRR)    │
               │      - Status: Pending        │
               │   - Event: InvoiceGenerated   │
               └───────────────┬───────────────┘
                               │
                     ┌─────────▼─────────┐
                     │  Pagamento no     │
                     │    Vencimento?    │
                     └────┬─────────┬────┘
                   Sim    │         │ Não (Vencida)
  ┌───────────────────────┘         └────────────────────────┐
  ▼                                                          ▼
┌───────────────────────────────┐          ┌───────────────────────────────────┐
│       Fatura Quitada          │          │ ProcessBillingDelinquencyJob      │
│      - Status: Paid           │          │ - Status Fatura: PastDue          │
│   - Event: PaymentReceived    │          │ - Event: InvoicePastDue           │
│ - Tenant Mantém Status Active │          │ - Atualiza Régua de Aging         │
└───────────────────────────────┘          └─────────────────┬─────────────────┘
                                                             │
                                                   ┌─────────▼─────────┐
                                                   │ Tenant Status:    │
                                                   │     PastDue       │
                                                   └─────────┬─────────┘
                                                             │
                                               ┌─────────────┴─────────────┐
                                               ▼                           ▼
                                ┌───────────────────────────┐ ┌───────────────────────────┐
                                │     Baixa / Pagamento     │ │   Regularização / Anistia │
                                │ - RecordPaymentCommand    │ │ - RegularizeTenantCommand │
                                │ - Status: Paid            │ │ - Justificativa >= 15 car │
                                │ - Tenant volta para Active│ │ - Status: Waived          │
                                │ - Audit: PaymentRecorded  │ │ - Tenant volta para Active│
                                └───────────────────────────┘ └───────────────────────────┘
```

---

## 3. Réguas de Aging e Impacto no Acesso do Tenant

| Faixa de Aging | Dias de Atraso | Status Fatura | Status Tenant | Impacto no Acesso do Produtor | Ação Recomendada |
| :--- | :---: | :---: | :---: | :--- | :--- |
| **Em dia** | 0 dias | `Pending` / `Paid` | `Active` | Acesso total a todos os módulos contratados no plano. | Cobrança preventiva / Envio de boleto ou chave Pix. |
| **Aging 1-30d** | 1 a 30 dias | `PastDue` | `PastDue` | Acesso operacional mantido com alerta visual de pendência. | Régua automática de cobrança / Notificação N1. |
| **Aging 31-60d** | 31 a 60 dias | `PastDue` | `PastDue` / `Suspended` | Suspensão de novas inclusões ou bloqueio assistido. | Contato financeiro ativo / Notificação N2. |
| **Aging 61-90d** | 61 a 90 dias | `PastDue` | `Suspended` | Acesso do produtor bloqueado até a quitação. | Renegociação comercial ou anistia com justificativa. |
| **Aging > 90d** | > 90 dias | `PastDue` | `Suspended` / `Cancelled` | Acesso bloqueado / Risco crítico de churn. | Encaminhamento para cobrança jurídica / Cancelamento. |

---

## 4. Modelo de Entidades e Schema Relacional (`backoffice`)

### `BillingInvoice`
- `Id`: Guid
- `TenantId`: Guid (vínculo lógico com `Modules.Tenancy`)
- `TenantName`: string
- `SubscriptionId`: Guid?
- `PlanVersionId`: Guid?
- `PlanName`: string
- `InvoiceNumber`: string (Índice Único)
- `Amount`: decimal (18, 2)
- `DueDate`: DateTime
- `PaidAtUtc`: DateTime?
- `Status`: `Draft`, `Pending`, `Paid`, `PastDue`, `Cancelled`, `Waived`
- `PaymentMethod`: `Boleto`, `Pix`, `CreditCard`, `BankTransfer`
- `ExternalTransactionId`: string?
- `BillingPeriodStart` / `BillingPeriodEnd`: DateTime
- `Notes`: string?
- `Events`: ICollection<`BillingEvent`>

### `BillingEvent`
- `Id`: Guid
- `InvoiceId`: Guid
- `TenantId`: Guid
- `EventType`: `InvoiceGenerated`, `PaymentReceived`, `PaymentFailed`, `InvoicePastDue`, `InvoiceWaived`, `InvoiceCancelled`
- `OccurredAtUtc`: DateTime
- `ProcessedByAdminUserId`: Guid?
- `Justification`: string?
- `MetadataJson`: string?

---

## 5. Endpoints da API Minimal API

| Método | Endpoint | Permissão Requerida | Descrição |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/backoffice/billing/overview` | `subscriptions.read` | Resumo financeiro com MRR, total inadimplente e contagem por aging bucket. |
| `GET` | `/api/v1/backoffice/billing/invoices` | `subscriptions.read` | Consulta paginada com filtros por status, faixa de aging e busca textual. |
| `GET` | `/api/v1/backoffice/billing/tenants/{tenantId}/history` | `subscriptions.read` | Extrato financeiro e linha do tempo de eventos de cobrança do tenant. |
| `POST` | `/api/v1/backoffice/billing/invoices` | `subscriptions.manage` | Emissão avulsa ou gerada de fatura de cobrança. |
| `POST` | `/api/v1/backoffice/billing/invoices/{id}/pay` | `subscriptions.manage` | Registro de baixa de pagamento com conciliação automática do tenant. |
| `POST` | `/api/v1/backoffice/billing/invoices/{id}/regularize` | `subscriptions.manage` | Anistia/regularização assistida de fatura com justificativa obrigatória. |
| `POST` | `/api/v1/backoffice/jobs/process-billing-delinquency` | `subscriptions.manage` | Disparo manual/agendado do job de reconciliação de aging e inadimplência. |

---

## 6. Frontend Blazor WASM

1. **`BillingManagement.razor`**:
   - Cards de KPI financeiro (MRR, Total Inadimplente, Faturas Pendentes, Taxa de Inadimplência).
   - Régua interativa de Aging com filtro instantâneo por bucket (1-30d, 31-60d, 61-90d, >90d).
   - Tabela consolidada de cobrança com badges de status e aging.
2. **Modais de Ação**:
   - `RecordPaymentModal.razor`: Baixa de pagamento com forma (Pix, Boleto, Cartão) e ID de comprovante.
   - `RegularizeBillingModal.razor`: Anistia assistida com validação de justificativa mínima (15 caracteres).
   - `TenantBillingHistoryModal.razor`: Histórico completo de faturas e timeline de eventos de cobrança.
3. **Navegação**:
   - Integrado ao `BackofficeNavMenu.razor` sob o item "Faturamento & Cobrança" protegido por `<PermissionGuard Permission="subscriptions.read">`.

---

## 7. Governança e Auditoria Forense
Todas as operações de baixa financeira (`Billing.PaymentRecorded`), regularização (`Billing.InvoiceRegularized`) e execução de jobs de inadimplência gravam registros imutáveis em `AuditLog` no schema `backoffice`, incluindo:
- Identificador do usuário administrativo (`AdminUserId` e `AdminUserEmail`).
- Recurso afetado e ID da fatura / tenant.
- Endereço IP e payload JSON com detalhes da transação e justificativa.
