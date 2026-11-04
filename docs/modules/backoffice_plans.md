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

---

## 6. Consumo Público e Single Source of Truth

O catálogo comercial configurado no Backoffice é a fonte única da verdade para a exibição de planos na plataforma:
1. **Provedor de Planos (`BackofficeSubscriptionPlansProvider`):**
   - Implementa `ISubscriptionPlansProvider` do módulo Tenancy.
   - Disponibiliza os planos ativos na rota pública `GET /api/v1/tenancy/plans`.
2. **Assistente de Onboarding (`OnboardingWizard.razor`):**
   - Carrega dinamicamente a lista de planos via `TenancyApiClient.GetSubscriptionPlansAsync()`.
   - Permite seleção de planos com base no catálogo atualizado, preenchendo automaticamente capacidades e valores do período de teste de 30 dias.
3. **Gestão de Assinatura (`/settings/subscription` - `SubscriptionManagement.razor`):**
   - Apresenta os cards de planos, limites de cabeças e matriz comparativa de funcionalidades dinamicamente conforme os preços e módulos cadastrados no Backoffice.

---

## 7. Catálogo Modular Self-Service e Precificação por Faixas de Cabeças (Fase 1.1)

A partir da migração para **Assinatura Self-Service Modular (À La Carte)**, o `Modules.Backoffice` incorpora três entidades de catálogo comercial desacopladas por faixa de cabeças e módulo:

### 7.1. Entidades do Catálogo Modular
- **`HeadTierCatalog` (`backoffice.HeadTierCatalogs`):**
  - Define as faixas comerciais de capacidade de cabeças (`TIER_500`, `TIER_1500`, `TIER_3000`, `TIER_5000_PLUS`).
  - Atributos: `Id`, `TierCode` (único), `DisplayName`, `MinHeads`, `MaxHeads`, `SortOrder`, `IsActive`.
- **`ModuleCatalog` (`backoffice.ModuleCatalogs`):**
  - Define cada módulo comercializável da plataforma (`Cows`, `Breeding`, `Calving`, `Growth`, `Sanitary`, `Nutrition`, `Analytics`).
  - Atributos: `Id`, `ModuleKey` (único), `Name`, `Description`, `Icon`, `IsCore` (`true` apenas para `Cows` / Plantel Base, protegido contra desativação via `PlanErrors.CannotDeactivateCoreModule`), `IsActive`, `SortOrder`.
- **`ModuleTierPrice` (`backoffice.ModuleTierPrices`):**
  - Tabela versionada e imutável relacionando `ModuleCatalog` + `HeadTierCatalog`.
  - Atributos: `Id`, `ModuleCatalogId`, `HeadTierCatalogId`, `VersionNumber`, `Status` (`Draft`, `Published`, `Deprecated`), `MonthlyPrice`, `AnnualPriceMonthly`, `StripePriceIdMonthly`, `StripePriceIdAnnual`, `PublishedAtUtc`, `PublishedByAdminId`, `ApprovalNotes`.
  - Impede múltiplos rascunhos simultâneos para o mesmo par `(ModuleCatalogId, HeadTierCatalogId)` e deprecia automaticamente a versão `Published` anterior daquela faixa ao publicar uma nova versão.

### 7.2. Endpoints do Catálogo Modular
| Operação | Endpoint | Permissão Exigida |
| :--- | :--- | :--- |
| **Listar Faixas de Cabeças** | `GET /api/v1/backoffice/catalog/tiers` | `plans.read` |
| **Criar Faixa de Cabeças** | `POST /api/v1/backoffice/catalog/tiers` | `plans.write` |
| **Atualizar Faixa de Cabeças** | `PUT /api/v1/backoffice/catalog/tiers/{tierId}` | `plans.write` |
| **Listar Módulos e Preços por Faixa** | `GET /api/v1/backoffice/catalog/modules` | `plans.read` |
| **Detalhar Módulo por ID** | `GET /api/v1/backoffice/catalog/modules/{moduleId}` | `plans.read` |
| **Criar Módulo no Catálogo** | `POST /api/v1/backoffice/catalog/modules` | `plans.write` |
| **Atualizar Módulo no Catálogo** | `PUT /api/v1/backoffice/catalog/modules/{moduleId}` | `plans.write` |
| **Criar Rascunho de Preço por Faixa** | `POST /api/v1/backoffice/catalog/modules/{moduleId}/prices` | `plans.write` |
| **Editar Rascunho de Preço** | `PUT /api/v1/backoffice/catalog/modules/prices/{priceId}` | `plans.write` |
| **Publicar Versão de Preço por Faixa** | `POST /api/v1/backoffice/catalog/modules/prices/{priceId}/publish` | `plans.publish` (+ flag `backoffice.feature.plan_publishing`) |

### 7.3. Seed Inicial (`BackofficeDataSeeder.SeedModularCatalogAsync`) e Ponte com Tenancy (`BackofficeSubscriptionPlansProvider.GetModularCatalogAsync`)
Popula de forma idempotente as **4 faixas de cabeças** (`TIER_500`, `TIER_1500`, `TIER_3000`, `TIER_5000_PLUS`), os **7 módulos** (`Cows` base obrigatório + 6 add-ons opcionais) e os **28 preços publicados (`ModuleTierPrice`)** correspondentes à matriz comercial. O serviço `BackofficeSubscriptionPlansProvider` implementa `GetModularCatalogAsync`, projetando as faixas e módulos ativos com seus respectivos preços publicados para consumo público no módulo `Tenancy` (`GET /api/v1/tenancy/catalog`).
