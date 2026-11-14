# Roadmap de Correção: Conformidade PWA & Sincronização Offline (`CriaCerto`)

> **Objetivo para o Agente LLM:** Este documento descreve o plano de execução passo a passo para tornar o aplicativo **CriaCerto** (`Blazor WebAssembly` + `ASP.NET Core Minimal API`) 100% aderente aos padrões de instalabilidade **Progressive Web App (PWA)** e corrigir toda a cadeia de operação e sincronização **Offline-First** (Service Worker, IndexedDB, `IOfflineSyncService`, clientes HTTP e endpoint de sincronização na API).
>
> **Regra de Execução:** Execute as Fases sequencialmente (Fase 1 -> Fase 6). Ao concluir cada fase, execute os comandos de validação indicados antes de avançar para a próxima.

---

## Visão Geral da Arquitetura Envolvida

- **Host Web / Shell SSR:** `src/Web/CriaCerto.Web/CriaCerto.Web/`
- **Cliente Blazor WASM (PWA):** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/`
- **API Backend (Host):** `src/Host/CriaCerto.Api/`
- **Testes Unitários do Cliente:** `tests/Unit/CriaCerto.Web.Client.UnitTests/`

---

## Fase 1: Ativos Visuais, Manifesto Web (`manifest.webmanifest`) e Meta Tags Mobile

### Contexto do Problema
O manifesto atual referencia `icon-192.png` e `icon-512.png`, que não existem no projeto (causando falha de instalabilidade no Chrome/Edge/Android). Além disso, a descrição no manifesto ainda menciona suinocultura em inglês (`"Swine Management SaaS foundation app."`) e o `App.razor` não possui as meta tags para iOS/Safari.

### Tarefa 1.1: Gerar os ícones `icon-192.png` e `icon-512.png`
- **Arquivo de Origem:** `src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/imgs/CriaCerto.png` (dimensões atuais: `1536x1536` RGBA).
- **Arquivos de Destino:**
  1. `src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-192.png` (`192x192` px, PNG)
  2. `src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-512.png` (`512x512` px, PNG)
  3. (Espelhamento no Host para resolução estática direta) `src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/icon-192.png` e `src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/icon-512.png`
- **Subtarefa 1.1.1:** Redimensionar `CriaCerto.png` usando `python3` (Pillow/PIL), `ffmpeg` ou `convert` (ImageMagick) via terminal para gerar os arquivos nas dimensões exatas `192x192` e `512x512`.
  - Exemplo com `ffmpeg`:
    ```bash
    ffmpeg -y -i src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/imgs/CriaCerto.png -vf scale=192:192 src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-192.png
    ffmpeg -y -i src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/imgs/CriaCerto.png -vf scale=512:512 src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-512.png
    cp src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-192.png src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/icon-192.png
    cp src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-512.png src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/icon-512.png
    ```
- **Subtarefa 1.1.2:** Remover o arquivo residual `src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/CriaCerto.png:Zone.Identifier`.

### Tarefa 1.2: Atualizar `manifest.webmanifest`
- **Arquivo:** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/manifest.webmanifest`
- **Subtarefa 1.2.1:** Substituir o conteúdo para incluir `id`, `scope`, `lang`, `dir`, `orientation`, descrição correta em `pt-BR` e suporte a `purpose: "any maskable"`:
  ```json
  {
    "id": "/",
    "name": "CriaCerto - Gestão Pecuária Inteligente",
    "short_name": "CriaCerto",
    "description": "Plataforma SaaS de gestão pecuária de precisão com operação offline no curral.",
    "lang": "pt-BR",
    "dir": "ltr",
    "start_url": "/",
    "scope": "/",
    "display": "standalone",
    "orientation": "portrait-primary",
    "background_color": "#f5f7f5",
    "theme_color": "#1f5638",
    "categories": ["business", "productivity", "utilities"],
    "icons": [
      {
        "src": "/icon-192.png",
        "sizes": "192x192",
        "type": "image/png",
        "purpose": "any maskable"
      },
      {
        "src": "/icon-512.png",
        "sizes": "512x512",
        "type": "image/png",
        "purpose": "any maskable"
      }
    ]
  }
  ```

### Tarefa 1.3: Adicionar Meta Tags PWA/iOS em `App.razor`
- **Arquivo:** `src/Web/CriaCerto.Web/CriaCerto.Web/Components/App.razor`
- **Subtarefa 1.3.1:** No bloco `<head>` (próximo às linhas 16-19), adicionar as tags de compatibilidade com iOS/Safari e dispositivos móveis:
  ```html
  <link rel="manifest" href="manifest.webmanifest" />
  <link rel="apple-touch-icon" sizes="192x192" href="icon-192.png" />
  <meta name="mobile-web-app-capable" content="yes" />
  <meta name="apple-mobile-web-app-capable" content="yes" />
  <meta name="apple-mobile-web-app-status-bar-style" content="default" />
  <meta name="apple-mobile-web-app-title" content="CriaCerto" />
  ```

---

## Fase 2: Página de Fallback Offline (`offline.html`) em Português (`pt-BR`)

### Contexto do Problema
O arquivo `offline.html` está em inglês (`lang="en"`, `"Offline Mode Active"`) e não possui botão de tentativa de reconexão nem identidade visual alinhada com o restante do sistema em português.

### Tarefa 2.1: Reescrever `offline.html`
- **Arquivo:** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/offline.html`
- **Subtarefa 2.1.1:** Atualizar o HTML para `lang="pt-BR"`, traduzir textos para o contexto de campo/curral e adicionar botão `"Tentar Novamente"` (`window.location.reload()`) e listener automático `window.addEventListener('online', () => window.location.replace('/'))`:
  ```html
  <!doctype html>
  <html lang="pt-BR">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="theme-color" content="#1f5638" />
    <title>CriaCerto - Modo de Campo (Offline)</title>
    <style>
      body {
        margin: 0;
        font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
        background: #f5f7f5;
        color: #1f5638;
        display: grid;
        place-items: center;
        min-height: 100vh;
        padding: 1rem;
        box-sizing: border-box;
      }
      main {
        max-width: 520px;
        width: 100%;
        text-align: center;
        padding: 2.25rem 2rem;
        background: #ffffff;
        border-radius: 14px;
        box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
        border: 1px solid rgba(31, 86, 56, 0.12);
      }
      .badge {
        display: inline-block;
        background: #e8f3ec;
        color: #1f5638;
        font-weight: 600;
        font-size: 0.85rem;
        padding: 0.35rem 0.85rem;
        border-radius: 999px;
        margin-bottom: 1rem;
      }
      h1 {
        margin: 0 0 0.75rem;
        font-size: 1.65rem;
      }
      p {
        color: #4a5d52;
        line-height: 1.5;
        margin-bottom: 1.5rem;
      }
      .actions {
        display: flex;
        gap: 0.75rem;
        justify-content: center;
        flex-wrap: wrap;
      }
      button, a.btn {
        appearance: none;
        border: none;
        background: #1f5638;
        color: #ffffff;
        font-weight: 600;
        font-size: 0.95rem;
        padding: 0.75rem 1.4rem;
        border-radius: 8px;
        cursor: pointer;
        text-decoration: none;
      }
      button:hover, a.btn:hover {
        background: #17422b;
      }
    </style>
  </head>
  <body>
    <main>
      <span class="badge">Modo de Campo (Sem Conexão)</span>
      <h1>Você está operando Offline</h1>
      <p>
        Não foi possível conectar ao servidor neste momento. As páginas e recursos já carregados no dispositivo continuam disponíveis e os registros feitos no curral serão sincronizados assim que o sinal retornar.
      </p>
      <div class="actions">
        <button type="button" onclick="window.location.reload()">Tentar Reconectar</button>
        <a class="btn" href="/">Voltar ao Início</a>
      </div>
    </main>
    <script>
      window.addEventListener("online", () => window.location.reload());
    </script>
  </body>
  </html>
  ```

---

## Fase 3: Correção do Service Worker e Manifesto de Ativos do Blazor WASM

### Contexto do Problema
1. O `service-worker.js` atual intercepta qualquer requisição `GET` (inclusive chamadas dinâmicas para `/api/...`, `/health`, scripts externos do Google Analytics/Fonts) e armazena no cache de shell ou retorna o HTML `offline.html` quando uma chamada de API falha por falta de rede — o que faz o `HttpClient` do Blazor lançar `JsonException` ao tentar ler HTML como JSON.
2. Não há `self.skipWaiting()` no `install` nem `self.clients.claim()` no `activate`.
3. O projeto `CriaCerto.Web.Client.csproj` não declara `<ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>`, impedindo o pré-cache confiável dos arquivos `_framework/*.wasm` em produção.

### Tarefa 3.1: Configurar `ServiceWorkerAssetsManifest` no `.csproj` do Cliente
- **Arquivo:** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/CriaCerto.Web.Client.csproj`
- **Subtarefa 3.1.1:** Adicionar `<ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>` ao `<PropertyGroup>` e o `<ServiceWorker>` ao `<ItemGroup>`:
  ```xml
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NoDefaultLaunchSettingsFile>true</NoDefaultLaunchSettingsFile>
    <StaticWebAssetProjectMode>Default</StaticWebAssetProjectMode>
    <BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>
    <ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>
  </PropertyGroup>

  <ItemGroup>
    <ServiceWorker Include="wwwroot\service-worker.js" PublishedContent="wwwroot\service-worker.published.js" />
  </ItemGroup>
  ```

### Tarefa 3.2: Reescrever `service-worker.js` (Desenvolvimento e Base de Runtime)
- **Arquivo:** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.js`
- **Subtarefa 3.2.1:** Implementar regras estritas de roteamento e cache:
  - Ignorar requisições que não sejam `GET`.
  - Ignorar requisições de origens externas (`url.origin !== self.location.origin`), como `googletagmanager.com`.
  - **Nunca** interceptar ou retornar `offline.html` para rotas de API (`url.pathname.startsWith("/api/")` ou `url.pathname.startsWith("/health")`). Para `/api/`, deixar a requisição ir direto para a rede (`fetch(event.request)`) para que o C# detecte a falha de rede limpa e acione o fallback do `IOfflineSyncService`.
  - Para **navegação de páginas** (`event.request.mode === "navigate"`): usar estratégia *Network-First* com fallback para o cache da rota ou `/` (App Shell do Blazor) e, em último caso, `/offline.html`.
  - Para **ativos estáticos locais** (`.js`, `.wasm`, `.css`, `.png`, `.woff2`, `.webmanifest`): usar *Stale-While-Revalidate* ou *Cache-First com atualização*, incluindo os ativos essenciais em `CORE_ASSETS`.
- **Código de Referência para `service-worker.js`:**
  ```javascript
  const CACHE_NAME = "criacerto-shell-v2";
  const OFFLINE_URL = "/offline.html";

  const CORE_ASSETS = [
    "/",
    OFFLINE_URL,
    "/manifest.webmanifest",
    "/icon-192.png",
    "/icon-512.png",
    "/favicon.png",
    "/app.css",
    "/js/offlineSync.js",
    "/js/downloadHelper.js"
  ];

  self.addEventListener("install", event => {
    self.skipWaiting();
    event.waitUntil(
      caches.open(CACHE_NAME).then(async cache => {
        for (const asset of CORE_ASSETS) {
          try {
            await cache.add(asset);
          } catch (err) {
            console.warn("[SW] Falha ao fazer pre-cache do ativo:", asset, err);
          }
        }
      })
    );
  });

  self.addEventListener("activate", event => {
    event.waitUntil(
      caches.keys()
        .then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))))
        .then(() => self.clients.claim())
    );
  });

  self.addEventListener("fetch", event => {
    if (event.request.method !== "GET") {
      return;
    }

    const url = new URL(event.request.url);

    // Não interceptar origens externas nem endpoints de API / Health
    if (url.origin !== self.location.origin ||
        url.pathname.startsWith("/api/") ||
        url.pathname.startsWith("/health")) {
      return;
    }

    // Navegação de documento HTML (SPA / Blazor App Shell)
    if (event.request.mode === "navigate") {
      event.respondWith(
        fetch(event.request)
          .then(response => {
            if (response.ok) {
              const clone = response.clone();
              caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
            }
            return response;
          })
          .catch(async () => {
            const cachedPage = await caches.match(event.request);
            if (cachedPage) return cachedPage;

            const cachedRoot = await caches.match("/");
            if (cachedRoot) return cachedRoot;

            return caches.match(OFFLINE_URL);
          })
      );
      return;
    }

    // Demais ativos estáticos (_framework, css, js, imagens)
    event.respondWith(
      fetch(event.request)
        .then(response => {
          if (response.ok) {
            const clone = response.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
          }
          return response;
        })
        .catch(async () => {
          const cached = await caches.match(event.request);
          if (cached) return cached;
          return new Response("Offline asset unavailable", { status: 503, statusText: "Service Unavailable" });
        })
    );
  });
  ```

### Tarefa 3.3: Atualizar `service-worker.published.js` para Produção
- **Arquivo:** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/service-worker.published.js`
- **Subtarefa 3.3.1:** Configurar `service-worker.published.js` para carregar `self.importScripts('./service-worker-assets.js');` quando disponível (em `dotnet publish`) e fazer o pré-cache dos ativos listados em `self.assetsManifest.assets` (filtrando extensões `.dll`, `.pdb`, `.wasm`, `.js`, `.css`, `.png`, `.html`, `.webmanifest`), mantendo a mesma proteção de nunca interceptar `/api/`.

---

## Fase 4: Unificação da Fila Offline no Cliente (`BreedingOpsApiClient` e `IOfflineSyncService`)

### Contexto do Problema
As páginas `BatchBreedingLog.razor` e `PregnancyCheckTasks.razor` já possuem toda a UI para modo offline, mas chamam métodos em `BreedingOpsApiClient.cs` que são *stubs* fixos:
- `EnqueueDiagnosisAsync` retorna `true` sem fazer nada (linhas 57-60).
- `EnqueueBreedingBatchAsync` retorna `true` sem fazer nada (linhas 68-71).
- `SyncPendingOpsAsync` retorna `0` (linhas 73-76).
- `IsOnlineAsync` retorna sempre `true` (linhas 78-81).
- `GetPendingOpsCountAsync` retorna sempre `0` (linhas 83-86).
- `RegisterBreedingBatchAsync` retorna um objeto em memória sem enviar requisição HTTP ou tratar erro (linhas 62-66).

Enquanto isso, `IOfflineSyncService` (`OfflineSyncService.cs`) já gerencia o estado real de `IsOnline`, `PendingCount`, `PendingOperations` e persistência no IndexedDB via `offlineSync.js`.

### Tarefa 4.1: Integrar `IOfflineSyncService` dentro de `BreedingOpsApiClient`
- **Arquivo:** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Services/BreedingOpsApiClient.cs`
- **Subtarefa 4.1.1:** Injetar `IOfflineSyncService` no construtor de `BreedingOpsApiClient`:
  ```csharp
  private readonly HttpClient _httpClient;
  private readonly IJSRuntime _jsRuntime;
  private readonly IOfflineSyncService _offlineSyncService;

  public BreedingOpsApiClient(
      HttpClient httpClient,
      IJSRuntime jsRuntime,
      IOfflineSyncService offlineSyncService)
  {
      _httpClient = httpClient;
      _jsRuntime = jsRuntime;
      _offlineSyncService = offlineSyncService;
  }
  ```
- **Subtarefa 4.1.2:** Conectar `IsOnlineAsync()` e `GetPendingOpsCountAsync()` ao `_offlineSyncService`:
  ```csharp
  public async Task<bool> IsOnlineAsync()
  {
      await _offlineSyncService.InitializeAsync();
      return _offlineSyncService.IsOnline;
  }

  public async Task<int> GetPendingOpsCountAsync()
  {
      await _offlineSyncService.InitializeAsync();
      return _offlineSyncService.PendingCount;
  }
  ```
- **Subtarefa 4.1.3:** Conectar `EnqueueDiagnosisAsync` e `EnqueueBreedingBatchAsync` ao `_offlineSyncService.EnqueueOperationAsync`:
  ```csharp
  public async Task<bool> EnqueueDiagnosisAsync(RegisterPregnancyDiagnosisRequest request)
  {
      await _offlineSyncService.InitializeAsync();
      var result = await _offlineSyncService.EnqueueOperationAsync("Breeding", "RegisterDiagnosis", request);
      return result.IsSuccess;
  }

  public async Task<bool> EnqueueBreedingBatchAsync(RegisterBreedingBatchRequest request)
  {
      await _offlineSyncService.InitializeAsync();
      var result = await _offlineSyncService.EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request);
      return result.IsSuccess;
  }
  ```
- **Subtarefa 4.1.4:** Implementar `SyncPendingOpsAsync` delegando para `_offlineSyncService.ForceSyncAsync()`:
  ```csharp
  public async Task<int> SyncPendingOpsAsync(Func<string, string, Task<bool>>? handler = null)
  {
      await _offlineSyncService.InitializeAsync();
      if (!_offlineSyncService.IsOnline || _offlineSyncService.PendingCount == 0)
          return 0;

      var beforeCount = _offlineSyncService.PendingCount;
      await _offlineSyncService.ForceSyncAsync();
      var afterCount = _offlineSyncService.PendingCount;
      return Math.Max(0, beforeCount - afterCount);
  }
  ```
- **Subtarefa 4.1.5:** Em `RegisterDiagnosisAsync` e `RegisterBreedingBatchAsync`, capturar `HttpRequestException` (queda abrupta de sinal durante o clique no curral) para atualizar `_offlineSyncService.SetNetworkStatus(false)` e enfileirar automaticamente a operação no IndexedDB.

### Tarefa 4.2: Assinar `SyncService.OnStateChanged` nas páginas de campo
- **Arquivos:**
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/BatchBreedingLog.razor`
  - `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/PregnancyCheckTasks.razor`
- **Subtarefa 4.2.1:** Injetar `@inject IOfflineSyncService SyncService` e implementar `@implements IDisposable` em ambas as páginas.
- **Subtarefa 4.2.2:** Em `OnInitializedAsync()`, assinar `SyncService.OnStateChanged += HandleSyncStateChanged;` para atualizar `_isOnline` e `_pendingCount` em tempo real quando o sinal de Wi-Fi/4G cair ou voltar no curral, e remover a assinatura no `Dispose()`.

---

## Fase 5: Implementação dos Endpoints de Sincronização no Backend (`CriaCerto.Api`)

### Contexto do Problema
O serviço `OfflineSyncService.cs` (linhas 118 e 184) faz chamadas para:
- `POST api/v1/sync/{ModuleName}/{ActionType}`
- `POST api/v1/sync/{ModuleName}/{ActionType}?force=true`
Enviando `op.PayloadJson` como corpo da requisição. Atualmente esse grupo de rotas **não existe** em `src/Host/CriaCerto.Api/Program.cs`, resultando em `404 Not Found` toda vez que o usuário clica em `"Sincronizar Agora"` no `SyncStatusHeader.razor` ou quando a conexão é restabelecida.

### Tarefa 5.1: Corrigir o envio do Payload JSON em `OfflineSyncService.cs`
- **Arquivo:** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/Services/OfflineSyncService.cs`
- **Detalhe Técnico Importante:** Nas linhas 118 e 184, `PostAsJsonAsync(..., op.PayloadJson)` serializa uma `string` que já está em formato JSON, gerando uma string JSON duplamente escapada (`"\"{\\\"CowTag\\\":...}\""`).
- **Subtarefa 5.1.1:** Em `OfflineSyncService.cs` (linhas 118 e 184), enviar o `op.PayloadJson` com `StringContent(op.PayloadJson, Encoding.UTF8, "application/json")` via `_httpClient.PostAsync(...)` para que o JSON chegue íntegro ao endpoint da API (mantendo total compatibilidade com os testes em `OfflineSyncServiceTests.cs` que interceptam `SendAsync` no `HttpMessageHandler`).

### Tarefa 5.2: Mapear o grupo `/api/v1/sync` em `CriaCerto.Api`
- **Arquivo:** `src/Host/CriaCerto.Api/Program.cs`
- **Subtarefa 5.2.1:** Criar o grupo autenticado `/api/v1/sync`:
  ```csharp
  var syncGroup = app.MapGroup("/api/v1/sync")
      .RequireAuthorization()
      .WithTags("OfflineSync");
  ```
- **Subtarefa 5.2.2:** Mapear o endpoint `POST /api/v1/sync/{moduleName}/{actionType}` (com query param opcional `bool force = false`):
  - Suportar os módulos e ações enfileiradas pelo PWA:
    1. `Breeding` / `RegisterDiagnosis` (ou `RecordInsemination`): desserializar o JSON e despachar `RegisterPregnancyDiagnosisCommand` via `ISender` (ou processar o evento reprodutivo).
    2. `Breeding` / `RegisterBreedingBatch`: processar cada linha do lote (`RegisterBreedingBatchRequest`) atualizando/registrando o evento nas matrizes do tenant autenticado.
    3. `Growth` / `RecordWeighing`: desserializar para `RecordWeighingCommand` e enviar via `ISender`.
    4. `Sanitary` / `ApplyVaccine` ou `ApplyTreatment`: desserializar para `ApplyTreatmentCommand` e enviar via `ISender`.
  - Caso o resultado de domínio indique conflito de estado e `force == false`, retornar `Results.Conflict(new { Error = result.Error.Code, Message = result.Error.Message })` (status HTTP `409 Conflict`), permitindo que o `ConflictResolutionModal.razor` exiba a divergência para mediação do usuário.
  - Caso a combinação `moduleName`/`actionType` não seja reconhecida, retornar `Results.BadRequest(new { Error = "Sync.UnsupportedAction", Message = $"Ação '{moduleName}/{actionType}' não suportada." })`.

---

## Fase 6: Testes Automatizados e Validação Final

### Tarefa 6.1: Atualizar e Expandir Testes Unitários em `CriaCerto.Web.Client.UnitTests`
- **Arquivo:** `tests/Unit/CriaCerto.Web.Client.UnitTests/Services/OfflineSyncServiceTests.cs`
- **Subtarefa 6.1.1:** Garantir que todos os 6 testes existentes em `OfflineSyncServiceTests.cs` continuem passando.
- **Subtarefa 6.1.2:** Adicionar teste unitário para `ResolveConflictAsync` com `ConflictResolutionOption.UseLocal` verificando que a requisição é enviada com `?force=true` e que o conflito é removido da lista `ActiveConflicts` após retorno `200 OK`.
- **Subtarefa 6.1.3:** Adicionar testes unitários para `BreedingOpsApiClient` validando que `EnqueueDiagnosisAsync`, `EnqueueBreedingBatchAsync`, `IsOnlineAsync` e `GetPendingOpsCountAsync` delegam corretamente para `IOfflineSyncService`.

### Tarefa 6.2: Checklist de Validação de Build e Testes (Comandos Obrigatórios)
O agente LLM deve executar os seguintes comandos e garantir saída sem erros (`exit code 0`):

1. **Verificar existência e dimensões dos ícones PWA:**
   ```bash
   file src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-192.png src/Web/CriaCerto.Web/CriaCerto.Web.Client/wwwroot/icon-512.png
   ```
   *(Saída esperada: `192 x 192` e `512 x 512` PNG image data).*

2. **Compilar toda a solução Web e API:**
   ```bash
   dotnet build src/Web/CriaCerto.Web/CriaCerto.Web/CriaCerto.Web.csproj
   dotnet build src/Host/CriaCerto.Api/CriaCerto.Api.csproj
   ```

3. **Executar os testes unitários do cliente Web:**
   ```bash
   dotnet test tests/Unit/CriaCerto.Web.Client.UnitTests/CriaCerto.Web.Client.UnitTests.csproj
   ```

---

## Critérios de Aceite (Definition of Done)

- [x] `icon-192.png` (`192x192`) e `icon-512.png` (`512x512`) existem em `wwwroot` e são servidos com HTTP `200`.
- [x] `manifest.webmanifest` está em `pt-BR`, com `id`, `scope`, `purpose: "any maskable"` e descrição condizente com pecuária bovina.
- [x] `App.razor` possui as meta tags `apple-touch-icon` e `apple-mobile-web-app-capable`.
- [x] `offline.html` está em `pt-BR` com botão de reconexão e listener automático do evento `online`.
- [x] `service-worker.js` e `service-worker.published.js` não interceptam rotas `/api/*` nem `/health`, ativam `skipWaiting()` + `clients.claim()`, e fazem fallback de navegação para o App Shell ou `offline.html`.
- [x] `BreedingOpsApiClient` delega fila e status de conexão para `IOfflineSyncService` (sem métodos *stub* fixos).
- [x] `CriaCerto.Api` responde ao endpoint `POST /api/v1/sync/{moduleName}/{actionType}` (incluindo suporte a `?force=true`).
- [x] Todos os testes em `CriaCerto.Web.Client.UnitTests` compilam e passam.

