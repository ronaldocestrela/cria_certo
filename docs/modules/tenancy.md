# Módulo de Tenancy & Autenticação (`Modules.Tenancy`)

## Visão Geral
O módulo `Modules.Tenancy` gerencia as identidades dos usuários, organizações/fazendas (Tenants), mapeamento multi-tenant (`UserTenants`), fluxo de autenticação e licenças de acesso.

---

## 1. Entidades de Domínio

- **User**: Representa a identidade global do usuário no sistema.
  - `Id`: `Guid`
  - `FullName`: `string` (obrigatório, max 150)
  - `Email`: `string` (obrigatório, único, max 150)
  - `PasswordHash`: `string` (hash PBKDF2 com SHA256)
  - `PhoneNumber`: `string?` (opcional, max 30)
  - `PasswordResetToken`: `string?` (token para recuperação de senha)
  - `PasswordResetTokenExpiresAt`: `DateTime?` (data/hora de expiração do token)
  - `UserTenants`: `List<UserTenant>` (relacionamento N:N com Tenants)

- **Tenant**: Representa a unidade produtiva/fazenda.
  - `Id`: `Guid`
  - `Name`: `string` (nome fantasia/fazenda)
  - `LegalName`: `string?` (razão social)
  - `CNPJ`: `string` (formatado, exibido na UI)
  - `CnpjNormalized`: `string` (11 ou 14 dígitos, **único**)
  - `ExternalIdentifier`: `string?` (**único** quando informado)
  - `State`, `City`, `StateRegistration`, `AreaInHectares`, `Type`
  - `Status`: `string` (`Trial`, `Active`, `PastDue`, `Suspended`, `Cancelled`, `Archived`)
  - `IsProtected`: `bool` — impede suspensão/cancelamento/arquivamento
  - `StatusReason`: `string?` — última justificativa de transição
  - `StatusChangedAtUtc`: `DateTime?`
  - `SubscribedPlan`: `string` (Starter, Pro, Enterprise — mantido para compatibilidade durante transição)
  - `HeadTierCode`: `string` (`TIER_500`, `TIER_1500`, `TIER_3000`, `TIER_5000_PLUS`, padrão `TIER_500` no Trial)
  - `Capacity`: `int` (limite de cabeças sincronizado com o `MaxHeads` da faixa contratada via `PlanCapacityLimits`)
  - `SubscribedModules`: `List<TenantSubscribedModule>` — módulos contratados/ativos na assinatura self-service (módulo base `"Cows"` sempre incluído)
  - `TechnicalOwnerName` / `TechnicalOwnerEmail`
  - `CommercialOwnerName` / `CommercialOwnerEmail`
  - `SizeSegment`: `Micro`, `Small`, `Medium`, `Large` (default por `Capacity`)
  - `CommercialRegion`: `Norte`, `Nordeste`, `CentroOeste`, `Sudeste`, `Sul` (default por UF)
  - `ProductiveProfile`: `Corte`, `Leite`, `Misto`, `Cria`, `Recria`, `Engorda`, `Confinamento`
  - `ChurnRisk`: `None`, `Low`, `Medium`, `High`, `Critical`
  - `CreatedAtUtc` / `UpdatedAtUtc`

- **TenantSubscribedModule** (`tenancy.TenantSubscribedModules`): Representa cada módulo ativo na assinatura do tenant.
  - `Id`: `Guid`
  - `TenantId`: `Guid` (FK para `Tenants`, índice único composto `(TenantId, ModuleKey)`)
  - `ModuleKey`: `string` (`Cows`, `Breeding`, `Calving`, `Growth`, `Sanitary`, `Nutrition`, `Analytics`)
  - `StripeSubscriptionItemId`: `string?` (ID do `SubscriptionItem` correspondente no Stripe)
  - `ActivatedAtUtc`: `DateTime`
  - `MonthlyPriceSnapshot`: `decimal(18,2)` (valor mensal vigente no momento da ativação)

- **OperationalTag**: Catálogo de etiquetas operacionais (`Slug` único, `Category`: `Support` | `CustomerSuccess` | `Retention`).
- **TenantOperationalTag**: Vínculo N:N entre `Tenant` e `OperationalTag`.

- **UserTenant**: Tabela associativa entre `User` e `Tenant`.

- **StripeWebhookEvent**: Registro de idempotência de eventos Stripe recebidos (`EventId` único, `EventType`, `ProcessedAtUtc`, `PayloadJson`) para evitar reprocessamento em retentativas automáticas e concorrência.

### Diretrizes de Segurança e Isolamento em Webhooks Stripe
- **Sanitização de Chaves Nulas:** Todas as consultas e mutações disparadas por webhooks (`invoice.paid`, `invoice.payment_failed`, `customer.subscription.updated`, `customer.subscription.deleted`) realizam verificação defensiva prévia de `CustomerId` e `SubscriptionId`. Consultas ao banco nunca são executadas com valores nulos ou em branco, impedindo a contaminação acidental de tenants recém-criados (`StripeCustomerId == null`).
- **Blindagem de Tenants Protegidos (`IsProtected`):** Organizações sinalizadas com `tenant.IsProtected == true` (ex.: contas governamentais, VIPs ou em regime especial) têm suas transições destrutivas (`Suspended`, `Cancelled`, `Archived`) bloqueadas perante eventos de cancelamento ou falha de pagamento do Stripe, registrando logs de aviso para auditoria sem interrupção de acesso e persistindo no histórico a tentativa de desativação interceptada.
- **Ativação Condicionada à Confirmação do Pagamento (`PaymentStatus == "paid"`):** No webhook `checkout.session.completed`, a transição para `Status = "Active"`, remoção de `StatusReason` e a atualização do plano (`SubscribedPlan`) e capacidade (`Capacity`) ocorrem exclusivamente quando `session.PaymentStatus` for `"paid"` (ou `"no_payment_required"`). Para pagamentos assíncronos ou pendentes (`PaymentStatus == "unpaid"`, como Boleto Bancário ou Pix em compensação), o sistema vincula os identificadores do cliente (`StripeCustomerId`) e assinatura (`StripeSubscriptionId`) para correlação futura, preserva o status atual do tenant (ex.: `Trial`) sem liberar módulos antecipadamente e grava justificativa informativa em `StatusReason`. A ativação definitiva é delegada para a liquidação da fatura via webhook `invoice.paid`.
- **Rastreabilidade e Histórico de Assinaturas (`TenantSubscriptionHistories`):** Cada evento de faturamento e ciclo de vida originado no Stripe grava atomicamente uma entrada em `SubscriptionHistories` via `TenantSubscriptionHistory.CreateFromStripeWebhook` com o identificador de sistema `StripeSystemActorId = Guid.Empty`. Eventos mapeados:
  * `checkout.session.completed` (com pagamento confirmado): `SubscriptionActionType.NewSubscription`.
  * `invoice.paid`: `SubscriptionActionType.Renewal` (registrando a extensão do período garantido).
  * `invoice.payment_failed`: `SubscriptionActionType.PaymentFailed` (documentando a entrada em `PastDue`).
  * `customer.subscription.updated`: `SubscriptionActionType.PlanChanged` (atualização de plano/capacidade), `PaymentFailed` (`past_due`) ou `Suspended`.
  * `customer.subscription.deleted`: `SubscriptionActionType.Cancelled` (ou registro de bloqueio por proteção de conta).

### Homologação e Testes de Integração com Stripe CLI
- **Suíte de Testes Automatizados:** Implementada em `tests/Integration/CriaCerto.Architecture.IntegrationTests/StripeWebhookIntegrationTests.cs`, cobrindo o ciclo de vida completo (`checkout.session.completed`, `invoice.paid`, `invoice.payment_failed`, `customer.subscription.deleted`), salvaguarda de inquilinos protegidos (`IsProtected`), idempotência contra eventos duplicados e validação criptográfica HMAC-SHA256 (`Stripe.InvalidSignature`).
- **Script Operacional:** Disponibilizado em `scripts/stripe-test-webhooks.sh` para escuta local (`listen`) e emissão em lote dos 4 eventos de faturamento (`trigger-all`).
- **Procedimento Operacional Padrão (SOP):** Documentado integralmente em `docs/operations/homologacao_stripe_cli.md`.

### Observabilidade, Métricas e Alertas de Pagamento
- **Métricas Nativas (.NET 10 Telemetry):** Instrumentadas via `PaymentTelemetry` (`Meter: CriaCerto.Modules.Tenancy.Payments`):
  * `payments.webhook.events.total`: Contagem total de eventos de webhook processados, classificados por tipo de evento e status (`success`, `failed`, `duplicate`).
  * `payments.webhook.signature_failures.total`: Contador de falhas de autenticação criptográfica por motivo (`missing_header`, `secret_unconfigured`, `invalid_signature`).
  * `payments.invoice.failures.total`: Monitoramento de falhas de débito de faturas categorizado por moeda (`BRL`), motivo e valor.
  * `payments.webhook.duration_ms`: Histograma de latência de processamento em milissegundos.
- **Eventos de Integração In-Process:**
  * `WebhookSignatureFailedIntegrationEvent`: Disparado perante falhas criptográficas na validação do cabeçalho `Stripe-Signature` para alertar operações sobre possíveis ataques de replay/spoofing ou segredos desatualizados.
  * `PaymentInvoiceFailedIntegrationEvent`: Emitido ao receber `invoice.payment_failed` com contexto do produtor (`TenantId`, `TenantName`), fatura (`InvoiceId`), valor e motivo da recusa.
- **Integração com a Central de Incidentes do Backoffice:** O handler `PaymentAlertEventHandler` intercepta os eventos in-process e gera alertas operacionais rastreáveis em `/backoffice/observability`:
  * `ALR_WEBHOOK_SIGNATURE_INVALID` (Severidade Crítica): Alerta com agregação horária para investigação de incidentes de segurança ou rotação de segredo.
  * `ALR_PAYMENT_INVOICE_FAILED` (Severidade Atenção): Alerta vinculado ao tenant com deduplicação por fatura para ação proativa da equipe de CS/Suporte antes do vencimento do período de carência.

---

## 2. Endpoints da API (`/api/auth`)

| Método | Endpoint | Descrição | Requer Auth | Status de Sucesso |
|---|---|---|---|---|
| `POST` | `/api/auth/login` | Autenticação por e-mail e senha | Não | `200 OK` (`AuthResponse`) |
| `POST` | `/api/auth/select-tenant` | Seleção de tenant para contas com múltiplas fazendas | Não | `200 OK` (`AuthResponse`) |
| `POST` | `/api/auth/register` | Auto-cadastro de novo usuário (Sign-Up) | Não | `201 Created` (`UserDto`) |
| `POST` | `/api/auth/forgot-password` | Solicitação de código/token para redefinição de senha | Não | `200 OK` (token) |
| `POST` | `/api/auth/reset-password` | Redefinição de senha com token de verificação | Não | `200 OK` |
| `POST` | `/api/v1/auth/refresh-token` | Renovação de claims e token JWT para o tenant ativo (`head_tier`, `head_capacity`, `subscribed_module`, `subscribed_modules`) | Sim | `200 OK` (`RefreshTokenResult`) |
| `POST` | `/api/v1/tenancy/farms` | Onboarding de fazenda e associação automática de tenant | Não | `201 Created` (`AuthResponse`) |
| `GET` | `/api/v1/tenancy/plans` | Consulta de planos de assinatura comercial (legado/retrocompatibilidade) | Não | `200 OK` (`List<SubscriptionPlanDto>`) |
| `GET` | `/api/v1/tenancy/catalog` | Consulta do catálogo modular self-service (faixas de cabeças + módulos e preços por faixa) | Não | `200 OK` (`ModularSubscriptionCatalogDto`) |
| `POST` | `/api/v1/tenancy/subscription/checkout-session` | Criação de sessão de checkout Stripe multi-item modular | Sim | `200 OK` (`CheckoutSessionResponse`) |
| `POST` | `/api/v1/tenancy/subscription/update-modular` | Alteração self-service de módulos e faixa de cabeças com rateio proporcional | Sim | `200 OK` (`ModularSubscriptionUpdateResult`) |

### Endpoints Backoffice (`/api/v1/backoffice/tenants`)

| Método | Endpoint | Permissão | Descrição |
|---|---|---|---|
| `GET` | `/api/v1/backoffice/tenants` | `tenants.read` | Listagem paginada com filtros |
| `GET` | `/api/v1/backoffice/tenants/{id}` | `tenants.read` | Visão 360 do tenant |
| `POST` | `/api/v1/backoffice/tenants` | `tenants.write` | Cadastro administrativo |
| `PUT` | `/api/v1/backoffice/tenants/{id}` | `tenants.write` | Atualização cadastral (sem alterar plano/status) |
| `POST` | `/api/v1/backoffice/tenants/{id}/suspend` | `tenants.suspend` | Suspensão com justificativa |
| `POST` | `/api/v1/backoffice/tenants/{id}/reactivate` | `tenants.suspend` | Reativação com justificativa |
| `POST` | `/api/v1/backoffice/tenants/{id}/cancel` | `tenants.suspend` | Cancelamento com justificativa |
| `POST` | `/api/v1/backoffice/tenants/{id}/archive` | `tenants.suspend` | Arquivamento com justificativa |
| `POST` | `/api/v1/backoffice/tenants/{id}/protection` | `tenants.suspend` | Proteger/desproteger tenant |
| `GET` | `/api/v1/backoffice/tenants/export` | `tenants.read` | Exportação CSV do recorte filtrado (máx. 10.000 linhas) |
| `PUT` | `/api/v1/backoffice/tenants/{id}/segmentation` | `tenants.write` | Atualizar taxonomias operacionais |
| `PUT` | `/api/v1/backoffice/tenants/{id}/tags` | `tenants.write` | Substituir etiquetas operacionais do tenant |
| `GET` | `/api/v1/backoffice/tenants/tags` | `tenants.read` | Listar catálogo de etiquetas |
| `POST` | `/api/v1/backoffice/tenants/tags` | `tenants.write` | Criar etiqueta operacional |
| `DELETE` | `/api/v1/backoffice/tenants/tags/{tagId}` | `tenants.write` | Desativar etiqueta operacional |
| `GET` | `/api/v1/backoffice/tenants/saved-filters` | `tenants.read` | Listar filtros salvos do admin autenticado |
| `POST` | `/api/v1/backoffice/tenants/saved-filters` | `tenants.read` | Salvar recorte operacional |
| `DELETE` | `/api/v1/backoffice/tenants/saved-filters/{id}` | `tenants.read` | Excluir filtro salvo do admin autenticado |

### Segmentação Operacional (Sub-fase 2.3)

Filtros adicionais em `GET /api/v1/backoffice/tenants`: `sizeSegment`, `commercialRegion`, `productiveProfile`, `churnRisk`, `tagIds[]`, `afterCreatedAtUtc`, `afterId`.

Paginação: offset (`page`/`pageSize`, clamp 1–100) e keyset (`afterCreatedAtUtc` + `afterId`) com ordenação `CreatedAtUtc DESC, Id DESC`.

Erros: `Tenant.InvalidSegmentation`, `Tenant.TagNotFound`, `Tenant.TagInactive`, `Tenant.TagSlugAlreadyExists`, `Tenant.ExportLimitExceeded`.

### Máquina de Estados do Tenant (Sub-fase 2.2)

Estados: `Trial`, `Active`, `PastDue`, `Suspended`, `Cancelled`, `Archived`.

Transições administrativas exigem justificativa (mín. 15 caracteres) e permissão `tenants.suspend`.

Acesso do produtor permitido em: `Trial`, `Active`, `PastDue`. Bloqueado em: `Suspended`, `Cancelled`, `Archived`.
- **Expiração Ativa de Período de Testes (Trial):** Tenants no estado `Trial` possuem prazo padrão de 30 dias registrado em `CurrentPeriodEndUtc`. Quando `CurrentPeriodEndUtc < UtcNow`, o `TenantAccessGuard` intercepta requisições de manejo e dados operacionais retornando `TenancyErrors.TrialExpired` (`Tenant.TrialExpired` / HTTP 403 Forbidden).
- **Worker de Segundo Plano (`SubscriptionLifecycleWorker`):** Executa periodicamente no host (a cada 6 horas por padrão, configurável via `SubscriptionLifecycleOptions`) invocando `ISubscriptionLifecycleService`:
  - **Varredura de Trials:** Tenants em `Trial` com `CurrentPeriodEndUtc < UtcNow` são transicionados formalmente para `Suspended` (`StatusReason = "Período de testes expirado."`).
  - **Carência de Inadimplência (*PastDue Grace Period*):** Tenants em `PastDue` cujo tempo de inadimplência excede a tolerância de 7 dias (`(StatusChangedAtUtc ?? UpdatedAtUtc) <= UtcNow.AddDays(-7)`) são transicionados para `Suspended` (`StatusReason = "Inadimplência não regularizada após prazo de tolerância."`).
  - **Salvaguarda de Tenants Protegidos (`IsProtected == true`):** A suspensão é contida para evitar cortes indevidos em contas estratégicas ou institucionais, gerando registro de auditoria em `TenantSubscriptionHistories` com o motivo de proteção preservada.
  - **Rastreabilidade:** Cada alteração automática efetuada pelo worker grava atomicamente uma entrada em `TenantSubscriptionHistories` com `SubscriptionActionType.Suspended`.
- **Prevenção de Deadlock de Faturamento:** O middleware de acesso `TenantAccessMiddleware` autoriza bypass da checagem para endpoints de consulta de perfil (`/api/v1/tenancy/profile`), catálogo de planos (`/api/v1/tenancy/plans`) e sessões seguras do Stripe (`/api/v1/tenancy/subscription/*`), viabilizando que o produtor bloqueado acesse a tela de contratação e conclua o pagamento sem impedimentos.

Erros: `Tenant.InvalidTransition`, `Tenant.JustificationRequired`, `Tenant.ProtectedTenant`, `Tenant.NotAccessible`, `Tenant.TrialExpired`.


## 3. Casos de Uso (CQRS / MediatR)

### 3.1 `RegisterUserCommand`
- **Contrato:** `RegisterUserCommand(string FullName, string Email, string Password, string? PhoneNumber)`
- **Validações (`RegisterUserCommandValidator`):**
  - Nome completo: Mínimo 3 caracteres, máximo 150.
  - E-mail: Formato de e-mail válido.
  - Senha: Mínimo 8 caracteres, maiúscula, minúscula e número.
- **Regra de Negócio:** Se o e-mail já estiver cadastrado, retorna `Result.Failure(Error.Conflict("User.EmailAlreadyExists", ...))`.

### 3.2 `CreateTenantCommand`
- **Contrato:** `CreateTenantCommand(Guid? UserId, string Name, string CNPJ, string State, string City, string StateRegistration, decimal AreaInHectares, string SubscribedPlan = "Starter", int Capacity = 500, string? UserEmail = null, string? ReferralCode = null, string? OriginIpAddress = null, string? UserAgent = null, string HeadTierCode = "TIER_500", IReadOnlyList<string>? SelectedModules = null)`
- **Validações (`CreateTenantCommandValidator`):** Nome da fazenda obrigatório (3-150 caracteres), UF com exatamente 2 caracteres, capacidade restrita à faixa de teste de 1 a 500 cabeças (`PlanCapacityLimits.StarterLimit`), faixa inicial `TIER_500` e validação dos módulos selecionados contra `PlanCapacityLimits.AllCommercialModules`.
- **Regra de Negócio:** Cria o `Tenant` com `Status = "Trial"` (30 dias), `HeadTierCode = "TIER_500"` (`Capacity` até 500 cabeças), habilita em degustação todos os 7 módulos comerciais (ou os módulos escolhidos em `SelectedModules`, incluindo obrigatoriamente o módulo base `"Cows"`) na coleção `TenantSubscribedModules` e aplica a segmentação padrão via `tenant.ApplyDefaultSegmentation()`. Associa o usuário em `UserTenant` e retorna `AuthResponse` com JWT válido assinado para a fazenda recém-criada.

### 3.3 `ForgotPasswordCommand`
- **Contrato:** `ForgotPasswordCommand(string Email)`
- **Validações (`ForgotPasswordCommandValidator`):** Formato de e-mail válido.
- **Regra de Negócio:** Gera token de 6 dígitos numéricos alfanuméricos com validade de 1 hora (`PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1)`). Retorna `Result.Success`.

### 3.4 `ResetPasswordCommand`
- **Contrato:** `ResetPasswordCommand(string Email, string Token, string NewPassword)`
- **Validações (`ResetPasswordCommandValidator`):** Valida e-mail, obrigatoriedade do token e senha forte.
- **Regra de Negócio:** Verifica se o token corresponde ao usuário e se `PasswordResetTokenExpiresAt > DateTime.UtcNow`. Se válido, atualiza o hash da nova senha e limpa o token.

### 3.5 `UpdateModularSubscriptionCommand`
- **Contrato:** `UpdateModularSubscriptionCommand(string HeadTierCode, IReadOnlyList<string> SelectedModules, bool AllowGracePeriod = false)`
- **Validações (`UpdateModularSubscriptionCommandValidator`):**
  - `HeadTierCode` obrigatório e correspondente a uma faixa cadastrada (`TIER_500`, `TIER_1500`, `TIER_3000`, `TIER_5000_PLUS`).
  - Módulo core `"Cows"` inviolável (inclusão compulsória se omitido).
  - Todos os módulos validados contra o catálogo de módulos comerciais ativos.
- **Regra de Negócio:**
  - Consulta o rebanho ativo através da abstração desacoplada `ITenantActiveHerdCounter`.
  - Se `activeHerd > MaxHeads` e `!AllowGracePeriod`, bloqueia imediatamente com `Result.Failure(License.HeadCapacityExceeded)`.
  - Se `AllowGracePeriod = true`, autoriza a transição para `SubscriptionActionType.DowngradeGracePeriodStarted` com status `GracePeriodActive` por 14 dias (`GracePeriodEndUtc = UtcNow.AddDays(14)`).
  - Executa a atualização atômica no Stripe via `IStripePaymentService.UpdateModularSubscriptionAsync` com `create_prorations` (adicionando/removendo `SubscriptionItems`).
  - Atualiza a coleção `TenantSubscribedModules`, sincroniza `tenant.Capacity = MaxHeads` e `tenant.HeadTierCode`, registra histórico em `TenantSubscriptionHistories` e gera novo JWT com claims atualizadas (`head_tier`, `head_capacity`, `subscribed_modules`), permitindo que a interface Blazor atualize sem recarregar.

### 3.6 `CreateCheckoutSessionCommand` (Modular)
- **Contrato:** `CreateCheckoutSessionCommand(string HeadTierCode, IReadOnlyList<string> SelectedModules, string BillingCycle = "Monthly", string? SuccessUrl = null, string? CancelUrl = null)`
- **Regra de Negócio:**
  - Valida capacidade inicial contra o rebanho ativo (`ITenantActiveHerdCounter`).
  - Garante a presença compulsória do módulo base `"Cows"`.
  - Constrói `SessionLineItemOptions` multi-item no Stripe para a combinação faixa/módulos escolhida com os `PriceIds` correspondentes ao ciclo (mensal ou anual com desconto).
  - Persiste metadados canônicos na sessão do Stripe (`tenant_id`, `head_tier`, `max_heads`, `subscribed_modules`) para sincronização determinística via webhooks.

### 3.7 `LegacyPlanMigrationResolver` & Conversão de Planos
- Converte tenants com planos legados fixos para a matriz modular:
  - `Starter` (e variações comerciais) $\rightarrow$ `HeadTierCode = "TIER_500"`, `Capacity = 500`, Módulos `["Cows", "Breeding", "Calving"]`.
  - `Pro` $\rightarrow$ `HeadTierCode = "TIER_3000"`, `Capacity = 3000`, Módulos `["Cows", "Breeding", "Calving", "Growth", "Nutrition"]`.
  - `Enterprise` $\rightarrow$ `HeadTierCode = "TIER_5000_PLUS"`, `Capacity = 10000`, Todos os 7 módulos comerciais ativos.
- Implementado em `Tenant.MigrateFromLegacyPlan` e executado via migration de banco e script SQL idempotente `20261105_migrate_existing_tenants_to_modular.sql`.

### 3.8 Comandos Backoffice (via `Modules.Backoffice` → MediatR → `Modules.Tenancy`)

- **`CreateTenantForAdminCommand`**: cadastro administrativo sem exigir usuário produtor; provisiona DB do tenant; `OwnerUserEmail` opcional.
- **`UpdateTenantForAdminCommand`**: atualização cadastral; não altera `Status` nem `SubscribedPlan`.
- **`SuspendTenantForAdminCommand`**, **`ReactivateTenantForAdminCommand`**, **`CancelTenantForAdminCommand`**, **`ArchiveTenantForAdminCommand`**, **`SetTenantProtectionForAdminCommand`**: governança de ciclo de vida.
- **`GetTenantsBackofficeQuery`**: listagem paginada com filtros (busca, status, plano, UF, owners, segmentação, tags).
- **`GetTenantBackofficeDetailQuery`**: visão 360 com limites, owners, segmentação, tags, contagem de time e unidades produtivas.
- **`UpdateTenantSegmentationForAdminCommand`**, **`ReplaceTenantTagsForAdminCommand`**, **`CreateOperationalTagForAdminCommand`**, **`DeactivateOperationalTagForAdminCommand`**, **`GetOperationalTagsQuery`**, **`ExportTenantsBackofficeQuery`**: segmentação operacional e exportação CSV.

**Unicidade:** `CnpjNormalized` e `ExternalIdentifier` são validados no onboarding (`CreateTenantCommand`) e no backoffice.

**Limites de Capacidade Zootécnica:** `HeadTierCapacityLimits` — `TIER_500`: 500 cabeças, `TIER_1500`: 1.500 cabeças, `TIER_3000`: 3.000 cabeças, `TIER_5000_PLUS`: 10.000 cabeças.

---

## 4. Componentes Frontend (Blazor WASM)

- **`Login.razor` (`/login`):** Tela de login em 2 passos (Credenciais -> Seleção de Fazenda para usuários multi-tenant). Possui links diretos para `/register` e `/forgot-password`.
- **`Register.razor` (`/register`):** Formulário reativo de auto-cadastro com feedback visual, tratamento de erro do Result Pattern e card de confirmação.
- **`ForgotPassword.razor` (`/forgot-password`):** Assistente de recuperação em 2 passos (Solicitar código -> Redefinir senha).
- **`OnboardingWizard.razor` (`/onboarding`):** Assistente de boas-vindas com dados da fazenda e degustação modular gratuita de 30 dias na faixa `TIER_500`.
- **`SubscriptionManagement.razor` (`/settings/subscription`):** Configurador interativo "Monte seu Plano" com seleção de faixa de cabeças (`HeadTierSelector.razor`), grid de módulos add-on (`ModuleSelectionGrid.razor`) e barra fixa de carrinho (`ModularCartStickyBar.razor`) com checkout multi-item ou atualização proporcional imediata.
- **`ModuleGuard.razor`:** Guardião declarativo com motor de upsell contextual dinâmico (`ModuleGuardUpsellCalculator`) para ativação em 1-clique com proration.

---

## 5. Testes Unitários & Integração (`CriaCerto.Modules.Tenancy.UnitTests` & `CriaCerto.Architecture.IntegrationTests`)

- `RegisterUserCommandHandlerTests` & `RegisterUserCommandValidatorTests`: Criação de usuários e validação de requisitos de senha/e-mail.
- `CreateTenantCommandHandlerTests` & `CreateTenantCommandValidatorTests`: Criação de fazenda em Trial com faixa `TIER_500` e inclusão do módulo `"Cows"`.
- `JwtServiceTests`: Emissão determinística de claims modulares (`head_tier`, `head_capacity`, `subscribed_module` e `subscribed_modules`).
- `ModularCartCalculationTests`: Testes unitários do motor de precificação `ModularCartCalculator` (mensal/anual, 20% desconto anual, recálculo de custo por cabeça/mês e auto-inclusão de `"Cows"`).
- `ModularCheckoutAndCapacityTests`: Validação de checkout modular e comando de atualização, rejeição por capacidade excedida e disparo de Grace Period de 14 dias.
- `HeadTierCapacityValidationTests`: Testes de regras de fronteira de capacidade zootécnica por faixa.
- `LegacyPlanMigrationTests`: Cobertura de transição canônica de planos legados para a matriz modular com integridade de dados.
- `StripeWebhookIntegrationTests`: 12 testes de integração cobrindo o ciclo de vida completo de faturamento multi-item (checkout, adições, remoções, upgrades, downgrades e resolução reversa via `PriceIds`).
- `StripeWebhookIdempotencyTests` & `StripeWebhookNullKeyAndTenantProtectionTests`: Idempotência de eventos e blindagem de tenants protegidos (`IsProtected`).
- `OnboardingIntegrationTests` & `CommercialPartnerLoginIntegrationTests`: Fluxos integrados de onboarding e login de parceiros comerciais.
