# Roadmap de Correções: Fluxo de Pagamento, Assinaturas e Cobrança Stripe (CriaCerto)

## 1. Visão Geral e Diagnóstico da Auditoria

Este documento consolida o plano de ação técnico para sanar as **vulnerabilidades de segurança, falhas de lógica de faturamento e inconsistências de ciclo de vida** identificadas na auditoria do ecossistema de pagamentos e assinaturas da plataforma **CriaCerto Bovino SaaS**.

### Objetivos Principais
1. **Eliminar brechas de acesso gratuito** (bypasses de pagamento e planos arbitrários no onboarding).
2. **Corrigir o bloqueio indevido de produtores pagantes** (descasamento entre nomes comerciais e chaves do `ModuleLicenseChecker`).
3. **Garantir a segurança criptográfica e idempotência dos webhooks** do Stripe.
4. **Automatizar a expiração de períodos de teste (*Trial*) e controle de inadimplência (*PastDue*)**.
5. **Manter a integridade de dados e auditoria** de subscrições tanto para o produtor quanto para o Backoffice.

---

## 2. Matriz de Priorização das Entregas

```
┌────────────────────────────────────────────────────────────────────────┐
│ Fase 1 (Urgente): Segurança, Bloqueio de Bypasses e Validação Cripto   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Fase 2 (Crítica): Correção de Bloqueio de Assinantes & Token JWT       │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Fase 3 (Alta): Idempotência de Webhook, Portal Sync e Auditoria        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Fase 4 (Alta): Worker de Expiração de Trial & Regras de Inadimplência  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Fase 5 (Média): Testes Automatizados (TDD), Webhook Mocks & Homologação│
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Detalhamento das Fases & Entregáveis

---

### Fase 1: Segurança, Bloqueio de Bypasses e Blindagem Criptográfica [PRIORIDADE 1]

#### 1.1. Eliminação do Bypass de Troca Gratuita de Plano
* **Problema Identificado:** O endpoint `PUT /api/v1/tenancy/subscription` (`ChangeSubscriptionPlanCommand`) permite que qualquer usuário autenticado altere o plano da fazenda para "Enterprise" sem cobrança, recebendo imediatamente um JWT atualizado. O frontend `SubscriptionManagement.razor` inclusive utiliza esse endpoint como fallback.
* **Ações no Backend (.NET 10):**
  * Remover o endpoint público `PUT /api/v1/tenancy/subscription` em `Program.cs`.
  * Tornar `ChangeSubscriptionPlanCommand` restrito ao módulo de Backoffice administrativo com autenticação e autorização por papel (`Admin` / `SupportSupervisor`).
  * Em caso de falha de conexão com o Stripe no checkout, retornar erro claro ao cliente em vez de conceder plano gratuito.
* **Ações no Frontend (Blazor WASM):**
  * Em `SubscriptionManagement.razor`, remover a chamada de fallback a `TenancyApiClient.ChangeSubscriptionPlanAsync`.
  * Exibir modal explicativo de indisponibilidade de faturamento caso o Stripe falhe.
* **Arquivos Impactados:**
  - `src/Host/CriaCerto.Api/Program.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/ChangeSubscriptionPlan/ChangeSubscriptionPlanCommand.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Services/TenancyApiClient.cs`

#### 1.2. Blindagem de Assinatura Criptográfica do Webhook Stripe [CONCLUÍDO]
* **Problema Identificado:** Se `Stripe:WebhookSecret` estivesse em branco ou nulo, o método `StripePaymentService.ProcessWebhookAsync` invocava `EventUtility.ParseEvent(...)` sem validar `Stripe-Signature`, aceitando payloads forjados de qualquer origem na web.
* **Ações no Backend Implementadas:**
  * `StripePaymentService.ProcessWebhookAsync` refatorado para **sempre exigir** a validação estrita via `EventUtility.ConstructEvent` com `WebhookSecret` e `Stripe-Signature`. Fallback inseguro removido por completo.
  * Validações antecipadas (*guard clauses*) adicionadas: rejeição imediata com `StripeWebhookResult(false, null, ...)` e emissão de logs de segurança críticos caso o segredo ou o cabeçalho de assinatura estejam ausentes.
  * Validação *fail-fast* de inicialização implementada via `AddOptions<StripeOptions>().Validate(...).ValidateOnStart()` em `AddTenancyInfrastructure`, bloqueando o início da aplicação em ambiente `Production` caso `STRIPE_WEBHOOK_SECRET` não esteja configurado.
  * Mapeamento de erros refinado em `ProcessStripeWebhookCommandHandler` retornando código de erro específico `Stripe.InvalidSignature`.
* **Testes Automatizados (TDD):**
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookSecurityTests.cs` (10 cenários cobrindo ausência de segredo, ausência de assinatura, assinatura inválida, validação criptográfica HMAC-SHA256 e validação de startup em produção/desenvolvimento).
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/DependencyInjection.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/ProcessStripeWebhook/ProcessStripeWebhookCommand.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookSecurityTests.cs`

#### 1.3. Controle de Acesso Baseado em Papel (RBAC) para Faturamento [CONCLUÍDO]
* **Problema Identificado:** Handlers de checkout e portal Stripe (`CreateCheckoutSessionCommand` e `CreatePortalSessionCommand`) validavam apenas a associação do usuário ao tenant, permitindo que operadores de curral, veterinários ou zootecnistas criassem sessões de checkout, alterassem planos ou acessassem o Customer Portal da fazenda.
* **Ações no Backend Implementadas:**
  * Nos handlers `CreateCheckoutSessionCommandHandler` e `CreatePortalSessionCommandHandler`, validação estrita de papel adicionada com padrão Result:
    ```csharp
    if (userTenant.Role != UserRole.Admin)
    {
        return Result.Failure<...>(Error.Unauthorized("Auth.ForbiddenBilling", "Apenas administradores da fazenda podem gerenciar planos e pagamentos."));
    }
    ```
  * Mapeamento de `ErrorType.Unauthorized` para `403 Forbidden` preservado na camada de transporte HTTP via `ToHttpResult`.
* **Ações no Frontend Implementadas (Blazor WebAssembly):**
  * Em `SubscriptionManagement.razor`, implementada detecção reativa de papel administrativo via `ClaimsPrincipal` (`user.IsInRole("Admin")` / `ClaimTypes.Role` / `Role`).
  * Inserido banner informativo de modo somente leitura para colaboradores não administrativos.
  * Bloqueado o botão de acesso ao portal do Stripe (`OpenBillingPortal`), exibindo selo de acesso restrito a administradores.
  * Botões de contratação e upgrade/downgrade de planos desabilitados com indicação de bloqueio ("Apenas Administradores") para perfis não administrativos.
  * Guardas defensivas adicionadas nos métodos `ChangePlan` e `OpenBillingPortal` bloqueando invocações diretas no client-side.
* **Testes Automatizados (TDD):**
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionBillingAuthorizationTests.cs` (10 cenários cobrindo negação para `Veterinario`, `OperadorCurral` e `Zootecnista` com código `Auth.ForbiddenBilling`, sucesso para `Admin`, e isolamento multi-tenant com `Auth.UnauthorizedTenant`).
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommand.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionPortal/CreatePortalSessionCommand.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionBillingAuthorizationTests.cs`

#### 1.4. Proteção contra Open Redirect em URLs de Checkout e Portal [CONCLUÍDO]
* **Problema Identificado:** `SuccessUrl`, `CancelUrl` e `ReturnUrl` eram repassados ao Stripe sem validação de domínio, possibilitando ataques de phishing pós-checkout (CWE-601).
* **Ações no Backend Implementadas:**
  * Criada a abstração [ISubscriptionUrlValidator](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Abstractions/ISubscriptionUrlValidator.cs) e o serviço [SubscriptionUrlValidator](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Services/SubscriptionUrlValidator.cs), validando estritamente as origens permitidas contra `Cors:AllowedOrigins` e permitindo caminhos relativos seguros normalizados com a autoridade confiável.
  * Bloqueio explícito de esquemas inseguros (`javascript:`, `data:`, `file:`, `ftp:`), URLs com protocolo relativo (`//attacker.com`), evasão por subdomínio (`https://criacerto.com.br.evil.com`) eUserInfo (`https://user:pass@evil.com`).
  * Criados validadores declarativos FluentValidation:
    - [CreateCheckoutSessionCommandValidator](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommandValidator.cs)
    - [CreatePortalSessionCommandValidator](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionPortal/CreatePortalSessionCommandValidator.cs)
  * Injetada validação defensiva em [CreateCheckoutSessionCommandHandler](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommand.cs) e [CreatePortalSessionCommandHandler](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionPortal/CreatePortalSessionCommand.cs), retornando `Result.Failure` com código `Subscription.InvalidRedirectUrl` (`ErrorType.Validation`) caso URLs externas não autorizadas sejam fornecidas.
  * Registrado `ISubscriptionUrlValidator` como singleton em [DependencyInjection.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/DependencyInjection.cs) com carga dinâmica de `Cors:AllowedOrigins` e URL de retorno padrão.
* **Testes Automatizados (TDD):**
  - [SubscriptionRedirectUrlValidatorTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionRedirectUrlValidatorTests.cs) (12 cenários cobrindo URLs absolutas autorizadas, relativas, esquemas maliciosos, subdomínios, fallbacks seguros e evasão de autenticação).
  - [SubscriptionCommandValidatorsTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionCommandValidatorsTests.cs) (6 cenários validando integração de regras FluentValidation para Checkout e Portal).
  - [SubscriptionBillingAuthorizationTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionBillingAuthorizationTests.cs) (cenários adicionais garantindo rejeição no handler com `Subscription.InvalidRedirectUrl`).
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Abstractions/ISubscriptionUrlValidator.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Services/SubscriptionUrlValidator.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommandValidator.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionPortal/CreatePortalSessionCommandValidator.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommand.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionPortal/CreatePortalSessionCommand.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/DependencyInjection.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionRedirectUrlValidatorTests.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionCommandValidatorsTests.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionBillingAuthorizationTests.cs`

---

### Fase 2: Consistência de Planos, Licenciamento e Token JWT [PRIORIDADE 2]

#### 2.1. Descasamento de Identificadores de Plano e Bloqueio de Acesso [CONCLUÍDO]
* **Problema Identificado:** No checkout, o backend enviava ao Stripe apenas o `Name` comercial ("Pro Fazenda"). No retorno do webhook, gravava `tenant.SubscribedPlan = "Pro Fazenda"`. O `ModuleLicenseChecker` buscava estritamente as chaves `"Starter"`, `"Pro"` e `"Enterprise"`, fazendo com que produtores pagantes tivessem o acesso negado aos módulos no MediatR pipeline e na interface Blazor.
* **Ações no Backend Implementadas:**
  * No [ModuleLicenseChecker](file:///home/rony/LPR/CriaCerto/src/BuildingBlocks/CriaCerto.BuildingBlocks.Abstractions/Licensing/ModuleLicenseChecker.cs), implementado o método `NormalizePlan(string? plan)` com mapeamento de aliases comerciais ("Pro Fazenda", "Starter Pecuária", "Enterprise Confinamento", "Plano Pro") e heurística resiliente para variações e versões de Backoffice (ex.: "Pro 2026.1"), com fallback seguro para "Starter".
  * Atualizado `ModuleLicenseChecker.HasAccess` para normalizar automaticamente o plano do tenant antes de verificar permissões na matriz `PlanAccess`, assegurando retrocompatibilidade total com contas legadas já gravadas.
  * Em [IStripePaymentService](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Abstractions/IStripePaymentService.cs) e [StripePaymentService](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs), atualizado `CreateCheckoutSessionAsync` para receber `string planId` canônico e `string planName` comercial, persistindo ambos em `sessionOptions.Metadata` e `SubscriptionData.Metadata`.
  * Em [CreateCheckoutSessionCommandHandler](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommand.cs), implementada resolução canônica antecipada: `canonicalPlanId = selectedPlan?.PlanId ?? ModuleLicenseChecker.NormalizePlan(request.PlanId);`, combinando com busca enriquecida no catálogo.
  * No webhook `HandleCheckoutSessionCompletedAsync` e em `HandleSubscriptionUpdatedAsync`, extraído preferencialmente `PlanId` com fallback para `PlanName` e aplicado `NormalizePlan` antes de gravar em `tenant.SubscribedPlan` e ajustar capacidade via `AdjustTenantCapacityForPlan`.
  * Em [CreateTenantCommand](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/CreateTenant/CreateTenantCommand.cs) e [ChangeSubscriptionPlanCommand](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/ChangeSubscriptionPlan/ChangeSubscriptionPlanCommand.cs), aplicada a normalização preventiva antes da persistência.
* **Ações no Frontend Implementadas (Blazor WebAssembly):**
  * Em [SubscriptionManagement.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor), atualizado `IsActivePlan` para comparar planos normalizados via `ModuleLicenseChecker.NormalizePlan`.
  * Normalizado `_activePlan` na inicialização do componente ao carregar claims JWT ou perfil do tenant, garantindo consistência no cálculo de cotas (`_maxAnimalsAllowed` e `_maxReportsAllowed`).
* **Testes Automatizados (TDD):**
  - [ModuleLicenseCheckerTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.BuildingBlocks.UnitTests/Licensing/ModuleLicenseCheckerTests.cs) (cobertura exaustiva de canônicos, aliases comerciais, versões do Backoffice, nulos e avaliação de permissões).
  - [FeatureGatingIntegrationTests.cs](file:///home/rony/LPR/CriaCerto/tests/Integration/CriaCerto.Architecture.IntegrationTests/FeatureGatingIntegrationTests.cs) (testes de integração no pipeline MediatR para "Pro Fazenda" e "Enterprise Confinamento").
  - [SubscriptionBillingAuthorizationTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionBillingAuthorizationTests.cs) (validação do repasse de `PlanId` e `PlanName` no handler).
  - [StripeWebhookPlanMappingTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookPlanMappingTests.cs) (validação de webhook Stripe com payload `PlanId`, fallback de `PlanName` legado, plano Enterprise e sincronização de update de subscrição).
* **Arquivos Impactados:**
  - `src/BuildingBlocks/CriaCerto.BuildingBlocks.Abstractions/Licensing/ModuleLicenseChecker.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Abstractions/IStripePaymentService.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommand.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/CreateTenant/CreateTenantCommand.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/ChangeSubscriptionPlan/ChangeSubscriptionPlanCommand.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor`
  - `tests/Unit/CriaCerto.BuildingBlocks.UnitTests/Licensing/ModuleLicenseCheckerTests.cs`
  - `tests/Integration/CriaCerto.Architecture.IntegrationTests/FeatureGatingIntegrationTests.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionBillingAuthorizationTests.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookPlanMappingTests.cs`

#### 2.2. Prevenção de Assinaturas Concorrentes (Cobrança Dupla)
* **Problema Identificado:** Se uma fazenda já possui `StripeSubscriptionId` ativo e solicita um novo checkout, o Stripe cria uma nova assinatura simultânea para o mesmo cliente, cobrando duplamente.
* **Ações no Backend:**
  * No `CreateCheckoutSessionCommandHandler`, checar se `tenant.StripeSubscriptionId` já existe e se o status é ativo.
  * Se já houver assinatura ativa, retornar erro de domínio instruindo a transição de plano pelo Customer Portal, ou redirecionar automaticamente para a sessão do portal de gestão de faturamento.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommand.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor`

#### 2.3. Renovação de Claims e JWT no Retorno do Stripe
* **Problema Identificado:** Ao concluir a assinatura no Stripe e ser redirecionado para `/settings/subscription?success=true`, o usuário continua com o token JWT antigo gravado no navegador, mantendo o status desatualizado até efetuar logout.
* **Ações no Backend:**
  * Criar endpoint `POST /api/v1/auth/refresh-token` (ou re-emissão de claims atualizadas para o usuário e tenant ativo).
* **Ações no Frontend:**
  * No `SubscriptionManagement.razor`, ao detectar `success=true` na query string, chamar a atualização de token e acionar `AuthStateProvider.MarkUserAsAuthenticated(newToken)`.
* **Arquivos Impactados:**
  - `src/Host/CriaCerto.Api/Program.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Services/TenancyApiClient.cs`

---

### Fase 3: Idempotência de Webhook, Sincronização do Portal e Auditoria [PRIORIDADE 3]

#### 3.1. Tabela de Idempotência para Eventos Stripe
* **Problema Identificado:** O Stripe reenvia webhooks em retentativas automáticas. Não há deduplicação de eventos pelo `stripeEvent.Id`.
* **Ações no Backend:**
  * Criar entidade `StripeWebhookEvent` no schema `tenancy` com campos:
    - `EventId` (chave única / indexada, ex: `evt_...`)
    - `EventType` (ex: `invoice.paid`)
    - `ProcessedAtUtc` (DateTime)
    - `PayloadJson`
  * No `StripePaymentService.ProcessWebhookAsync`, verificar se o evento já foi processado antes de executar handlers; se sim, responder `Success` imediatamente sem re-executar operações em banco.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Persistence/TenancyDbContext.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`
  - Nova migration de banco de dados (`AddStripeWebhookEventsTable`).

#### 3.2. Sanitização de Chaves Nulas e Proteção de Tenants
* **Problema Identificado:** Consultas com `t.StripeCustomerId == invoice.CustomerId` sem validar string nula podem associar faturas ao primeiro tenant com `StripeCustomerId == null`.
* **Ações no Backend:**
  * Em todos os handlers de webhook (`HandleInvoicePaidAsync`, `HandleSubscriptionUpdatedAsync`, `HandleSubscriptionDeletedAsync`), adicionar guarda explícita:
    ```csharp
    if (string.IsNullOrWhiteSpace(invoice.CustomerId))
    {
        _logger.LogWarning("Evento recebido com CustomerId vazio/nulo.");
        return;
    }
    ```
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`

#### 3.3. Sincronização Completa de Alterações pelo Stripe Portal
* **Problema Identificado:** No evento `customer.subscription.updated`, o backend atualiza apenas status e `StripePriceId`, ignorando trocas de plano feitas pelo cliente no portal do Stripe.
* **Ações no Backend:**
  * No `HandleSubscriptionUpdatedAsync`:
    - Identificar o `Price.Id` ou metadados da assinatura.
    - Atualizar `tenant.SubscribedPlan`, `tenant.Capacity` e `tenant.CurrentPeriodEndUtc`.
    - Respeitar a flag `tenant.IsProtected` antes de qualquer alteração de status destrutiva.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`

#### 3.4. Ativação Condicionada à Confirmação do Pagamento
* **Problema Identificado:** No `checkout.session.completed`, o tenant é ativado imediatamente sem validar se `session.PaymentStatus == "paid"`.
* **Ações no Backend:**
  * No `HandleCheckoutSessionCompletedAsync`:
    - Ativar o tenant (`Status = "Active"`) apenas se `session.PaymentStatus == "paid"`.
    - Se `PaymentStatus == "unpaid"` (ex: boleto ou pix pendente), manter o tenant em status informativo ou aguardar o evento definitivo `invoice.paid`.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`

#### 3.5. Registro de Histórico em `TenantSubscriptionHistories`
* **Problema Identificado:** Eventos do Stripe não gravam histórico em `SubscriptionHistories`, deixando o Backoffice sem visibilidade das ações de faturamento do cliente.
* **Ações no Backend:**
  * Injetar a persistência de `TenantSubscriptionHistory` nos handlers de webhook para eventos de nova subscrição, renovação de fatura, cancelamento e falha de pagamento.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`

---

### Fase 4: Gestão do Ciclo de Vida, Expiração de Trial e Inadimplência [PRIORIDADE 4]

#### 4.1. Bloqueio Ativo de Período de Testes (*Trial*) Expirado
* **Problema Identificado:** O tenant entra em `Trial` por 14 dias (`CurrentPeriodEndUtc = now.AddDays(14)`), mas `TenantLifecycle.CanProducerAccess("Trial")` nunca expira porque a data não é avaliada no middleware de acesso.
* **Ações no Backend:**
  * No `TenantAccessGuard.EnsureProducerAccessAsync`:
    ```csharp
    if (tenant.Status == "Trial" && tenant.CurrentPeriodEndUtc.HasValue && tenant.CurrentPeriodEndUtc.Value < DateTime.UtcNow)
    {
        return Result.Failure(TenancyErrors.TrialExpired);
    }
    ```
* **Ações no Frontend:**
  * Criar tela de bloqueio e redirecionamento para regularização com o Stripe quando o trial expirar.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/TenantAccessGuard.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/Errors/TenancyErrors.cs`
  - `src/Host/CriaCerto.Api/Middleware/TenantAccessMiddleware.cs`

#### 4.2. Worker de Segundo Plano para Expiração e Carência de Inadimplência
* **Problema Identificado:** Não há serviço em background processando o vencimento diário de contas em trial ou contas inadimplentes há muito tempo.
* **Ações no Backend:**
  * Criar `SubscriptionLifecycleWorker : BackgroundService`:
    - Execução periódica (ex: a cada 6 horas).
    - Identificar tenants em `Trial` com `CurrentPeriodEndUtc < UtcNow` e transicionar para `Suspended` (motivo: "Período de testes expirado").
    - Identificar tenants em `PastDue` há mais de 7 dias (período de tolerância/grace period) e transicionar para `Suspended` (motivo: "Inadimplência não regularizada após prazo de tolerância").
    - Respeitar a regra de proteção `tenant.IsProtected == true`.
* **Arquivos Impactados:**
  - Novo serviço em `src/Host/CriaCerto.Api/BackgroundServices/SubscriptionLifecycleWorker.cs`
  - Registro em `Program.cs`.

#### 4.3. Validação Estrita de Dados de Onboarding
* **Problema Identificado:** O endpoint `POST /api/v1/tenancy/farms` aceita qualquer valor para `SubscribedPlan` e `Capacity` enviados no payload.
* **Ações no Backend:**
  * No `CreateTenantCommandHandler`:
    - Padronizar todo novo cadastro para iniciar no plano padrão de trial (`Starter` ou plano de teste configurado na plataforma).
    - Fixar a capacidade correspondente ao plano do trial em vez de aceitar valores arbitrários do cliente (ex: 999.999 cabeças).
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/CreateTenant/CreateTenantCommand.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/CreateTenant/CreateTenantCommandValidator.cs`

---

### Fase 5: Testes Automatizados (TDD), Observabilidade e Homologação [PRIORIDADE 5]

#### 5.1. Testes Unitários de Cobrança e Licenciamento
* Criar suíte de testes em `tests/Modules/Tenancy/`:
  - `CreateCheckoutSessionCommandHandlerTests`: RBAC, mapeamento de planos, trava contra assinatura duplicada.
  - `CreatePortalSessionCommandHandlerTests`: RBAC e geração de URL segura.
  - `StripePaymentServiceTests`: Validação de assinatura, descarte de IDs nulos, parsing de eventos.
  - `TenantAccessGuardTests`: Bloqueio de trial expirado e past_due fora do grace period.

#### 5.2. Testes de Integração com Stripe CLI
* Script para disparar eventos simulados via Stripe CLI:
  ```bash
  stripe trigger checkout.session.completed
  stripe trigger invoice.paid
  stripe trigger invoice.payment_failed
  stripe trigger customer.subscription.deleted
  ```
* Validar que as transições no banco de dados ocorrem de forma atômica e correta.

#### 5.3. Observabilidade e Alertas
* Adicionar logs estruturados com métricas para falhas em pagamentos:
  - Alertas para assinaturas de webhook rejeitadas (potencial ataque ou segredo vencido).
  - Alertas para faturas falhadas no Stripe para notificação da equipe de CS/Suporte.

---

## 4. Checklist Consolidado de Execução

| Item | Descrição da Tarefa | Arquivo Principal | Status |
| :---: | :--- | :--- | :---: |
| **1.1** | Remover endpoint público de troca de plano gratuita | `Program.cs` / `ChangeSubscriptionPlanCommand.cs` | [x] |
| **1.2** | Exigir validação criptográfica obrigatória no Webhook | `StripePaymentService.cs` | [x] |
| **1.3** | Adicionar checagem de papel `Admin` para checkout e portal | `CreateCheckoutSessionCommand.cs` | [x] |
| **1.4** | Sanitizar URLs de retorno contra Open Redirect | `CreateCheckoutSessionCommand.cs` | [x] |
| **2.1** | Unificar identificadores canônicos de plano no Stripe e no banco | `CreateCheckoutSessionCommand.cs` / `ModuleLicenseChecker.cs` | [x] |
| **2.2** | Bloquear checkout para quem já possui assinatura ativa | `CreateCheckoutSessionCommand.cs` | [ ] |
| **2.3** | Implementar renovação de token no retorno do checkout | `SubscriptionManagement.razor` / `Program.cs` | [ ] |
| **3.1** | Criar tabela e validação de idempotência para webhooks | `TenancyDbContext.cs` / `StripePaymentService.cs` | [ ] |
| **3.2** | Adicionar verificação contra IDs de clientes nulos no webhook | `StripePaymentService.cs` | [ ] |
| **3.3** | Sincronizar trocas de plano feitas pelo Stripe Portal | `StripePaymentService.cs` | [ ] |
| **3.4** | Validar `PaymentStatus == "paid"` antes de ativar conta | `StripePaymentService.cs` | [ ] |
| **3.5** | Gravar histórico em `TenantSubscriptionHistories` via webhook | `StripePaymentService.cs` | [ ] |
| **4.1** | Bloquear acesso no `TenantAccessGuard` para trials vencidos | `TenantAccessGuard.cs` | [ ] |
| **4.2** | Criar `SubscriptionLifecycleWorker` para expiração e grace period | `SubscriptionLifecycleWorker.cs` | [ ] |
| **4.3** | Travar plano e capacidade padrão no onboarding | `CreateTenantCommand.cs` | [ ] |
| **5.1** | Implementar testes unitários para fluxo financeiro | `tests/Modules/Tenancy/` | [ ] |
| **5.2** | Homologar com Stripe CLI e documentar rotina de testes | `docs/operations/` | [ ] |

---

## 5. Critérios de Aceite (Definition of Done)

1. **Zero Bypass:** Nenhuma conta pode obter ou alterar plano sem registro válido de pagamento e assinatura no Stripe ou autorização expressa documentada por Admin no Backoffice.
2. **Acesso Imediato sem Falhas:** Ao pagar um plano no Stripe Checkout, o produtor deve ter seus módulos ativados imediatamente, com os claims sincronizados e sem bloqueio no `ModuleLicenseChecker`.
3. **Segurança de Webhook:** Requisições para `/api/v1/payments/stripe-webhook` sem cabeçalho `Stripe-Signature` válido devem retornar `400 BadRequest`.
4. **Fim do Trial Infinito:** Nenhuma conta com mais de 14 dias de cadastro sem plano ativo pode continuar acessando os módulos operacionais.
5. **Cobertura de Testes:** Todos os novos comandos, validações e handlers devem possuir testes automatizados cobrindo os cenários de sucesso e falha.
