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
  - `SubscribedPlan`: `string` (Starter, Pro, Enterprise)
  - `Capacity`: `int` (limite de cabeças; validado via `PlanCapacityLimits`)
  - `TechnicalOwnerName` / `TechnicalOwnerEmail`
  - `CommercialOwnerName` / `CommercialOwnerEmail`
  - `SizeSegment`: `Micro`, `Small`, `Medium`, `Large` (default por `Capacity`)
  - `CommercialRegion`: `Norte`, `Nordeste`, `CentroOeste`, `Sudeste`, `Sul` (default por UF)
  - `ProductiveProfile`: `Corte`, `Leite`, `Misto`, `Cria`, `Recria`, `Engorda`, `Confinamento`
  - `ChurnRisk`: `None`, `Low`, `Medium`, `High`, `Critical`
  - `CreatedAtUtc` / `UpdatedAtUtc`

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


---

## 2. Endpoints da API (`/api/auth`)

| Método | Endpoint | Descrição | Requer Auth | Status de Sucesso |
|---|---|---|---|---|
| `POST` | `/api/auth/login` | Autenticação por e-mail e senha | Não | `200 OK` (`AuthResponse`) |
| `POST` | `/api/auth/select-tenant` | Seleção de tenant para contas com múltiplas fazendas | Não | `200 OK` (`AuthResponse`) |
| `POST` | `/api/auth/register` | Auto-cadastro de novo usuário (Sign-Up) | Não | `201 Created` (`UserDto`) |
| `POST` | `/api/auth/forgot-password` | Solicitação de código/token para redefinição de senha | Não | `200 OK` (token) |
| `POST` | `/api/auth/reset-password` | Redefinição de senha com token de verificação | Não | `200 OK` |
| `POST` | `/api/v1/auth/refresh-token` | Renovação de claims e token JWT para o tenant ativo | Sim | `200 OK` (`RefreshTokenResult`) |
| `POST` | `/api/v1/tenancy/farms` | Onboarding de fazenda e associação automática de tenant | Não | `201 Created` (`AuthResponse`) |
| `GET` | `/api/v1/tenancy/plans` | Consulta de planos de assinatura comercial | Não | `200 OK` (`List<SubscriptionPlanDto>`) |

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
- **Expiração Ativa de Período de Testes (Trial):** Tenants no estado `Trial` possuem prazo padrão de 14 dias registrado em `CurrentPeriodEndUtc`. Quando `CurrentPeriodEndUtc < UtcNow`, o `TenantAccessGuard` intercepta requisições de manejo e dados operacionais retornando `TenancyErrors.TrialExpired` (`Tenant.TrialExpired` / HTTP 403 Forbidden).
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
- **Contrato:** `CreateTenantCommand(Guid? UserId, string Name, string CNPJ, string State, string City, string StateRegistration, decimal AreaInHectares, string SubscribedPlan = "Starter", int Capacity = 500, string? UserEmail = null)`
- **Validações (`CreateTenantCommandValidator`):** Nome da fazenda obrigatório (3-150 caracteres), UF com exatamente 2 caracteres, capacidade restrita à faixa de teste de 1 a 500 cabeças (`PlanCapacityLimits.StarterLimit`) e plano restrito exclusivamente ao plano padrão de trial (`Starter`). Tentativas de passar outros planos (`Pro`, `Enterprise`) ou capacidades acima de 500 no onboarding gratuito são rejeitadas preventivamente.
- **Regra de Negócio:** Cria o `Tenant` forçando o plano padrão de testes (`Starter`), capacidade limitada a até 500 cabeças, status inicial `Trial` (14 dias) e aplica a segmentação padrão via `tenant.ApplyDefaultSegmentation()`. Associa o usuário em `UserTenant` e retorna `AuthResponse` com JWT válido assinado para a fazenda recém-criada.

### 3.3 `ForgotPasswordCommand`
- **Contrato:** `ForgotPasswordCommand(string Email)`
- **Validações (`ForgotPasswordCommandValidator`):** Formato de e-mail válido.
- **Regra de Negócio:** Gera token de 6 dígitos numéricos alfanuméricos com validade de 1 hora (`PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1)`). Retorna `Result.Success`.

### 3.5 Comandos Backoffice (via `Modules.Backoffice` → MediatR → `Modules.Tenancy`)

- **`CreateTenantForAdminCommand`**: cadastro administrativo sem exigir usuário produtor; provisiona DB do tenant; `OwnerUserEmail` opcional.
- **`UpdateTenantForAdminCommand`**: atualização cadastral; não altera `Status` nem `SubscribedPlan`.
- **`SuspendTenantForAdminCommand`**, **`ReactivateTenantForAdminCommand`**, **`CancelTenantForAdminCommand`**, **`ArchiveTenantForAdminCommand`**, **`SetTenantProtectionForAdminCommand`**: governança de ciclo de vida.
- **`GetTenantsBackofficeQuery`**: listagem paginada com filtros (busca, status, plano, UF, owners, segmentação, tags).
- **`GetTenantBackofficeDetailQuery`**: visão 360 com limites, owners, segmentação, tags, contagem de time e unidades produtivas.
- **`UpdateTenantSegmentationForAdminCommand`**, **`ReplaceTenantTagsForAdminCommand`**, **`CreateOperationalTagForAdminCommand`**, **`DeactivateOperationalTagForAdminCommand`**, **`GetOperationalTagsQuery`**, **`ExportTenantsBackofficeQuery`**: segmentação operacional e exportação CSV.

**Unicidade:** `CnpjNormalized` e `ExternalIdentifier` são validados no onboarding (`CreateTenantCommand`) e no backoffice.

**Limites de plano:** `PlanCapacityLimits` — Starter: 500, Pro: 2500, Enterprise: ilimitado.

### 3.4 `ResetPasswordCommand`
- **Contrato:** `ResetPasswordCommand(string Email, string Token, string NewPassword)`
- **Validações (`ResetPasswordCommandValidator`):** Valida e-mail, obrigatoriedade do token e senha forte.
- **Regra de Negócio:** Verifica se o token corresponde ao usuário e se `PasswordResetTokenExpiresAt > DateTime.UtcNow`. Se válido, atualiza o hash da nova senha e limpa o token.

---

## 4. Componentes Frontend (Blazor WASM)

- **`Login.razor` (`/login`):** Tela de login em 2 passos (Credenciais -> Seleção de Fazenda para usuários multi-tenant). Possui links diretos para `/register` e `/forgot-password`.
- **`Register.razor` (`/register`):** Formulário reativo de auto-cadastro com feedback visual, tratamento de erro do Result Pattern e card de confirmação.
- **`ForgotPassword.razor` (`/forgot-password`):** Assistente de recuperação em 2 passos (Solicitar código -> Redefinir senha).
- **`OnboardingWizard.razor` (`/onboarding`):** Assistente de 3 passos para perfil do produtor, dados da fazenda e seleção de plano/capacidade com vinculação direta de tenant.

---

## 5. Testes Unitários & Integração (`CriaCerto.Modules.Tenancy.UnitTests` & `CriaCerto.Architecture.IntegrationTests`)

- `RegisterUserCommandHandlerTests`: Testes de criação bem-sucedida e rejeição de e-mail duplicado (`Error.Conflict`).
- `RegisterUserCommandValidatorTests`: Testes de regras de validação cliente/servidor.
- `CreateTenantCommandHandlerTests`: Testes de criação de fazenda, associação em `UserTenant` e geração de JWT.
- `CreateTenantCommandValidatorTests`: Testes de validação de dados da fazenda e plano.
- `ForgotPasswordCommandHandlerTests`: Testes de geração de token e expiração.
- `ResetPasswordCommandHandlerTests`: Testes de alteração de senha e rejeição de tokens expirados/inválidos.
- `StripeWebhookIdempotencyTests`: Testes de deduplicação e idempotência de eventos Stripe via tabela dedicada.
- `StripeWebhookNullKeyAndTenantProtectionTests`: Testes de sanitização de chaves nulas/vazias e salvaguarda de tenants protegidos (`IsProtected`).
- `OnboardingIntegrationTests`: Teste de integração end-to-end do fluxo Registro -> Onboarding da Fazenda -> Login sem erro `Auth.NoTenantAssociation`.
