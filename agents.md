# System Instructions & Agent Guide: Cattle / Bovine Management SaaS Platform

## 1. Project Overview & Vision
This project is a high-performance, enterprise-grade, multi-tenant Cattle/Bovine Management SaaS platform (targeting beef and dairy cattle management). 

The platform optimizes cattle farm operations across the entire lifecycle: Breeding/Cow management, Reproduction & Artificial Insemination (IATF), Gestation, Calving & Nursery, Weight Gain & Growth (Pasture / Feedlot / Confinamento), Nutrition/Feed management, Sanitary/Veterinary workflows, and Advanced Zootecnic Analytics (Calving Interval, ADG/GPD, Feed Conversion Ratio, Stocking Rate / Taxa de Lotação UA/ha).

---

## 2. Architecture & Design Principles

### 2.1 Architectural Style: Modular Monolith
* **Structure:** Single deployable unit composed of strictly separated, domain-aligned internal modules (`CriaCerto.Modules.*`).
* **Isolation:** In-process domain events and MediatR commands/queries. Direct cross-module database joins or foreign keys are **strictly prohibited**. Inter-module data references use value IDs (`Guid`).
* **Tiered Feature Gating:** Modules are dynamically enabled/disabled per tenant subscription plan and subscribed module list (gated via `ModuleAccessBehavior` and `<ModuleGuard>`).

### 2.2 Core Architectural Requirements
* **Backend:** .NET 10 (C# 14) following Clean Architecture & Domain-Driven Design (DDD).
* **Frontend & Mobile Híbrido:** Blazor Web App / WASM .NET 10 com suporte PWA offline e app nativo Android & iOS via **.NET MAUI Blazor Hybrid (`BlazorWebView`)** no host `CriaCerto.Mobile`. Compartilhamento de componentes e fluxos na Razor Class Library (RCL) `CriaCerto.Shared.UI` desacoplada por abstrações de plataforma (ADRs 0028–0032).
* **Stateless API:** Toda a comunicação cliente-API é estritamente **stateless** via cabeçalho `Authorization: Bearer <token>` (ADR 0033).
* **Design & UI Automation:** **MCP Stitch** para síntese padronizada e componentes atômicos Blazor.
* **Development Methodology:** **Test-Driven Development (TDD)** — Red/Green/Refactor mandatório para todo código de negócio.
* **Documentation Strategy:** **Living Documentation** — Manter ADRs e documentações em `/docs/` rigorosamente atualizados a cada alteração.
* **Error & Response Pattern:** Mandatory **Result Pattern** (`Result<T>`) across all Backend layers — **NO Exceptions for Control Flow**.

---

## 3. Technology Stack & Specifications

### 3.1 Backend (.NET 10)
* **Framework:** .NET 10 Web API / Modular Monolith Architecture.
* **Language:** C# 14.
* **Domain & CQRS:** MediatR com pipeline behaviors para validação, autorização, observabilidade e feature flags.
* **Data Access:** Entity Framework Core 10, PostgreSQL com isolamento multi-tenant.
* **Functional Error Handling:** Result Pattern nativo em `CriaCerto.BuildingBlocks.Results`.
* **Validation:** FluentValidation integrado ao pipeline MediatR.
* **Testing Stack:** xUnit, FluentAssertions, NSubstitute / Moq, Testcontainers (PostgreSQL).

### 3.2 Frontend & Mobile Híbrido (Blazor .NET 10 & .NET MAUI)
* **Web:** Blazor Web App / WebAssembly com Service Worker e cache PWA offline.
* **Mobile Nativo:** .NET MAUI Blazor Hybrid (`CriaCerto.Mobile`) para Android e iOS (ADR 0030).
* **Shared UI (RCL):** `CriaCerto.Shared.UI` (`net10.0`) encapsulando páginas, componentes, layouts e API clients (ADR 0028).
* **Platform Abstractions:** Contratos em `CriaCerto.Shared.UI.Abstractions` (`ITokenStorageService`, `IFileExportService`, `IConnectivityService`, `IOfflineSyncStorage`) com implementações Web (ADR 0029) e Mobile (ADR 0031).
* **Offline Storage:** IndexedDB no browser via JSInterop (`offlineSync.js`); SQLite / SecureStorage no mobile.
* **CI/CD Mobile:** GitHub Actions em `.github/workflows/mobile-build.yml` com jobs heterogêneos para Android e iOS (ADR 0034).

### 3.3 Transactional Email Provider (Resend)
* **Provider:** Resend REST API v1 (`POST https://api.resend.com/emails`).
* **Abstractions:** `IEmailSender`, `EmailMessage`, `EmailRecipient`, `EmailSendResult` em `CriaCerto.BuildingBlocks.Abstractions.Email`.
* **Infrastructure:** `ResendEmailSender` e fallback `NullEmailSender` (ADR 0015).

---

## 4. Mandatory Backend Standard: The Result Pattern

Exceptions **MUST NOT** be used for domain validation or expected business rule failures. All application services, command handlers, query handlers, and domain methods must return `Result` or `Result<T>`.

### Uso Prático
A biblioteca base está localizada em `CriaCerto.BuildingBlocks.Results` / `CriaCerto.BuildingBlocks.Abstractions`.

```csharp
// Sucesso sem retorno de valor
return Result.Success();

// Sucesso com valor
return Result.Success(entityDto);

// Falha de validação ou domínio usando tipos tipados de Error
return Result.Failure(Error.Validation("Animal.InvalidTag", "O brinco informado é inválido."));
return Result.Failure(Error.NotFound("Animal.NotFound", "Matriz não encontrada."));
return Result.Failure(Error.Conflict("Animal.TagInUse", "Brinco já cadastrado no rebanho."));
```

---

## 5. Domain Modules Specification

Cada módulo de negócio possui seu próprio ciclo de vida, modelos e contexto de persistência. A documentação aprofundada de cada módulo reside em `/docs/modules/` e nos respectivos ADRs em `/docs/adrs/`.

### 5.1 Breeding & Cattle Herd Management (`Modules.Breeding`)
* **Entidades:** `Cow` (Matriz), `Bull` (Touro), `SemenBatch` (Sêmen/Palheta), `BreedingEvent` (Inseminação/IATF/Monta Natural).
* **Capacidades:** Protocolos IATF, diagnóstico de gestação, métricas reprodutivas (IEP, Taxa de Concepção, Dias em Aberto).
* **Docs:** [docs/modules/breeding.md](file:///home/rony/LPR/CriaCerto/docs/modules/breeding.md).

### 5.2 Calving & Calf Nursery (`Modules.Calving`)
* **Entidades:** `Calving` (Parto), `Calf` (Bezerro/a), `Weaning` (Desmame).
* **Capacidades:** Registro de nascimentos, identificação (Brinco/SISBOV/RFID), desmame, peso ajustado aos 205 dias, taxa de mortalidade pré-desmame.
* **Docs:** [docs/modules/calving.md](file:///home/rony/LPR/CriaCerto/docs/modules/calving.md).

### 5.3 Growth, Pasture & Feedlot (`Modules.Growth`)
* **Entidades:** `Lot` (Lote), `PasturePaddock` (Piquete), `AnimalMovement`, `Weighing` (Pesagem/@), `FeedlotPen` (Curral de Confinamento).
* **Capacidades:** Fases (Cria, Recria, Engorda), cálculo de UA/ha (Taxa de Lotação), GPD / ADG (Ganho de Peso Diário).
* **Docs:** [docs/modules/growth.md](file:///home/rony/LPR/CriaCerto/docs/modules/growth.md).

### 5.4 Nutrition & Feed Management (`Modules.Nutrition`)
* **Entidades:** `FeedRation`, `PastureSupplementation` (Sal Mineral / Proteinado), `DailyFeedBatch` (Trato Diário), `SiloStock`.
* **Capacidades:** Suplementação a pasto, trato de confinamento (TMR), Conversão Alimentar (CA) e Custo por @ produzida.
* **Docs:** [docs/modules/nutrition.md](file:///home/rony/LPR/CriaCerto/docs/modules/nutrition.md), [ADR 0003](file:///home/rony/LPR/CriaCerto/docs/adrs/0003-nutrition-and-feed-conversion-analytics.md).

### 5.5 Sanitary & Veterinary (`Modules.Sanitary`)
* **Entidades:** `VaccinationCampaign` (Aftosa, Brucelose, Raiva, Clostridiose), `DewormingRecord`, `TreatmentRecord`.
* **Capacidades:** Campanhas sanitárias obrigatórias, tratamentos clínicos e bloqueio preventivo de período de carência (slaughter withdrawal).
* **Docs:** [docs/modules/sanitary.md](file:///home/rony/LPR/CriaCerto/docs/modules/sanitary.md).

### 5.6 Tenant & Licensing (`Modules.Tenancy`)
* **Entidades:** `Tenant`, `User`, `TenantSubscribedModule`, `SubscriptionPlan`, `TenantSubscriptionHistory`.
* **Capacidades:**
  * Multi-tenancy, isolamento de dados e ciclo de vida do tenant (`Trial`, `Active`, `PastDue`, `Suspended`, `Cancelled`, `Archived` - ADR 0005).
  * Catálogo Modular Self-Service e precificação por faixas de cabeças (`TIER_500` a `TIER_5000_PLUS`) com módulo core `Cows` mandatório (ADR 0027).
  * Gating dinâmico via `ITenantContext`, claims JWT e middleware `ModuleAccessBehavior`.
  * Governança LGPD: confirmação de e-mail (ADR 0016), retificação (ADR 0017), portabilidade JSON (ADR 0018), exclusão/anonimização (ADR 0019), DPA B2B (ADR 0022), expurgo automatizado de dados temporários (ADR 0023) e canal DSR/DPO em `/dpo` (ADR 0024).
* **Docs:** [docs/modules/tenancy.md](file:///home/rony/LPR/CriaCerto/docs/modules/tenancy.md), [ADR 0027](file:///home/rony/LPR/CriaCerto/docs/adrs/0027-modular-self-service-and-tiered-head-pricing.md).

### 5.7 Backoffice Module (`Modules.Backoffice`)
* **Entidades:** `AdminUser`, `PlanCatalog`, `ModuleCatalog`, `HeadTierCatalog`, `AdminApprovalRequest`, `AuditLog`, `BackofficeAlert`.
* **Capacidades:**
  * Gestão do catálogo modular (preços mensais/anuais vinculados ao Stripe) e governança de planos (ADR 0027).
  * Princípio 4-Eyes (`AdminApprovalRequest`) para ações críticas de plataforma com justificativa e TTL (ADR 0008).
  * Trilha de auditoria forense estruturada com encadeamento criptográfico SHA-256 (`RecordHash` encadeado) e políticas de retenção escalonadas (ADR 0009).
  * Observabilidade e detecção proativa de anomalias (`AnomalyDetectionEngine`) via telemetria nativa .NET 10 (ADR 0010).
  * Privacidade e conformidade LGPD: mascaramento determinístico de PII (`IPiiDataMasker`) e revelação Just-In-Time com auditoria (ADR 0011).
  * Hardening de segurança OWASP: rate limiting nativo, mitigação de side-channel timing e contenção de impersonation (ADR 0007, 0012).
  * Governança de rollout por anéis (`Ring0_Canary` a `Ring2_GA`) e Kill-Switch emergencial instantâneo (ADR 0013).
  * Playbooks operacionais consolidados e resposta a incidentes de privacidade ANPD (ADRs 0014, 0025, [docs/operations/](file:///home/rony/LPR/CriaCerto/docs/operations)).
* **Docs:** [docs/modules/backoffice_plans.md](file:///home/rony/LPR/CriaCerto/docs/modules/backoffice_plans.md), [docs/modules/backoffice_billing.md](file:///home/rony/LPR/CriaCerto/docs/modules/backoffice_billing.md).

### 5.8 Referral & Parceiros Comerciais (`Modules.Referral`)
* **Entidades:** `ReferralProgramParticipant`, `PartnerContract`, `ReferralRewardLedger`, `PaidMediaSpend`.
* **Capacidades:**
  * Programa "Indique e Ganhe" para produtores (descontos vinculados à faixa de cabeças ou fatura total) e parceiros comerciais (comissões financeiras).
  * Carência estrita D+30 contra estornos, conciliação e repasses Pix no Backoffice com remessa estruturada em CSV com BOM UTF-8.
  * Proteção contra fraudes: bloqueio de autofiliação (mesmo CPF/CNPJ/e-mail), colisão de cartão de crédito no Stripe e teto de 100% de desconto.
  * Criptografia em repouso com algoritmo **AES-256-GCM** e **Blind Indexing (HMAC-SHA256)** para busca $O(1)$ de PIIs financeiras (ADR 0020).
  * Telemetria e consolidação de KPIs (CAC, LTV, $K\text{-Factor}$) no console analítico `/backoffice/referral/analytics`.
* **Docs:** [docs/modules/referral.md](file:///home/rony/LPR/CriaCerto/docs/modules/referral.md), [ADR 0020](file:///home/rony/LPR/CriaCerto/docs/adrs/0020-lgpd-referral-at-rest-encryption-and-blind-indexing.md).

---

## 6. Architecture Decision Records (ADRs) Quick Reference

Consulte o arquivo de ADR específico em [docs/adrs/](file:///home/rony/LPR/CriaCerto/docs/adrs) para obter os requisitos detalhados de cada decisão:

| ADR | Título / Escopo |
| :--- | :--- |
| **0001 - 0003** | Modular Monolith Foundation, Identity & Multi-tenancy, Nutrição e Zootecnia |
| **0004** | Offline-First IndexedDB Sync Manager & Service Worker |
| **0005 - 0006** | Tenant Lifecycle State Machine e Segmentação Operacional |
| **0007 - 0013** | Governança Backoffice: Dupla Salvaguarda, 4-Eyes, Auditoria Forense SHA-256, Observabilidade, Máscaras PII LGPD, Hardening OWASP e Wave Rollout |
| **0014** | Operational Playbooks, Incident Response e Handoff Operacional |
| **0015 - 0016** | Provedor de E-mail Transacional Resend e Workflow de Confirmação de E-mail |
| **0017 - 0026** | Pacote LGPD: Retificação, Portabilidade, Anonimização, Criptografia AES-GCM/Blind Indexing, Sanitização PII, DPA B2B, Expurgo Automatizado, Canal DPO/DSR, Resposta a Incidentes ANPD e ROPA/RIPD |
| **0027** | Catálogo Modular Self-Service e Precificação por Faixas de Cabeças |
| **0028 - 0034** | Arquitetura Mobile Híbrida: RCL Shared UI, Abstrações de Plataforma, Host .NET MAUI, Serviços Nativos, Segurança Android/iOS, API Stateless Bearer JWT e CI/CD Multiplataforma |

---

## 7. Software Engineering Practices & AI Agent Guidelines

### 7.1 TDD Workflow (Mandatory Enforcement)
Todo desenvolvimento deve seguir rigorosamente o ciclo Red-Green-Refactor:
1. **RED:** Escrever testes unitários/integração que capturem o comportamento esperado (ex.: `Result.Failure` para violações de regra de negócio).
2. **GREEN:** Escrever o código de produção mínimo indispensável para os testes passarem.
3. **REFACTOR:** Limpar o código, aplicar boas práticas e garantir o isolamento estrito dos módulos.

### 7.2 Living Documentation & Documentation Drift Prevention
* Ao adicionar ou modificar endpoints, eventos de domínio, entidades ou regras de negócio, os arquivos em `/docs/` **DEVEM ser atualizados no mesmo commit/iteração**.
* Mantenha especificações executáveis em testes xUnit autodocumentados.
* Atualize ou crie novos ADRs em `/docs/adrs/` sempre que uma decisão de arquitetura for tomada ou alterada.

### 7.3 Frontend Development & MCP Stitch Protocols
* **Component Architecture:** Componentes Blazor granulares, modulares e desacoplados (padrão smart parent / dumb child).
* **MCP Stitch Integration:** Usar MCP Stitch para prototipar/sintetizar estruturas HTML/CSS antes de ligá-las aos componentes Blazor `.razor`.
* **PWA & Field Offline-First:**
  * Telas de manejo de campo (Pesagem, Vacinação, IATF, Partos) devem operar offline.
  * Operações offline são enfileiradas (`offlineSync.js` no Web / SQLite no Mobile) e sincronizadas via `POST /api/v1/sync/{moduleName}/{actionType}` (ADR 0004).
  * Service Workers nunca devem interceptar rotas `/api/*` ou `/health`.

---

## 8. AI Agent Rules for Output Generation

Ao atuar como assistente de IA neste repositório:
1. **Nunca lance exceções para validação ou regras de domínio.** Retorne sempre `Result.Failure(Error)`.
2. **Respeite as fronteiras modulares.** Nunca execute joins entre tabelas de módulos diferentes nem referencie DbContexts externos. Use IDs primitivos (`Guid`).
3. **Escreva testes junto com o código (TDD).** Não entregue implementações sem sua respectiva suíte de testes.
4. **Pratique Progressive Disclosure.** Consulte os arquivos específicos em `/docs/modules/` e `/docs/adrs/` quando precisar de detalhes de implementação de um módulo específico, mantendo este guia (`agents.md`) conciso e focado em princípios transversais.
