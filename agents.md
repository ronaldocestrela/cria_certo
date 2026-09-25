# System Instructions & Agent Guide: Cattle / Bovine Management SaaS Platform

## 1. Project Overview & Vision
This project is a high-performance, enterprise-grade, multi-tenant Cattle/Bovine Management SaaS platform (targeting beef and dairy cattle management). 

The platform optimizes cattle farm operations across the entire lifecycle: Breeding/Cow management, Reproduction & Artificial Insemination (IATF), Gestation, Calving & Nursery, Weight Gain & Growth (Pasture / Feedlot / Confinamento), Nutrition/Feed management, Sanitary/Veterinary workflows, and Advanced Zootecnic Analytics (Calving Interval, ADG/GPD, Feed Conversion Ratio, Stocking Rate / Taxa de Lotação UA/ha).

---

## 2. Architecture & Design Principles

### 2.1 Architectural Style: Modular Monolith
* **Structure:** Single unit composed of strictly separated, domain-aligned internal modules.
* **Communication:** In-process domain events and mediator patterns. Direct cross-module database joins are **prohibited**.
* **Tiered Feature Gating:** Modules are dynamically enabled/disabled based on tenant subscription plans (e.g., *Starter* = Breeding & Herd Registry; *Enterprise* = Full Zootecnic Analytics + Financials + Feedlot/Confinamento Optimization).

### 2.2 Core Architectural Requirements
* **Backend:** .NET 10 (C# 14) following Clean Architecture & Domain-Driven Design (DDD).
* **Frontend:** Blazor WebAssembly / Blazor Web App targeting .NET 10 with **PWA (Progressive Web App)** capabilities for offline resilience in field/pasture environments.
* **Design & UI Automation:** **MCP Stitch** integration for automated, standardized Blazor UI component generation.
* **Development Methodology:** **Test-Driven Development (TDD)** — Red/Green/Refactor mandatory for all business code.
* **Documentation Strategy:** **Living Documentation** — Automated generation and execution of specs via BDD/Tests. Architecture Decision Records (ADRs) and markdown docs must be kept up-to-date with code changes.
* **Error & Response Pattern:** Mandatory **Result Pattern** (`Result<T>`) across all Backend layers — **NO Exceptions for Control Flow**.

---

## 3. Technology Stack & Specifications

### 3.1 Backend (.NET 10)
* **Framework:** .NET 10 Web API / Modular Monolith Architecture.
* **Language:** C# 14.
* **Domain & CQRS:** MediatR / In-Memory Channel Bus.
* **Data Access:** Entity Framework Core 10, PostgreSQL (Multi-tenant schema-per-tenant or row-level security).
* **Functional Error Handling:** Custom or CSharpFunctionalExtensions `Result<T>` / `Result<T, Error>`.
* **Validation:** FluentValidation integrated into MediatR pipeline.
* **Testing Stack:** xUnit, FluentAssertions, NSubstitute / Moq, Testcontainers (PostgreSQL integration tests).

### 3.2 Frontend (Blazor .NET 10)
* **Framework:** Blazor Web App / WASM .NET 10 with PWA offline caching strategy.
* **UI Components:** Reusable atomic component design system. Integration with **MCP Stitch** for component synthesis and UI standardization.
* **Offline Storage:** IndexedDB (via Blazored.LocalStorage or JS Interop) with IndexedDB-to-Server background synchronization workers.
* **State Management:** Fluxor or Custom Scoped State Stores.

---

## 4. Mandatory Backend Standard: The Result Pattern

Exceptions MUST NOT be used for domain validation or expected business rule failures. All application services, command handlers, query handlers, and domain methods must return `Result` or `Result<T>`.

### Key Interfaces & Base Types
```csharp
public record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
}

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
            throw new InvalidOperationException("Invalid Result state.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess 
        ? _value! 
        : throw new InvalidOperationException("The value of a failure result cannot be accessed.");
}
```

---

## 5. Domain Modules Specification

### 5.1 Breeding & Cattle Herd Management Module (`Modules.Breeding`)
* **Entities:** `Cow` (Vaca/Matriz), `Bull` (Touro/Reprodutor), `SemenBatch` (Sêmen/Palheta), `BreedingEvent` (Inseminação/IATF/Monta Natural).
* **Features:**
  * Artificial Insemination / Fixed-Time AI (IATF) protocol tracking.
  * Pregnancy diagnosis (ultrasound / rectal palpation).
  * Reproductive indicators: Calving Interval (IEP - Intervalo Entre Partos), Conception Rate, Open Days (Dias em Aberto).

### 5.2 Calving & Calf Nursery Module (`Modules.Calving`)
* **Entities:** `Calving` (Parto), `Calf` (Bezerro/a), `Weaning` (Desmame).
* **Features:**
  * Birth recording (birth weight, sex, coat/breed, mother tag ID, birth condition).
  * Calf identification (Ear Tag / SISBOV / RFID Chip / Tattoo).
  * Weaning recording & 205-day adjusted weight calculation.
  * Pre-weaning mortality & weaning rate per cow.

### 5.3 Growth, Pasture & Feedlot Module (`Modules.Growth`)
* **Entities:** `Lot` (Lote de Animais), `PasturePaddock` (Pasto/Piquete), `AnimalMovement`, `Weighing` (Pesagem/Arroba), `FeedlotPen` (Confinamento/Curral).
* **Features:**
  * Growth stages: Calf (Bezerro), Yearling (Novilho/Garrote), Fattening/Finishing (Boi Gordo/Recria/Engorda).
  * Animal Unit per Hectare (UA/ha - Taxa de Lotação) calculation per paddock.
  * Average Daily Gain (ADG / GPD - Ganho de Peso Diário em kg e @).
  * Mortality and culling tracking.

### 5.4 Nutrition & Feed Management Module (`Modules.Nutrition`)
* **Entities:** `FeedRation`, `PastureSupplementation` (Sal Mineral / Proteinado), `DailyFeedBatch` (Carregamento de Trato), `SiloStock`.
* **Features:**
  * Pasture mineral & protein supplementation logs.
  * Feedlot total mixed ration (TMR / Trato) delivery logs.
  * Feed Conversion Ratio (CA) and Cost per @ (Arroba) produced.

### 5.5 Sanitary & Veterinary Module (`Modules.Sanitary`)
* **Entities:** `VaccinationCampaign` (Febre Aftosa, Brucelose, Raiva, Clostridiose), `DewormingRecord` (Vermifugação), `TreatmentRecord`.
* **Features:**
  * Mandatory state/federal vaccination campaign tracking.
  * Individual/Lot medical treatments with slaughter withdrawal period (período de carência) blocks.

### 5.6 Tenant & Licensing Module (`Modules.Tenancy`)
* **Entities:** `Tenant`, `SubscriptionPlan`, `ModuleAccess`, `OperationalTag`, `TenantOperationalTag`.
* **Tenant lifecycle states:** `Trial`, `Active`, `PastDue`, `Suspended`, `Cancelled`, `Archived` — governed via `TenantLifecycle` with mandatory admin justification for transitions.
* **Operational segmentation (Backoffice 2.3):** `SizeSegment`, `CommercialRegion`, `ProductiveProfile`, `ChurnRisk` on `Tenant`; operational tags for support/CS/retention campaigns.
* **Features:**
  * Middleware for module availability verification per tenant.
  * Feature toggling and usage limits (e.g., max head of cattle supported per plan).
  * Producer access enforcement for blocked tenant statuses (`Suspended`, `Cancelled`, `Archived`).

### 5.7 Backoffice Module (`Modules.Backoffice`)
* **Bootstrap admin (seed):** Parametrizado via `.env` / variáveis de ambiente (`BACKOFFICE_MASTER_ADMIN_EMAIL`, `BACKOFFICE_MASTER_ADMIN_PASSWORD`, `BACKOFFICE_MASTER_ADMIN_NAME`) ou seção `Backoffice:MasterAdmin` do `appsettings.json`, com fallbacks padrão para `admin@criacerto.com.br` / `AdminPassword123!` — criado por `BackofficeDataSeeder` na inicialização da API.
* **Dev reset:** set `Backoffice:ResetBootstrapAdminPassword=true` in `appsettings.Development.json` to re-sync the bootstrap password on existing databases (never enable in production without explicit ops approval).
* **Login endpoint:** `POST /api/v1/backoffice/auth/login` returns `401` for invalid credentials (`Backoffice.InvalidCredentials`), not RBAC `403`.
* **Administrative Approvals (4-Eyes Principle):** Critical platform actions (`PublishPlanVersion`, `MassTenantSuspension`, `ExtendedAccessGrant`) must follow the dual-control principle (`AdminApprovalRequest`). The requester cannot self-approve (`ApprovalErrors.CannotSelfApprove`). Requests carry automatic TTL expiration (default 48h), mandatory justification (min 10 chars), execution payloads and visual diffs with full audit logging in `AuditLog`.
* **Forensic Audit & Tamper-Evident Hashing (Backoffice 5.1):** All administrative actions are recorded in structured `AuditLog` records with actor context, network metadata (IP, user agent), target tenants and before/after diffs (`OldValuesJson`, `NewValuesJson`). Every entry includes a canonical SHA-256 integrity hash (`RecordHash`) chained to the previous entry (`PreviousRecordHash`). Data retention policies are tiered by severity (`Critical`: permanent/1825d without auto-purge; `High`: 1095d archive; `Medium`: 365d archive; `Low`: 90d purge).
* **Observability & Anomaly Alerts (Backoffice 5.2):** Native .NET 10 telemetry via `Meter("CriaCerto.Modules.Backoffice")` and `ActivitySource` with MediatR pipeline behavior `BackofficeObservabilityBehavior` recording operation latency histograms and distributed spans. Proactive anomaly detection engine (`AnomalyDetectionEngine`) monitoring brute force/policy denials (`ALR_POLICY_BRUTE_FORCE`), off-hours critical actions (`ALR_OFF_HOURS_CRITICAL_ACTION`), impersonation surges (`ALR_IMPERSONATION_BURST`), and forensic hash chain tampering (`ALR_FORENSIC_TAMPER_DETECTED`). Managed via `BackofficeAlert` with fingerprint deduplication, alert triage cycle (`Active` ➔ `Acknowledged` ➔ `Resolved`), and `/backoffice/observability` console.
* **Compliance LGPD & Access Governance (Backoffice 5.3):** Full compliance with Brazilian General Data Protection Law (LGPD - Law 13.709/2018) via deterministic PII masking (`IPiiDataMasker` / `PiiDataMasker`) protecting CPFs, CNPJs, personal emails, phone numbers, IP addresses and sensitive JSON payloads by default (Privacy by Default). Granular permissions (`compliance.read`, `compliance.export`, `compliance.unmask`). Just-In-Time unmasking (`RevealSensitiveDataCommand`) requires mandatory justification (min 10 chars) and immediately records an immutable SHA-256 chained audit record (`Category = Compliance, Action = "PII_DATA_UNMASKED", Severity = High`). Formal access dossiers for external audits and regulatory inquiries (`ExportAccessTrailQuery`) generated with cryptographic signature and critical audit severity. Managed visually via `MaskedDataField.razor`, `RevealPiiModal.razor`, and `/backoffice/compliance` console.
* **Application Security & Negative Authorization Hardening (Backoffice 6.1):** Defense-in-depth architecture covering the OWASP API Security Top 10. Native .NET 10 rate limiting (`BackofficeAuthRateLimiter`) with 15 req/min on `/api/v1/backoffice/auth/login` and `/refresh` (`Backoffice.RateLimitExceeded`). Constant-time PBKDF2 dummy verification in `AuthenticateAdminUserCommandHandler` eliminating username enumeration via timing side-channels. Impersonation token containment in `BackofficeAccessMiddleware` preventing support tokens from accessing administrative endpoints (`Backoffice.ImpersonationRestricted`). Mandatory security headers (CSP, Permissions-Policy, HSTS, and `Cache-Control: no-store, no-cache` for all backoffice responses). Exhaustive negative authorization matrix (`BackofficeNegativeAuthorizationTests` and `BackofficeAuthenticationHardeningTests`) tagged with `[Trait("Category", "SecurityRegression")]` ensuring continuous CI/CD quality gate enforcement.
* **Wave Rollout & Feature Flag Governance (Backoffice 6.2):** Liberação progressiva por anéis (`Ring0_Canary`: `PlatformOwner`; `Ring1_EarlyAdopters`: `SupportN2`, `FinanceOps`; `Ring2_GeneralAvailability`: todos os operadores autorizados pelo RBAC). Avaliação determinística contextual via SHA-256 (`Key:AdminEmail % 100`) para rollouts percentuais sem oscilações na interface. Intercepção declarativa via `[RequireFeatureFlag("key")]` e `FeatureFlagEvaluationBehavior` no MediatR abortando comandos não liberados com `Result.Failure(FeatureFlagErrors)`. Regime de emergência (*Kill-Switch* / *Circuit Breaker*) instantâneo com registro compulsório em `AuditLog` com severidade crítica e encadeamento criptográfico SHA-256. Guardas client-side reativos (`FeatureFlagGuard.razor`), badges de estado (`RolloutWaveBadge.razor`) e console `/backoffice/rollout` com monitoramento contínuo de SLOs (taxa de erro e latência p95).
* **Operações, Playbooks e Resposta a Incidentes (Backoffice 6.3):** Procedimentos operacionais padrão (SOPs) consolidados em `/docs/operations/`: triagem N1, suporte assistido com dupla salvaguarda e remediação assistida (`playbook_support.md`); governança de planos, precificação e separação de funções via 4-Eyes (`playbook_finance.md`); runbooks de contenção emergencial (`runbook_incident_response.md`: RUNBOOK-01 a RUNBOOK-05 cobrindo Kill-Switch com corte em < 1s, violação de hash forense `ALR_FORENSIC_TAMPER_DETECTED`, mitigação de força bruta/rate-limiting e contenção de impersonação `Backoffice.ImpersonationRestricted`). Matriz RACI, trilhas de treinamento por perfil e termos de privilégios (`training_matrix_and_handoff.md`), homologados por simulação de mesa (*Tabletop Drill* - `tabletop_simulation_report.md`) e suíte de testes automatizada de simulação (`IncidentResponseSimulationTests`), formalizados no ADR `0014-operational-playbooks-incident-response-and-handoff.md`.

## 6. Software Engineering Practices & AI Agent Guidelines

### 6.1 TDD Workflow (Mandatory Enforcement)
Every feature implementation must strictly adhere to the TDD cycle:
1. **RED:** Write unit/integration tests defining expected behavior (e.g., verifying `Result.Failure` when input violates business rules).
2. **GREEN:** Write minimal production code required to pass tests.
3. **REFACTOR:** Clean up code, enforce design patterns, ensure module isolation.

### 6.2 Living Documentation & Documentation Drift Prevention
* **Living Documentation Rules:**
  * When adding or modifying an API endpoint, domain event, or business rule, you **MUST update the relevant markdown files** in `/docs/` during the same commit/iteration.
  * Maintain executable specifications using SpecFlow / Reqnroll or self-documenting xUnit tests.
  * Keep ADRs (Architecture Decision Records) updated in `/docs/adrs/`.

### 6.3 Frontend Development & MCP Stitch Protocols
* **Component Architecture:** All Blazor components must be modular, highly granular, and stateless where possible (smart parent / dumb child pattern).
* **MCP Stitch Integration:** Use MCP Stitch to generate consistent, accessible HTML/CSS component structures before binding them into Blazor `.razor` components.
* **PWA & Field Offline First:**
  * Mangueiro / Curral / Pasture registries (Pesagem, Vacinação, IATF, Partos) must be fully operational offline.
  * Offline actions must be queued in `IndexedDB` and processed through a background worker once connection is restored.

---

## 7. AI Agent Guidelines for Output Generation

When writing code as an AI assistant for this repository:
1. **Never throw exceptions for validation or business domain errors.** Always return `Result.Failure(Error)`.
2. **Check module boundaries.** Do not reference cross-module DB entities directly.
3. **Always write unit tests first or alongside code.**
4. **Update `agents.md` or related module documentation** whenever architectural decisions or business domain rules are updated.
