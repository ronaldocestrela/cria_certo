# Catálogo de Planos Versionado — Especificação de Domínio e Arquitetura (`Modules.Backoffice`)

## 1. Visão Geral
A **Sub-fase 3.1: Catálogo de Planos Versionado** implementa a gestão de planos SaaS no Backoffice Administrativo do CriaCerto com suporte a **versionamento estritamente imutável**.

Essa abordagem garante que reajustes de preço, inclusão ou remoção de módulos e alteração nos limites de capacidade (ex.: limite de cabeças de gado) não gerem impactos retroativos indesejados nas assinaturas ativas dos tenants contratantes.

---

## 2. Modelo de Domínio e Entidades

```
┌─────────────────────────────────────────────────────────────┐
│                        PlanCatalog                          │
│ ─────────────────────────────────────────────────────────── │
│ - Id: Guid                                                  │
│ - Code: string (slug única, ex: starter, pro, enterprise)   │
│ - Name: string                                              │
│ - Description: string                                       │
│ - Category: string (PeDistributed, Confinamento, Enterprise)│
│ - IsArchived: bool                                          │
└──────────────────────────────┬──────────────────────────────┘
                               │ 1
                               │
                               │ *
┌──────────────────────────────▼──────────────────────────────┐
│                        PlanVersion                          │
│ ─────────────────────────────────────────────────────────── │
│ - Id: Guid                                                  │
│ - VersionNumber: int (1, 2, 3...)                           │
│ - VersionName: string                                       │
│ - Status: PlanVersionStatus (Draft, Published, Deprecated)  │
│ - MonthlyPrice / AnnualPriceMonthly: decimal                │
│ - HeadCapacityLimit: int                                    │
│ - PublishedAtUtc: DateTimeOffset?                           │
│ - PublishedByAdminId: Guid?                                 │
└──────────────┬──────────────────────────────┬───────────────┘
               │ 1                            │ 1
               │                              │
               │ *                            │ *
┌──────────────▼─────────────┐  ┌─────────────▼───────────────┐
│        PlanFeature         │  │          PlanLimit          │
│ ────────────────────────── │  │ ─────────────────────────── │
│ - FeatureKey: string       │  │ - LimitKey: string          │
│ - DisplayName: string      │  │ - LimitValue: decimal       │
│ - IsEnabled: bool          │  │ - Unit: string              │
└────────────────────────────┘  └─────────────────────────────┘
```

---

## 3. Regras de Imutabilidade e Transição de Estado

1. **Status Draft (Rascunho):**
   - Apenas um rascunho pode existir ativamente por plano (`PlanErrors.DraftAlreadyExists`).
   - Todos os atributos de preços, módulos inclusos e limites operacionais são editáveis enquanto a versão estiver em `Draft`.
2. **Status Published (Publicada):**
   - Transição efetuada via comando `PublishPlanVersionCommand` exigindo a permissão `plans.publish`.
   - Uma vez publicada, a versão torna-se **estritamente imutável** (`PlanErrors.PublishedVersionImmutable`).
   - Ao publicar uma nova versão, qualquer versão anteriormente em `Published` passa automaticamente para `Deprecated` (obsoleta para novos cadastros).
3. **Status Deprecated (Obsoleta):**
   - Não aceita novas contratações, porém permanece ativa para os tenants que a assinaram previamente.

---

## 4. Matriz de Segurança e Permissões Granulares

| Operação | Endpoint | Permissão Exigida | Papéis Padrão Habilitados |
| :--- | :--- | :--- | :--- |
| **Listar / Detalhar Planos** | `GET /api/v1/backoffice/plans` | `plans.read` | PlatformOwner, SupportN1, SupportN2, FinanceOps, ReadOnlyAuditor |
| **Comparar Versões** | `GET /api/v1/backoffice/plans/versions/compare` | `plans.read` | PlatformOwner, SupportN1, SupportN2, FinanceOps, ReadOnlyAuditor |
| **Criar Plano / Versão Rascunho** | `POST /api/v1/backoffice/plans` | `plans.write` | PlatformOwner, FinanceOps |
| **Editar Rascunho** | `PUT /api/v1/backoffice/plans/versions/{id}` | `plans.write` | PlatformOwner, FinanceOps |
| **Publicar Versão** | `POST /api/v1/backoffice/plans/versions/{id}/publish` | `plans.publish` | PlatformOwner, FinanceOps |

---

## 5. Auditoria Administrativa

Todas as operações de criação, atualização de rascunho e publicação geram registros imutáveis em `AuditLog`:
- `PlanCatalog.Created`
- `PlanVersion.Created`
- `PlanVersion.UpdatedDraft`
- `PlanVersion.Published`

Cada log armazena o id do administrador (`PerformedByAdminUserId`), e-mail, IP e payload JSON das alterações efetuadas.
