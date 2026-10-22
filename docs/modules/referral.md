# Módulo de Indicações e Parceiros Comerciais (`Modules.Referral`)

## Visão Geral
O módulo `Modules.Referral` é responsável por orquestrar o **Programa de Indicação do CriaCerto**, cobrindo tanto clientes ativos (programa "Indique e Ganhe" com descontos em fatura) quanto parceiros comerciais (consultores, veterinários, revendas e associações com repasse de comissão financeira).

O módulo opera em nível de plataforma (Master Context), no banco de dados principal (`criacerto_foundation`), com isolamento no schema SQL dedicado `[referral]`.

---

## 1. Arquitetura e Limites de Fronteira

* **Estilo Arquitetural:** Monólito Modular com Clean Architecture e Domain-Driven Design (DDD).
* **Isolamento de Banco:** Mapeado no schema `[referral]` com controle de migrações em `[referral].[__EFMigrationsHistory]`.
* **Fronteiras Modulares:** Referências a outros módulos (ex: `TenantId`, `UserId` do módulo `Tenancy`, e `PlanCatalogId` do `Backoffice`) são mantidas estritamente como tipos de valor (`Guid`), sendo proibidas chaves estrangeiras ou navegações físicas diretas cross-module via Entity Framework Core.
* **Error & Response Pattern:** 100% de conformidade com o `Result<T>` pattern.

---

## 2. Entidades de Domínio e Tabelas do Schema `[referral]`

### 2.1 `ReferralProgramParticipant` (`[referral].[Participants]`)
Representa o participante ativo ou suspenso no programa. Pode ser um cliente (`TenantId` preenchido) ou um parceiro comercial cadastrado pela administração (`CreatedByAdminId` preenchido, `TenantId` nulo).

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `TenantId`: `uniqueidentifier?` (opcional, preenchido para clientes)
  - `UserId`: `uniqueidentifier?` (opcional)
  - `ParticipantType`: `nvarchar(30)` (`Customer` | `CommercialPartner`)
  - `Status`: `nvarchar(30)` (`Active` | `Suspended`)
  - `SuspensionReason`: `nvarchar(500)?`
  - `CreatedByAdminId`: `uniqueidentifier?`
  - `CreatedAtUtc`: `datetime2`
  - `UpdatedAtUtc`: `datetime2?`
* **Índices**: `TenantId`, `UserId`, `Status`, `ParticipantType`, `CreatedAtUtc`.

### 2.2 `ReferralCode` (`[referral].[ReferralCodes]`)
Código alfanumérico ou slug único para geração de links rastreáveis (`?ref=CODIGO`).

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `ParticipantId`: `uniqueidentifier` (FK interna para `Participants.Id`)
  - `Code`: `nvarchar(50)` (único, normalizado em maiúsculas)
  - `CampaignTitle`: `nvarchar(150)?` (identificador de canal/campanha)
  - `ClicksCount`: `bigint` (contador de acessos ao link)
  - `IsActive`: `bit` (default `true`)
  - `CreatedAtUtc`: `datetime2`
* **Índices**: Unique index em `Code`, índices em `ParticipantId` e `IsActive`.

### 2.3 `PartnerContract` (`[referral].[PartnerContracts]`)
Contratos comerciais individualizados para parceiros, com histórico de versões e vigência.

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `ParticipantId`: `uniqueidentifier` (FK interna para `Participants.Id`)
  - `CommissionType`: `nvarchar(40)` (`PercentageRecurring`, `PercentageFirstMonth`, `FixedPerConversion`)
  - `CommissionValue`: `decimal(18, 4)`
  - `MaxRecurringMonths`: `int?`
  - `PaymentTermsDays`: `int` (default `30` dias)
  - `PixKey`: `nvarchar(150)`
  - `PixKeyType`: `nvarchar(30)` (`Cpf`, `Cnpj`, `Email`, `Phone`, `RandomKey`)
  - `LegalDocumentNumber`: `nvarchar(30)` (CPF ou CNPJ)
  - `ValidFromUtc`: `datetime2`
  - `ValidToUtc`: `datetime2?` (nulo enquanto vigente)
  - `IsActive`: `bit`
  - `CreatedAtUtc`: `datetime2`
* **Índices**: `ParticipantId`, `IsActive`, índice composto `(ValidFromUtc, ValidToUtc)`.

### 2.4 `PlanReferralDiscountPolicy` (`[referral].[PlanDiscountPolicies]`)
Parametrização das políticas de desconto concedidas aos clientes por indicação ativa com base no plano contratado.

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `PlanCatalogId`: `uniqueidentifier`
  - `PlanCode`: `nvarchar(50)`
  - `DiscountType`: `nvarchar(30)` (`FixedValue`, `Percentage`)
  - `DiscountValue`: `decimal(18, 2)`
  - `MaxDiscountCapPercentage`: `decimal(5, 2)` (default `100.00%`)
  - `IsActive`: `bit`
  - `CreatedAtUtc`: `datetime2`
* **Índices**: `PlanCatalogId`, `PlanCode`, `IsActive`.

### 2.5 `ReferralAttribution` (`[referral].[Attributions]`)
Rastreia a atribuição da indicação originada em onboarding ou checkout.

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `ReferralCodeId`: `uniqueidentifier` (FK interna para `ReferralCodes.Id`)
  - `ReferrerParticipantId`: `uniqueidentifier` (FK interna para `Participants.Id`)
  - `ReferredTenantId`: `uniqueidentifier` (Tenant que se cadastrou)
  - `AttributionDateUtc`: `datetime2`
  - `ConvertedAtUtc`: `datetime2?` (preenchido após confirmação do 1º pagamento)
  - `Status`: `nvarchar(30)` (`Pending`, `Converted`, `Churned`, `Fraudulent`)
  - `OriginIpAddress`: `nvarchar(45)?`
  - `UserAgent`: `nvarchar(500)?`
  - `DeviceFingerprintHash`: `nvarchar(128)?`
  - `FraudReason`: `nvarchar(500)?`
* **Índices**: `ReferredTenantId`, `ReferralCodeId`, `ReferrerParticipantId`, `Status`, `AttributionDateUtc`, índice composto `(ReferredTenantId, Status)`.

### 2.6 `ReferralRewardLedger` (`[referral].[RewardLedger]`)
Livro-razão contábil e imutável para controle de créditos em fatura e comissões a pagar.

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `ReferralAttributionId`: `uniqueidentifier` (FK interna para `Attributions.Id`)
  - `ParticipantId`: `uniqueidentifier` (FK interna para `Participants.Id`)
  - `RewardType`: `nvarchar(30)` (`InvoiceDiscountCredit`, `CashCommission`)
  - `GrossAmount`: `decimal(18, 2)`
  - `NetAmount`: `decimal(18, 2)`
  - `Currency`: `nvarchar(3)` (default `BRL`)
  - `Status`: `nvarchar(30)` (`PendingMaturity`, `AvailableForPayout`, `AppliedToInvoice`, `PaidOut`, `Cancelled`)
  - `MaturityDateUtc`: `datetime2?` (D+30 para comissões)
  - `StripeInvoiceId`: `nvarchar(100)?`
  - `StripeCreditBalanceTransactionId`: `nvarchar(100)?`
  - `PayoutTransactionReference`: `nvarchar(150)?`
  - `CancellationReason`: `nvarchar(500)?`
  - `CreatedAtUtc`: `datetime2`
  - `UpdatedAtUtc`: `datetime2?`
* **Índices**: `ParticipantId`, `ReferralAttributionId`, `Status`, `MaturityDateUtc`, `StripeInvoiceId`, índice composto `(Status, MaturityDateUtc)`.

---

## 3. Contexto EF Core e Migrações

- **Abstração:** `IReferralDbContext` localizada em `CriaCerto.Modules.Referral.Application.Abstractions`.
- **Implementação:** `ReferralDbContext` localizada em `CriaCerto.Modules.Referral.Infrastructure.Persistence`.
- **Fábrica de Design-Time:** `ReferralDbContextFactory` localizada em `.../Persistence/DesignTimeDbContextFactory.cs`.
- **Baseline Metadata:** Registrado em `MigrationBaselineMetadata.Referral` para auditoria e controle de integridade da base.
