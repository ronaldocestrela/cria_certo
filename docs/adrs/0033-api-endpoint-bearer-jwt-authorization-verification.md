# ADR 0033: Verificação de Endpoints e Autenticação Bearer JWT na CriaCerto.Api para .NET MAUI

## Status
Implementado na Fase 5 (Implemented in Phase 5 - Tarefa 5.3)

## Contexto
Com a implementação da arquitetura híbrida .NET MAUI Blazor Hybrid (`CriaCerto.Mobile`) ([ADR 0030](0030-hybrid-maui-blazor-mobile-host-project.md), [ADR 0031](0031-hybrid-maui-blazor-native-platform-services.md) e [ADR 0032](0032-mobile-platform-security-and-configuration-android-ios.md)), o consumo de dados da `CriaCerto.Api` passou a ser compartilhado entre dois ambientes com características de transporte substancialmente distintas:
1. **Host Web (`CriaCerto.Web` / Blazor WASM):** Opera em navegadores com suporte a cookies de mesma origem e proxy reverso YARP.
2. **Host Mobile Nativo (`CriaCerto.Mobile` / Android & iOS):** O `BlazorWebView` consome a API REST diretamente via `HttpClient` nativo (apontando para loopback `http://10.0.2.2:5274/` no emulador Android ou `https://app.criacerto.com.br/` em produção). Clientes nativos em dispositivos móveis não possuem cookies de sessão compartilhados com o navegador nem contexto de mesma origem.

Para garantir paridade operacional estrita, ausência de falhas em campo e proteção contra vulnerabilidades de acoplamento a cookies de sessão (como CSRF), era mandatório verificar e auditar se todos os endpoints da `CriaCerto.Api` chamados pelos `*ApiClient` da Razor Class Library `CriaCerto.Shared.UI` ([ADR 0028](0028-hybrid-maui-blazor-shared-ui-rcl.md)) operam de forma 100% *stateless* através do cabeçalho HTTP `Authorization: Bearer <token>`, sem nenhuma dependência de cookies de sessão web.

Além disso, durante a auditoria dos clientes da RCL, identificou-se que o serviço `FeatureFlagClientService` realizava requisições a `api/v1/backoffice/feature-flags` e `/slo-health` sem injetar o `ITokenStorageService`, dependendo de estado ambiental prévio do `HttpClient`.

## Decisão

1. **Exclusividade do Esquema Bearer JWT na `CriaCerto.Api`:**
   - A API backend registra compulsoriamente apenas o esquema `JwtBearerDefaults.AuthenticationScheme` (`builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`).
   - Esquemas de autenticação baseados em cookies de sessão web (`CookieAuthenticationDefaults`) **não são registrados nem permitidos** na API, impedindo dependências ocultas de navegadores.
   - Todos os grupos de rotas de domínio (`/api/breeding`, `/api/calving`, `/api/growth`, `/api/nutrition`, `/api/sanitary`, `/api/analytics`, `/api/v1/backoffice`, `/api/v1/sync`, `/api/v1/tenancy/*`, `/api/v1/referral/*`) aplicam compulsoriamente a política de autorização `.RequireAuthorization()`.
   - Nenhum endpoint de negócio inspeciona `Request.Cookies` para autenticação ou autorização de usuários.

2. **Endurecimento de Clientes na RCL (`CriaCerto.Shared.UI`):**
   - Todos os 11 clientes de API (`PlantelApiClient`, `BreedingOpsApiClient`, `GrowthApiClient`, `NutritionApiClient`, `CalvingApiClient`, `SanitaryApiClient`, `AnalyticsApiClient`, `TenancyApiClient`, `BackofficeApiClient`, `PartnerApiClient`, `ReferralApiClient`) e o `OfflineSyncService` injetam `ITokenStorageService` e executam `await AttachTokenAsync()` antes de cada requisição.
   - **Refatoração do `FeatureFlagClientService`:** O serviço foi endurecido com a injeção de `ITokenStorageService? tokenStorage = null` e rotina `AttachTokenAsync()`, assegurando envio do cabeçalho `Authorization: Bearer <token>` em todas as operações de consulta e governança de feature flags (`GetFlagsAsync`, `GetSloHealthAsync`, `ToggleFlagAsync`, `UpdateRolloutAsync`, `TriggerKillSwitchAsync`, `RestoreKillSwitchAsync`).

3. **Living Documentation & Automação de Testes de Arquitetura:**
   - **`ApiClientAuthorizationHeaderTests.cs` (207 testes unitários):** Bateria de testes automatizados na suíte `CriaCerto.Web.Client.UnitTests` validando que todos os clientes de API e o worker de sincronização offline anexam o token Bearer quando presente e limpam o cabeçalho adequadamente na ausência do token.
   - **`ApiBearerAuthorizationVerificationTests.cs` (117 testes de integração/arquitetura):** Bateria de testes na suíte `CriaCerto.Architecture.IntegrationTests` validando via inspeção estática e testes dinâmicos:
     - Registro exclusivo do esquema Bearer JWT na inicialização da API;
     - Inexistência de manipulação de cookies de sessão para autenticação;
     - Exigência compulsória de autorização em todos os grupos de rotas;
     - Geração e validação de tokens JWT gerados pelo `JwtService` com claims de Tenancy e segurança.

## Consequências

### Positivas
- **Interoperabilidade Total Web e Mobile:** O mesmo código cliente (`CriaCerto.Shared.UI`) opera de forma transparente no navegador e em dispositivos nativos Android e iOS sem alteração de contratos ou requisições.
- **Zero Dependência de Cookies de Sessão:** A API é integralmente stateless, eliminando riscos de CSRF em requisições de API e viabilizando chamadas nativas em rede local/remota a partir do .NET MAUI.
- **Segurança Reforçada:** O `FeatureFlagClientService` opera de forma autocontida sem depender de inicialização ambiental prévia de outros clientes.
- **Prevenção Contínua de Regressões:** Testes unitários e de integração validam continuamente a integridade dos cabeçalhos de autorização e o esquema Bearer da API em ambiente CI/CD Linux.

