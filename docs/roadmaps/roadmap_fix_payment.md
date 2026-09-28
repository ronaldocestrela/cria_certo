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

#### 2.2. Prevenção de Assinaturas Concorrentes (Cobrança Dupla) [CONCLUÍDO]
* **Problema Identificado:** Se uma fazenda já possui `StripeSubscriptionId` ativo e solicita um novo checkout, o Stripe cria uma nova assinatura simultânea para o mesmo cliente, cobrando duplamente.
* **Ações no Backend Implementadas:**
  * Em [TenancyErrors.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/Errors/TenancyErrors.cs), criado o erro de domínio `ActiveSubscriptionExists` com código `Tenant.ActiveSubscriptionExists` e tipo `ErrorType.Conflict` (mapeado para HTTP 409 Conflict).
  * Em [Tenant.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/Tenant.cs), implementado o método de domínio `HasActiveStripeSubscription()`, validando a presença de `StripeSubscriptionId` conjugado com status operacional ativo (`Active` ou `PastDue`).
  * Em [CreateCheckoutSessionCommandHandler.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommand.cs), inserida guarda bloqueando a emissão de Checkout Session com `Result.Failure<CheckoutSessionResult>(TenancyErrors.ActiveSubscriptionExists)` quando o tenant já possui assinatura Stripe ativa.
* **Ações no Frontend Implementadas (Blazor WebAssembly):**
  * Em [TenancyApiClient.cs](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Services/TenancyApiClient.cs), adicionado parsing de resposta de erro HTTP 409 (`ApiErrorDto`) propagando mensagem orientadora da API em vez de silenciar a falha.
  * Em [SubscriptionManagement.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor), adicionada a propriedade `HasActiveStripeSubscription`. Nos cards de plano, os botões de ação passam a indicar alternância via Portal Stripe (`Alterar para {Plan} (Portal Stripe)`). Ao clicar em alteração de plano, o produtor é conduzido diretamente ao Customer Portal seguro do Stripe (`OpenBillingPortal()`), prevenindo duplicação de assinaturas e garantindo cálculo pro-rata.
  * Adicionado botão de atalho `Acessar Portal Stripe` no modal de faturamento em caso de interceptação de tentativa concorrente.
* **Testes Automatizados (TDD):**
  - [SubscriptionBillingAuthorizationTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionBillingAuthorizationTests.cs):
    * `CreateCheckoutSession_Should_Fail_With_Conflict_When_Tenant_Already_Has_Active_StripeSubscription` (validação de rejeição 409 Conflict para tenant ativo com `StripeSubscriptionId`).
    * `CreateCheckoutSession_Should_Fail_With_Conflict_When_Tenant_Has_PastDue_StripeSubscription` (validação de rejeição 409 Conflict para tenant em tolerância `PastDue`).
    * `CreateCheckoutSession_Should_Succeed_When_Tenant_Has_Cancelled_StripeSubscription` (validação de permissão de nova assinatura caso a anterior tenha sido cancelada).
    * `CreateCheckoutSession_Should_Succeed_When_Tenant_Has_No_StripeSubscription` (validação de fluxo normal de primeiro checkout).
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/Errors/TenancyErrors.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/Tenant.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/SubscriptionCheckout/CreateCheckoutSessionCommand.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Services/TenancyApiClient.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionBillingAuthorizationTests.cs`

#### 2.3. Renovação de Claims e JWT no Retorno do Stripe [CONCLUÍDO]
* **Problema Identificado:** Ao concluir a assinatura no Stripe e ser redirecionado para `/settings/subscription?success=true`, o usuário continuava com o token JWT antigo gravado no navegador, mantendo as claims de plano desatualizadas até efetuar logout.
* **Ações no Backend Implementadas:**
  * Criado o comando e handler [RefreshTokenCommand.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/RefreshToken/RefreshTokenCommand.cs) no módulo `Modules.Tenancy`, validando existência do usuário, vínculo com a fazenda (`Auth.UnauthorizedTenant`) e acessibilidade operacional do tenant (`TenantLifecycle.CanProducerAccess`), gerando novo JWT via `IJwtService.GenerateToken` com claims do plano recém-contratado.
  * Em [Program.cs](file:///home/rony/LPR/CriaCerto/src/Host/CriaCerto.Api/Program.cs), expostos os endpoints autenticados `POST /api/v1/auth/refresh-token` e `POST /api/auth/refresh-token` protegidos por `.RequireAuthorization()`, extraindo `UserId` e `TenantId` das credenciais ativas.
* **Ações no Frontend Implementadas (Blazor WebAssembly):**
  * Em [TenancyApiClient.cs](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Services/TenancyApiClient.cs), adicionado o método `RefreshTokenAsync(Guid? tenantId)`.
  * Em [SubscriptionManagement.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor), adicionada captura de query parameters `success` e `canceled` via `[SupplyParameterFromQuery]`. Ao detectar `success=true`, invoca `RefreshTokenAsync`, aciona `CustomAuthStateProvider.MarkUserAsAuthenticated(newToken)`, recarrega o perfil do tenant e exibe feedback imediato ao usuário. Também atualizada a URL de retorno do Stripe Customer Portal para propagar `success=true`.
* **Testes Automatizados (TDD):**
  * [RefreshTokenCommandHandlerTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/RefreshTokenCommandHandlerTests.cs): cobertura de renovação de token bem-sucedida com plano atualizado, rejeição para usuário inexistente, rejeição para usuário fora do tenant e bloqueio para tenant com status restrito (`Suspended`, `Cancelled`, `Archived`).
  * [CustomAuthStateProviderTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Web.Client.UnitTests/Auth/CustomAuthStateProviderTests.cs): validação de extração de claims de plano e tenant renovados a partir de JWT.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/RefreshToken/RefreshTokenCommand.cs`
  - `src/Host/CriaCerto.Api/Program.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Services/TenancyApiClient.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/RefreshTokenCommandHandlerTests.cs`
  - `tests/Unit/CriaCerto.Web.Client.UnitTests/Auth/CustomAuthStateProviderTests.cs`
  - `docs/modules/tenancy.md`
  - `docs/roadmaps/roadmap_fix_payment.md`

---

### Fase 3: Idempotência de Webhook, Sincronização do Portal e Auditoria [PRIORIDADE 3]

#### 3.1. Tabela de Idempotência para Eventos Stripe [CONCLUÍDO]
* **Problema Identificado:** O Stripe reenvia webhooks em retentativas automáticas. Não havia deduplicação de eventos pelo `stripeEvent.Id`, gerando risco de reprocessamento redundante ou inconsistências sob concorrência.
* **Ações no Backend Implementadas:**
  * Criada a entidade de domínio [StripeWebhookEvent.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/StripeWebhookEvent.cs) com `Id`, `EventId` (chave de negócio única com índice exclusivo), `EventType`, `ProcessedAtUtc` e `PayloadJson`.
  * Atualizada a interface [ITenancyDbContext.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Abstractions/ITenancyDbContext.cs) e o contexto [TenancyDbContext.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Persistence/TenancyDbContext.cs), mapeando a tabela `StripeWebhookEvents` no schema `tenancy` com índice único em `EventId`.
  * Criada a migration [20260928124500_AddStripeWebhookEventsTable.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Persistence/Migrations/20260928124500_AddStripeWebhookEventsTable.cs) e atualizado o snapshot [TenancyDbContextModelSnapshot.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Persistence/Migrations/TenancyDbContextModelSnapshot.cs).
  * Em [StripePaymentService.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs), no método `ProcessWebhookAsync`:
    - Adicionada verificação prévia de `EventId` existente na tabela `StripeWebhookEvents`; se já existir, retorna imediatamente `Success = true` com mensagem informativa de idempotência sem reexecutar os handlers.
    - Após o processamento dos handlers de negócio, persiste o registro do evento e trata graciosamente possíveis colisões concorrentes de inserção (`DbUpdateException`).
* **Testes Automatizados (TDD):**
  * Criada a suíte [StripeWebhookIdempotencyTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookIdempotencyTests.cs) cobrindo:
    - Processamento e persistência na primeira recepção do evento.
    - Deduplicação e retorno de sucesso imediato sem reexecutar regras de negócio em chamadas repetidas.
    - Ignorar eventos pré-existentes na base.
    - Prevenção de duplicidade por restrição de unicidade no banco de dados.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/StripeWebhookEvent.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Abstractions/ITenancyDbContext.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Persistence/TenancyDbContext.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Persistence/Migrations/20260928124500_AddStripeWebhookEventsTable.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Persistence/Migrations/20260928124500_AddStripeWebhookEventsTable.Designer.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Persistence/Migrations/TenancyDbContextModelSnapshot.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookIdempotencyTests.cs`
  - `docs/modules/tenancy.md`
  - `docs/roadmaps/roadmap_fix_payment.md`

#### 3.2. Sanitização de Chaves Nulas e Proteção de Tenants [CONCLUÍDO]
* **Problema Identificado:** Consultas com `t.StripeCustomerId == invoice.CustomerId` ou `t.StripeCustomerId == subscription.CustomerId` sem validar string nula/vazia faziam o EF Core gerar `IS NULL` em SQL, associando faturas, inadimplências ou cancelamentos indevidos ao primeiro tenant com `StripeCustomerId == null`. Além disso, eventos de cancelamento ou suspensão ignoravam a flag `tenant.IsProtected`, desativando contas protegidas.
* **Ações no Backend Implementadas:**
  * Em [StripePaymentService.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs):
    - **Guardas prévias de chaves nulas:** Em `HandleInvoicePaidAsync` e `HandleInvoicePaymentFailedAsync`, adicionada validação antecipada com `string.IsNullOrWhiteSpace(invoice.CustomerId)`, abortando com log de advertência antes de consultar o banco.
    - **Validação de identificadores em assinaturas:** Em `HandleSubscriptionUpdatedAsync` e `HandleSubscriptionDeletedAsync`, implementada validação de `subscription.Id` e `subscription.CustomerId`, assegurando que a consulta ao banco só filtre por identificadores válidos (`hasSubId` e `hasCustId`), evitando qualquer correspondência acidental com campos nulos.
    - **Sanitização de checkout:** Em `HandleCheckoutSessionCompletedAsync`, assegurada a atribuição condicional de `session.CustomerId` e `session.SubscriptionId` apenas quando não vazios.
    - **Proteção de Tenants (`tenant.IsProtected`):** Em `HandleSubscriptionUpdatedAsync` (para status `canceled`/`unpaid`) e em `HandleSubscriptionDeletedAsync`, inserida salvaguarda verificando `tenant.IsProtected`. Se verdadeiro, impede a transição destrutiva para `Suspended` ou `Cancelled`, registrando log de advertência e preservando o status e direitos de acesso da organização protegida.
* **Testes Automatizados (TDD):**
  * Criada a suíte [StripeWebhookNullKeyAndTenantProtectionTests.cs](file:///home/rony/LPR/CriaCerto/tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookNullKeyAndTenantProtectionTests.cs) cobrindo:
    - Webhooks `invoice.paid` e `invoice.payment_failed` com `CustomerId` nulo, vazio ou espaços em branco não afetam tenants sem Stripe.
    - Webhooks `customer.subscription.updated` e `customer.subscription.deleted` com chaves nulas não alteram status de tenants não vinculados.
    - Webhook `customer.subscription.deleted` em tenant com `IsProtected = true` tem cancelamento bloqueado e status preservado.
    - Webhook `customer.subscription.updated` com status `canceled`/`unpaid` em tenant com `IsProtected = true` tem suspensão bloqueada.
    - Webhook `customer.subscription.deleted` em tenant normal (`IsProtected = false`) continua executando cancelamento com sucesso.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookNullKeyAndTenantProtectionTests.cs`
  - `docs/modules/tenancy.md`
  - `docs/roadmaps/roadmap_fix_payment.md`

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
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookPaymentStatusTests.cs`
  - `docs/modules/tenancy.md`
  - `docs/roadmaps/roadmap_fix_payment.md`

#### 3.5. Registro de Histórico em `TenantSubscriptionHistories`
* **Problema Identificado:** Eventos do Stripe não gravavam histórico em `SubscriptionHistories`, deixando o Backoffice sem visibilidade das ações de faturamento do cliente.
* **Ações no Backend Implementadas:**
  * No enum `SubscriptionActionType`: adicionados membros `NewSubscription`, `Renewal`, `PaymentFailed`, `Cancelled`, `PlanChanged` e `Suspended`.
  * Em `TenantSubscriptionHistory`: adicionado método de fábrica `CreateFromStripeWebhook` com atribuição do ator canônico de sistema `StripeSystemActorId = Guid.Empty`.
  * Em `StripePaymentService.cs`:
    - `HandleCheckoutSessionCompletedAsync`: persiste `SubscriptionActionType.NewSubscription` com snapshot de capacidade e justificativa descritiva ao confirmar pagamento do Checkout.
    - `HandleInvoicePaidAsync`: persiste `SubscriptionActionType.Renewal` registrando a extensão do período garantido.
    - `HandleInvoicePaymentFailedAsync`: persiste `SubscriptionActionType.PaymentFailed` documentando a entrada em `PastDue`.
    - `HandleSubscriptionUpdatedAsync`: persiste `SubscriptionActionType.PlanChanged` em caso de upgrade/downgrade de plano via portal, `PaymentFailed` para status `past_due`, ou `Suspended` (com registro explícito caso seja impedido por `tenant.IsProtected`).
    - `HandleSubscriptionDeletedAsync`: persiste `SubscriptionActionType.Cancelled` em caso de cancelamento da assinatura no Stripe, ou registro de interceptação caso o tenant esteja protegido (`IsProtected = true`).
* **Testes Automatizados (TDD):**
  * Criada a suíte `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookSubscriptionHistoryTests.cs` cobrindo:
    - Gravação de `NewSubscription` no Checkout com pagamento confirmado.
    - Gravação de `Renewal` no pagamento de fatura.
    - Gravação de `PaymentFailed` na falha de cobrança.
    - Gravação de `PlanChanged` com atualização de snapshot de capacidade na troca de plano.
    - Gravação de `Cancelled` em cancelamento regular de assinatura.
    - Gravação de bloqueio por proteção de tenant em cancelamento de conta protegida.
    - Garantia de não duplicação de histórico em reenvio de webhook (idempotência).
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/TenantSubscription.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/TenantSubscriptionHistory.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/StripePaymentService.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/StripeWebhookSubscriptionHistoryTests.cs`
  - `docs/modules/tenancy.md`
  - `docs/roadmaps/roadmap_fix_payment.md`

---

### Fase 4: Gestão do Ciclo de Vida, Expiração de Trial e Inadimplência [PRIORIDADE 4]

#### 4.1. Bloqueio Ativo de Período de Testes (*Trial*) Expirado [CONCLUÍDO]
* **Problema Identificado:** O tenant entra em `Trial` por 14 dias (`CurrentPeriodEndUtc = now.AddDays(14)`), mas `TenantLifecycle.CanProducerAccess("Trial")` nunca expirava porque a data não era avaliada no middleware de acesso.
* **Ações no Backend Implementadas:**
  * Adicionado erro canônico `TenancyErrors.TrialExpired` com `ErrorType.Unauthorized` em `TenancyErrors.cs`.
  * No `TenantAccessGuard.EnsureProducerAccessAsync`: checagem de `tenant.Status == "Trial" && tenant.CurrentPeriodEndUtc.HasValue && tenant.CurrentPeriodEndUtc.Value < DateTime.UtcNow`, retornando `Result.Failure(TenancyErrors.TrialExpired)`.
  * No `TenantAccessMiddleware.cs`: bypass mantido e expandido para rotas de regularização de billing e perfil (`/api/v1/tenancy/profile`, `/api/v1/tenancy/subscription`, `/api/v1/payments`, `/api/v1/auth`), evitando deadlock operacional para o produtor regularizar a fazenda.
* **Ações no Frontend Implementadas:**
  * Componente `TrialExpiredLockout.razor` estilizado com identidade visual rica, bento cards e CTAs diretos para o checkout do Stripe e suporte.
  * Página dedicada `/trial-expired` (`TrialExpired.razor`).
  * Banner de destaque contextual com alerta de regularização pendente no topo de `SubscriptionManagement.razor`.
* **Testes Automatizados (TDD):**
  * `TenantAccessGuardTests.cs`: validação do ciclo Red/Green cobrindo Active, Trial ativo, Trial expirado, inexistente e suspenso/cancelado.
  * `BillingLifecycleIntegrationTests.cs`: validação do bypass de rotas de faturamento no middleware e bloqueio HTTP 403 Forbidden para rotas operacionais quando o trial expira.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Domain/Errors/TenancyErrors.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/TenantAccessGuard.cs`
  - `src/Host/CriaCerto.Api/Middleware/TenantAccessMiddleware.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Components/TrialExpiredLockout.razor`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/TrialExpired.razor`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/TenantAccessGuardTests.cs`
  - `tests/Integration/CriaCerto.Architecture.IntegrationTests/BillingLifecycleIntegrationTests.cs`

#### 4.2. Worker de Segundo Plano para Expiração e Carência de Inadimplência [CONCLUÍDO]
* **Problema Identificado:** Não havia serviço em background processando o vencimento diário de contas em trial ou contas inadimplentes há muito tempo.
* **Ações no Backend Implementadas:**
  * Criado contrato `ISubscriptionLifecycleService` e record `SubscriptionLifecycleExecutionResult` em `CriaCerto.Modules.Tenancy.Application/Abstractions/ISubscriptionLifecycleService.cs`.
  * Criada classe de opções configuráveis `SubscriptionLifecycleOptions` em `CriaCerto.Modules.Tenancy.Application/Options/SubscriptionLifecycleOptions.cs` (`IntervalHours = 6`, `PastDueGracePeriodDays = 7`, `BatchSize = 100`).
  * Implementado serviço de aplicação/domínio `SubscriptionLifecycleService` em `CriaCerto.Modules.Tenancy.Infrastructure/Services/SubscriptionLifecycleService.cs`:
    - Varredura em lote de tenants com `Status == "Trial"` e `CurrentPeriodEndUtc < UtcNow`.
    - Varredura em lote de tenants com `Status == "PastDue"` e `(StatusChangedAtUtc ?? UpdatedAtUtc) <= UtcNow.AddDays(-PastDueGracePeriodDays)`.
    - Respeito estrito à proteção: se `tenant.IsProtected == true`, a suspensão é contida e gravado registro em `TenantSubscriptionHistories` com `SubscriptionActionType.Suspended` e justificativa de preservação.
    - Se não protegido: transiciona status via `tenant.Suspend(reason)` com justificativas canônicas válidas (`"Período de testes expirado."` e `"Inadimplência não regularizada após prazo de tolerância."`) e grava histórico em `TenantSubscriptionHistories`.
  * Criado o serviço hospedado `SubscriptionLifecycleWorker : BackgroundService` em `src/Host/CriaCerto.Api/BackgroundServices/SubscriptionLifecycleWorker.cs` gerenciando ciclo periódico resiliente via `PeriodicTimer`, criação de escopos e tratamento de cancelamento.
  * Registrado `SubscriptionLifecycleOptions` e `ISubscriptionLifecycleService` em `DependencyInjection.cs` e `builder.Services.AddHostedService<SubscriptionLifecycleWorker>()` em `Program.cs`.
* **Testes Automatizados (TDD):**
  * `SubscriptionLifecycleServiceTests.cs`: Suíte de testes unitários cobrindo suspensão de trial vencido, manutenção de trial futuro/nulo, suspensão de inadimplência fora do prazo de tolerância, manutenção dentro do grace period, salvaguarda de tenant protegido, ignorar status ativos/já suspensos e idempotência.
  * `SubscriptionLifecycleWorkerIntegrationTests.cs`: Teste de integração de ponta a ponta validando resolução por injeção de dependência via escopo e execução segura no worker.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Abstractions/ISubscriptionLifecycleService.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Options/SubscriptionLifecycleOptions.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/Services/SubscriptionLifecycleService.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Infrastructure/DependencyInjection.cs`
  - `src/Host/CriaCerto.Api/BackgroundServices/SubscriptionLifecycleWorker.cs`
  - `src/Host/CriaCerto.Api/Program.cs`
  - `src/Host/CriaCerto.Api/appsettings.json`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/SubscriptionLifecycleServiceTests.cs`
  - `tests/Integration/CriaCerto.Architecture.IntegrationTests/SubscriptionLifecycleWorkerIntegrationTests.cs`

#### 4.3. Validação Estrita de Dados de Onboarding
* **Problema Identificado:** O endpoint `POST /api/v1/tenancy/farms` aceitava qualquer valor para `SubscribedPlan` e `Capacity` enviados no payload.
* **Ações no Backend:**
  * No `CreateTenantCommand` e `CreateTenantCommandHandler`:
    - Definidas as constantes canônicas `DefaultTrialPlan = "Starter"` e `DefaultTrialCapacity = PlanCapacityLimits.StarterLimit` (500).
    - Parâmetros `SubscribedPlan` e `Capacity` padronizados com os defaults de trial seguro.
    - O handler força a criação em `SubscribedPlan = DefaultTrialPlan` e limita a capacidade estritamente ao teto do plano Starter (`request.Capacity is > 0 and <= 500 ? request.Capacity : 500`).
    - Aplicação da segmentação padrão via `tenant.ApplyDefaultSegmentation()`.
  * No `CreateTenantCommandValidator`:
    - Validação restrita permitindo unicamente o plano padrão `Starter` no onboarding gratuito.
    - Validação de capacidade limitando a faixa permitida de 1 a 500 cabeças (`PlanCapacityLimits.StarterLimit`), rejeitando payloads com valores arbitrários (> 500 ou <= 0).
* **Ações no Frontend:**
  * Em `OnboardingWizard.razor`, ajustado o campo de capacidade e a seleção de planos para orientar o limite de até 500 cabeças no teste gratuito e enviar payload padronizado com `Starter` e capacidade limitada a 500.
* **Testes Automatizados (TDD):**
  * `CreateTenantCommandValidatorTests.cs`: Testes cobrindo validação com sucesso (500 cabeças e Starter) e falha estrita para planos comerciais (`Pro`, `Enterprise`, planos inválidos) e capacidades além do teto (> 500 ou <= 0).
  * `CreateTenantCommandHandlerTests.cs`: Testes cobrindo criação com plano e capacidade padrão de trial, defesa em profundidade neutralizando payloads forjados e respeito a capacidades válidas customizadas inferiores a 500.
  * `OnboardingIntegrationTests.cs`: Fluxo de onboarding completo de ponta a ponta validado com sucesso.
* **Arquivos Impactados:**
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/CreateTenant/CreateTenantCommand.cs`
  - `src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features/CreateTenant/CreateTenantCommandValidator.cs`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/OnboardingWizard.razor`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/CreateTenantCommandValidatorTests.cs`
  - `tests/Unit/CriaCerto.Modules.Tenancy.UnitTests/CreateTenantCommandHandlerTests.cs`
  - `tests/Integration/CriaCerto.Architecture.IntegrationTests/OnboardingIntegrationTests.cs`
  - `docs/modules/tenancy.md`
  - `docs/roadmaps/roadmap_fix_payment.md`

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
* **Status**: Concluído [x].
* **Implementação**:
  - Suíte de testes de integração automatizados em `tests/Integration/CriaCerto.Architecture.IntegrationTests/StripeWebhookIntegrationTests.cs`.
  - Script automatizado de escuta e disparo de gatilhos em `scripts/stripe-test-webhooks.sh`.
  - Playbook operacional documentado em `docs/operations/homologacao_stripe_cli.md`.

#### 5.3. Observabilidade e Alertas
* Adicionar logs estruturados com métricas para falhas em pagamentos:
  - Alertas para assinaturas de webhook rejeitadas (potencial ataque ou segredo vencido).
  - Alertas para faturas falhadas no Stripe para notificação da equipe de CS/Suporte.
* **Status**: Concluído [x].
* **Implementação**:
  - Telemetria nativa .NET 10 via `Meter("CriaCerto.Modules.Tenancy.Payments")` em `PaymentTelemetry.cs` com contadores de eventos, falhas de assinatura, falhas de faturas e histograma de latência.
  - Eventos de integração in-process `WebhookSignatureFailedIntegrationEvent` e `PaymentInvoiceFailedIntegrationEvent` desacoplando Tenancy e Backoffice.
  - Alertas operacionais registrados na Central de Incidentes do Backoffice (`ALR_WEBHOOK_SIGNATURE_INVALID` e `ALR_PAYMENT_INVOICE_FAILED`) via `PaymentAlertEventHandler.cs`.
  - Suporte a testes e simulações em `SimulateAlertModal.razor`.
  - Runbook de resposta a incidentes `RUNBOOK-06` em `docs/operations/runbook_incident_response.md`.
  - Playbook de CS/Suporte N1/N2 em `docs/operations/playbook_support.md`.
  - Suíte de testes unitários e de integração em `PaymentTelemetryTests.cs`, `PaymentAlertEventHandlerTests.cs` e `PaymentObservabilityIntegrationTests.cs`.

---

## 4. Checklist Consolidado de Execução

| Item | Descrição da Tarefa | Arquivo Principal | Status |
| :---: | :--- | :--- | :---: |
| **1.1** | Remover endpoint público de troca de plano gratuita | `Program.cs` / `ChangeSubscriptionPlanCommand.cs` | [x] |
| **1.2** | Exigir validação criptográfica obrigatória no Webhook | `StripePaymentService.cs` | [x] |
| **1.3** | Adicionar checagem de papel `Admin` para checkout e portal | `CreateCheckoutSessionCommand.cs` | [x] |
| **1.4** | Sanitizar URLs de retorno contra Open Redirect | `CreateCheckoutSessionCommand.cs` | [x] |
| **2.1** | Unificar identificadores canônicos de plano no Stripe e no banco | `CreateCheckoutSessionCommand.cs` / `ModuleLicenseChecker.cs` | [x] |
| **2.2** | Bloquear checkout para quem já possui assinatura ativa | `CreateCheckoutSessionCommand.cs` | [x] |
| **2.3** | Implementar renovação de token no retorno do checkout | `SubscriptionManagement.razor` / `Program.cs` | [x] |
| **3.1** | Criar tabela e validação de idempotência para webhooks | `TenancyDbContext.cs` | [x] |
| **3.2** | Adicionar verificação contra IDs de clientes nulos no webhook | `StripePaymentService.cs` | [x] |
| **3.3** | Sincronizar trocas de plano feitas pelo Stripe Portal | `StripePaymentService.cs` | [x] |
| **3.4** | Validar `PaymentStatus == "paid"` antes de ativar conta | `StripePaymentService.cs` | [x] |
| **3.5** | Gravar histórico em `TenantSubscriptionHistories` via webhook | `StripePaymentService.cs` | [x] |
| **4.1** | Bloquear acesso no `TenantAccessGuard` para trials vencidos | `TenantAccessGuard.cs` | [x] |
| **4.2** | Criar `SubscriptionLifecycleWorker` para expiração e grace period | `SubscriptionLifecycleWorker.cs` | [x] |
| **4.3** | Travar plano e capacidade padrão no onboarding | `CreateTenantCommand.cs` | [x] |
| **5.1** | Implementar testes unitários para fluxo financeiro | `tests/Modules/Tenancy/` | [ ] |
| **5.2** | Homologar com Stripe CLI e documentar rotina de testes | `docs/operations/` | [x] |
| **5.3** | Implementar observabilidade, métricas e central de alertas | `PaymentTelemetry.cs` / `PaymentAlertEventHandler.cs` | [x] |

---

## 5. Critérios de Aceite (Definition of Done)

1. **Zero Bypass:** Nenhuma conta pode obter ou alterar plano sem registro válido de pagamento e assinatura no Stripe ou autorização expressa documentada por Admin no Backoffice.
2. **Acesso Imediato sem Falhas:** Ao pagar um plano no Stripe Checkout, o produtor deve ter seus módulos ativados imediatamente, com os claims sincronizados e sem bloqueio no `ModuleLicenseChecker`.
3. **Segurança de Webhook:** Requisições para `/api/v1/payments/stripe-webhook` sem cabeçalho `Stripe-Signature` válido devem retornar `400 BadRequest`.
4. **Fim do Trial Infinito:** Nenhuma conta com mais de 14 dias de cadastro sem plano ativo pode continuar acessando os módulos operacionais.
5. **Cobertura de Testes:** Todos os novos comandos, validações e handlers devem possuir testes automatizados cobrindo os cenários de sucesso e falha.
