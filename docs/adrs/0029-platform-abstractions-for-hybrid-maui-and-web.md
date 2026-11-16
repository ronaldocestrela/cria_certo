# ADR 0029: Abstrações de Plataforma (Auth, Storage, Downloads e Conectividade) para Arquitetura Híbrida Web e .NET MAUI

## Status
Implementado na Fase 2 (Implemented in Phase 2)

## Contexto
Após a extração da Razor Class Library (`CriaCerto.Shared.UI`) formalizada no [ADR 0028](0028-hybrid-maui-blazor-shared-ui-rcl.md), a biblioteca de componentes Razor ainda continha acoplamentos diretos com APIs específicas de navegadores da Web:
- `IJSRuntime` invocando métodos de `localStorage` para ler e persistir o token de autenticação JWT (`authToken`) no `CustomAuthStateProvider`, `OfflineSyncService` e em todos os clientes de API (`PlantelApiClient`, `BreedingOpsApiClient`, `BackofficeApiClient`, etc.).
- Verificações de ambiente como `OperatingSystem.IsBrowser()` dentro de componentes compartilhados como `CustomAuthStateProvider.cs`, o que impedia a recuperação e uso de tokens quando a interface fosse executada dentro de hosts não-browser, como o `BlazorWebView` do .NET MAUI Mobile (onde `OperatingSystem.IsBrowser()` retorna `false`).
- Invocação direta da função JavaScript global `window.downloadFileFromByteArray` para relatórios e exportações de dados nos componentes de Analytics, Backoffice e Configurações de Perfil LGPD.
- Dependência direta de `offlineSync.js` e do `IndexedDB` dentro de `OfflineSyncService`, além de chamadas ad-hoc a `localStorage.setItem` em componentes como `MoveLotModal.razor`.

No ambiente mobile nativo do .NET MAUI (Android e iOS):
- A persistência de tokens sensíveis deve ocorrer via APIs nativas de cofre seguro (`SecureStorage` via Android KeyStore e iOS Keychain).
- O download e manuseio de arquivos em memória deve acionar as folhas nativas de compartilhamento do sistema operacional móvel (`Share.Default.RequestAsync`).
- A detecção de conectividade é mais precisa e resiliente através de APIs nativas do subsistema de rede (`Connectivity.Current`).
- A fila offline de operações de manejo pecuário em curral/pasto deve operar com persistência transacional local (SQLite) além de `IndexedDB` no browser.

Para permitir que `CriaCerto.Shared.UI` execute de maneira 100% agnóstica de plataforma sem exceções nem duplicação de lógica, é necessário introduzir interfaces de abstração de plataforma por Inversão de Controle (IoC).

## Decisão

1. **Criação das Interfaces de Abstração em `CriaCerto.Shared.UI/Abstractions/`:**
   - `ITokenStorageService`: Métodos assíncronos `GetTokenAsync()`, `SetTokenAsync(string token)` e `RemoveTokenAsync()` para leitura, gravação e expurgo de tokens JWT.
   - `IFileExportService`: Método assíncrono `ExportFileAsync(string fileName, string contentType, byte[] content)` para exportação de dados (CSV, JSON, PDF).
   - `IConnectivityService`: Propriedade `IsOnline`, evento reativo `ConnectivityChanged` e inicialização assíncrona `InitializeAsync()`.
   - `IOfflineSyncStorage`: Métodos assíncronos `InitializeAsync()`, `GetPendingOperationsAsync()`, `SavePendingOperationAsync(SyncOperation operation)`, `RemovePendingOperationAsync(Guid operationId)` e `ClearPendingOperationsAsync()` para governança da fila de sincronização.

2. **Refatoração dos Serviços e Componentes em `CriaCerto.Shared.UI`:**
   - **`CustomAuthStateProvider.cs`:** Injeção de `ITokenStorageService` em substituição ao `IJSRuntime`. Remoção da trava `if (!OperatingSystem.IsBrowser())`, delegando o tratamento de indisponibilidade ou execução no servidor às implementações concretas da plataforma.
   - **`OfflineSyncService.cs`:** Injeção desacoplada de `IConnectivityService`, `IOfflineSyncStorage`, `ITokenStorageService` e `HttpClient`. Reação a mudanças de conectividade via eventos tipados e persistência de fila através do storage de sincronização sem chamadas diretas a scripts JS.
   - **Clientes HTTP (`*ApiClient.cs`):** Todos os clientes de API (`PlantelApiClient`, `BreedingOpsApiClient`, `CalvingApiClient`, `GrowthApiClient`, `NutritionApiClient`, `SanitaryApiClient`, `AnalyticsApiClient`, `TenancyApiClient`, `BackofficeApiClient`, `PartnerApiClient`, `ReferralApiClient`) passam a suportar construtor primário com `ITokenStorageService`, mantendo construtor secundário com `IJSRuntime` via `JSRuntimeTokenStorageAdapter` para compatibilidade estrita com testes e componentes legados.
   - **`MoveLotModal.razor`:** Substituição de escrita direta em `localStorage` pela invocação padronizada de `IOfflineSyncService.EnqueueOperationAsync("growth", "move-lot", command)`.
   - **Componentes de Exportação:** Injeção de `IFileExportService` em `ProfileManagement.razor`, `PayoutsManagement.razor`, `TenantsManagement.razor` e `ExportReportModal.razor`.

3. **Criação das Implementações Browser em `CriaCerto.Web.Client/Services/Browser/`:**
   - `BrowserTokenStorageService`: Acesso ao `localStorage` com guarda defensiva `if (!OperatingSystem.IsBrowser()) return null;` para segurança durante a pré-renderização estática (SSR).
   - `BrowserFileExportService`: Invocação de `window.downloadFileFromByteArray`.
   - `BrowserConnectivityService`: Integração com eventos de rede da janela (`window.addEventListener("online" / "offline")`) e `navigator.onLine`.
   - `BrowserOfflineSyncStorage`: Integração com `IndexedDB` através do helper `criaCertoOfflineSync` em `offlineSync.js`.

4. **Registro de Injeção de Dependências:**
   - Registradas as implementações de browser como serviços com escopo (`AddScoped`) em `CriaCerto.Web.Client/Program.cs` e no host SSR `CriaCerto.Web/Program.cs`.

## Consequências

### Positivas
- **Desacoplamento Total:** `CriaCerto.Shared.UI` deixa de ter qualquer dependência direta de APIs de janela de navegador ou `localStorage` para seus fluxos centrais.
- **Pronto para .NET MAUI (Fases 3 e 4):** A inclusão de implementações móveis nativas (`MauiTokenStorageService` com `SecureStorage`, `MauiFileExportService` com `Share`, `MauiConnectivityService` com `Connectivity`, `MauiOfflineSyncStorage` com `SQLite`) na Fase 4 conectará os mesmos componentes Razor sem nenhuma alteração na RCL.
- **Suporte Seguro a Prerendering (SSR):** As guardas defensivas nas implementações browser impedem exceções de JS Interop no host SSR `CriaCerto.Web`.
- **Retrocompatibilidade Preservada:** Todos os testes unitários legados de clientes HTTP continuam funcionando sem necessidade de alteração estrutural através do adaptador `JSRuntimeTokenStorageAdapter`.

### Mitigações e Desafios
- **Testes de Integração JS Interop:** Testes unitários das implementações de browser dependem de mocks de `IJSRuntime` ou guardas de ambiente não-browser.
- **Transição de Armazenamento:** Dados pendentes no `IndexedDB` permanecem isolados no ambiente Web; no ambiente móvel MAUI, a persistência passará a ser gerenciada via SQLite na Fase 4.

