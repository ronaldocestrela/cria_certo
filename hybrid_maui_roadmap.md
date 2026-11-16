# Roadmap de Implementação: Aplicativo Híbrido .NET MAUI (Android & iOS) — `CriaCerto`

> **Objetivo para o Agente LLM:** Este documento descreve o plano de execução passo a passo para criar o aplicativo mobile híbrido **CriaCerto** usando **.NET MAUI Blazor Hybrid (`BlazorWebView`)** para **Android** e **iOS**, reaproveitando a interface Razor, contratos (`*.Application`) e clientes HTTP existentes em `CriaCerto.Web.Client`.
>
> **Regra de Execução:** Execute as Fases sequencialmente (`Fase 1` → `Fase 6`). Ao concluir cada fase, execute os comandos de validação indicados antes de avançar para a próxima. Não quebre o build nem os testes da aplicação Web/PWA existente.

---

## Visão Geral da Arquitetura Alvo

### Estado Atual
- **API Backend:** `src/Host/CriaCerto.Api/`
- **Host Web / Proxy YARP:** `src/Web/CriaCerto.Web/CriaCerto.Web/`
- **Cliente Blazor WASM (PWA):** `src/Web/CriaCerto.Web/CriaCerto.Web.Client/`
- **Testes Unitários do Cliente:** `tests/Unit/CriaCerto.Web.Client.UnitTests/`

### Estado Alvo
- **`src/Web/CriaCerto.Shared.UI/` (Nova Razor Class Library - RCL):** Contém toda a UI compartilhada (`Pages/`, `Components/`, `Layout/`), `Models/`, `Services/`, `Auth/`, `Routes.razor` e assets estáticos compartilhados (`app.css`, `lib/bootstrap/`, `imgs/`, `js/`).
- **`src/Web/CriaCerto.Web/CriaCerto.Web.Client/` (Blazor WASM):** Mantém apenas `Program.cs`, bootstrap WASM, `service-worker*.js`, `manifest.webmanifest`, `offline.html` e implementações Web das abstrações de plataforma.
- **`src/Mobile/CriaCerto.Mobile/` (Novo Projeto .NET MAUI Blazor Hybrid):** Casca nativa Android/iOS com `BlazorWebView`, `MauiProgram.cs`, `wwwroot/index.html`, implementações nativas (`SecureStorage`, `SQLite`, `Connectivity`, `Share`) e configurações em `Platforms/Android` e `Platforms/iOS`.

### Restrição de Ambiente (Linux vs. macOS)
- Este workspace está em **Ubuntu Linux** com **.NET 10 (`10.0.115`)** e workloads `android` e `maui-android` instalados.
- **Android** pode ser compilado e testado localmente no Linux.
- **iOS** exige **macOS + Xcode** (`maui-ios` não existe para Linux). Portanto, o `.csproj` do MAUI deve condicionar o target `net10.0-ios` para não quebrar o build no Linux, e o build/assinatura iOS deve ser validado via **GitHub Actions (`macos-latest`)** ou em um Mac.

---

## Fase 1: Extração da Razor Class Library Compartilhada (`CriaCerto.Shared.UI`)

### Contexto
Hoje todos os componentes Razor, páginas, serviços e `CustomAuthStateProvider` estão acoplados ao SDK `Microsoft.NET.Sdk.BlazorWebAssembly` em `CriaCerto.Web.Client`, enquanto `app.css`, `lib/bootstrap` e `imgs/` estão em `CriaCerto.Web/wwwroot`. Precisamos extrair o núcleo compartilhável para uma RCL (`Microsoft.NET.Sdk.Razor`).

### Tarefa 1.1: Criar o projeto `CriaCerto.Shared.UI` [CONCLUÍDO - ADR 0028]
- [x] **Arquivo criado:** `src/Web/CriaCerto.Shared.UI/CriaCerto.Shared.UI.csproj`
- [x] **Subtarefa 1.1.1:** Criar o `.csproj` com `Sdk="Microsoft.NET.Sdk.Razor"`, `<TargetFramework>net10.0</TargetFramework>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`.
- [x] **Subtarefa 1.1.2:** Adicionar `<PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.10" />` e `<PackageReference Include="Microsoft.AspNetCore.Components.Authorization" Version="10.0.0" />`.
- [x] **Subtarefa 1.1.3:** Copiar todas as `<ProjectReference>` de módulos (`CriaCerto.BuildingBlocks.Abstractions` e `CriaCerto.Modules.*.Application`) que hoje estão em `src/Web/CriaCerto.Web/CriaCerto.Web.Client/CriaCerto.Web.Client.csproj` (ajustando os caminhos relativos).
- [x] **Subtarefa 1.1.4:** Adicionar `<InternalsVisibleTo Include="CriaCerto.Web.Client.UnitTests" />` no `CriaCerto.Shared.UI.csproj`.
- [x] **Subtarefa 1.1.5:** Registrar `CriaCerto.Shared.UI.csproj` em `CriaCerto.slnx` e formalizar o ADR `docs/adrs/0028-hybrid-maui-blazor-shared-ui-rcl.md`.

### Tarefa 1.2: Migrar código, páginas e assets estáticos para `CriaCerto.Shared.UI` [CONCLUÍDO - FASE 1]
- [x] **Subtarefa 1.2.1:** Mover de `src/Web/CriaCerto.Web/CriaCerto.Web.Client/` para `src/Web/CriaCerto.Shared.UI/`:
  - `Auth/`
  - `Components/`
  - `Layout/`
  - `Models/`
  - `Pages/`
  - `Services/`
  - `Routes.razor`
  - `_Imports.razor`
- [x] **Subtarefa 1.2.2:** Mover os assets compartilhados para `src/Web/CriaCerto.Shared.UI/wwwroot/`:
  - De `CriaCerto.Web.Client/wwwroot/js/` (`downloadHelper.js`, `offlineSync.js`) → `CriaCerto.Shared.UI/wwwroot/js/`
  - De `CriaCerto.Web/wwwroot/` (`app.css`, `lib/`, `imgs/`) → `CriaCerto.Shared.UI/wwwroot/` (mantendo cópia de `imgs/CriaCerto.png` em `CriaCerto.Web/wwwroot/imgs/` para preservação de OpenGraph e rotas externas).
- [x] **Subtarefa 1.2.3:** Ajustar namespaces de forma limpa para `CriaCerto.Shared.UI.*` em todos os arquivos C# (`.cs`) e componentes Razor (`.razor`), atualizando `_Imports.razor` e todas as referências cruzadas sem artifícios transitórios.
- [x] **Subtarefa 1.2.4:** Garantir que `Routes.razor` aponte `AppAssembly="typeof(Routes).Assembly"` e utilize `CriaCerto.Shared.UI.Pages.RedirectToLogin`.

### Tarefa 1.3: Atualizar `CriaCerto.Web.Client`, `CriaCerto.Web`, testes e `CriaCerto.slnx` [CONCLUÍDO - FASE 1]
- [x] **Subtarefa 1.3.1:** Em `CriaCerto.Web.Client.csproj`, remover as `<ProjectReference>` dos módulos (agora transitivas via `CriaCerto.Shared.UI`) e adicionar `<ProjectReference Include="..\..\CriaCerto.Shared.UI\CriaCerto.Shared.UI.csproj" />`. Atualizar `Program.cs` de `CriaCerto.Web.Client` com usings da RCL e classe pública parcial para reflexão.
- [x] **Subtarefa 1.3.2:** Em `src/Web/CriaCerto.Web/CriaCerto.Web/Components/App.razor`, atualizar os caminhos estáticos que foram movidos para a RCL:
  - `_content/CriaCerto.Shared.UI/lib/bootstrap/dist/css/bootstrap.min.css`
  - `_content/CriaCerto.Shared.UI/app.css`
  - `_content/CriaCerto.Shared.UI/CriaCerto.Shared.UI.bundle.scp.css`
  - `_content/CriaCerto.Shared.UI/js/downloadHelper.js`
  - `_content/CriaCerto.Shared.UI/js/offlineSync.js`
  - Referenciar `<CriaCerto.Shared.UI.Routes @rendermode="@(new InteractiveWebAssemblyRenderMode(prerender: false))" />`.
  - Atualizar `Program.cs` e `Dockerfile` do Web Host para incluir a RCL e projetos de aplicação.
- [x] **Subtarefa 1.3.3:** Atualizar `tests/Unit/CriaCerto.Web.Client.UnitTests/CriaCerto.Web.Client.UnitTests.csproj` para referenciar também `CriaCerto.Shared.UI.csproj`, atualizando usings nos testes e caminhos em `PwaFieldPagesOfflineStateTests.cs`.
- [x] **Subtarefa 1.3.4:** Registrar `src/Web/CriaCerto.Shared.UI/CriaCerto.Shared.UI.csproj` em `CriaCerto.slnx` (concluído na Tarefa 1.1).

### Validação da Fase 1 [CONCLUÍDA COM SUCESSO]
```bash
XDG_RUNTIME_DIR=/tmp dotnet build CriaCerto.slnx
# Build succeeded: 0 Warning(s), 0 Error(s)

XDG_RUNTIME_DIR=/tmp dotnet test tests/Unit/CriaCerto.Web.Client.UnitTests/CriaCerto.Web.Client.UnitTests.csproj
# Passed! - Failed: 0, Passed: 170, Skipped: 0, Total: 170

XDG_RUNTIME_DIR=/tmp dotnet test CriaCerto.slnx
# Passed! - Todas as suítes da solução integradas com sucesso (105 arq, 541 tenancy, 384 backoffice, 213 referral, etc.)
```

---

## Fase 2: Abstrações de Plataforma (Auth, Storage, Downloads, Conectividade) [CONCLUÍDO - ADR 0029]

### Contexto
O código original possuía acoplamento direto com `IJSRuntime`, `localStorage`, `IndexedDB` e `OperatingSystem.IsBrowser()`. No MAUI Blazor Hybrid, `OperatingSystem.IsBrowser()` retorna `false` e APIs nativas são muito mais confiáveis do que `localStorage`/`IndexedDB` dentro de WebViews (especialmente no iOS). As abstrações foram implementadas e integradas por Inversão de Controle (IoC).

### Tarefa 2.1: Criar interfaces de plataforma em `CriaCerto.Shared.UI/Abstractions/` [CONCLUÍDO]
- [x] **Subtarefa 2.1.1:** Criado `ITokenStorageService` (`GetTokenAsync`, `SetTokenAsync`, `RemoveTokenAsync`).
- [x] **Subtarefa 2.1.2:** Criado `IFileExportService` (`ExportFileAsync(fileName, contentType, content)`).
- [x] **Subtarefa 2.1.3:** Criado `IConnectivityService` (`IsOnline`, `ConnectivityChanged`, `InitializeAsync`).
- [x] **Subtarefa 2.1.4:** Criado `IOfflineSyncStorage` (`InitializeAsync`, `GetPendingOperationsAsync`, `SavePendingOperationAsync`, `RemovePendingOperationAsync`, `ClearPendingOperationsAsync`).

### Tarefa 2.2: Refatorar serviços e componentes em `CriaCerto.Shared.UI` para usar as abstrações [CONCLUÍDO]
- [x] **Subtarefa 2.2.1 (`CustomAuthStateProvider.cs`):**
  - Substituído `IJSRuntime` por `ITokenStorageService`.
  - **Removida** a trava `if (!OperatingSystem.IsBrowser())`.
- [x] **Subtarefa 2.2.2 (`BackofficeApiClient.cs`, `OfflineSyncService.cs` e demais `*ApiClient.cs`):**
  - Injetado `ITokenStorageService` com suporte a `AttachTokenAsync()`, preservando sobrecarga retrocompatível com `IJSRuntime` via `JSRuntimeTokenStorageAdapter`.
- [x] **Subtarefa 2.2.3 (`OfflineSyncService.cs` e `MoveLotModal.razor`):**
  - Injetados `IConnectivityService`, `IOfflineSyncStorage`, `ITokenStorageService` e `HttpClient` no `OfflineSyncService` (eliminando acoplamento com `offlineSync.js` no Shared.UI).
  - Em `Components/Growth/MoveLotModal.razor`, substituída a gravação em `localStorage` por `IOfflineSyncService.EnqueueOperationAsync(...)`.
- [x] **Subtarefa 2.2.4 (Downloads/Relatórios):**
  - Substituídas todas as chamadas a `downloadFileFromByteArray` por `IFileExportService.ExportFileAsync(...)` em `ProfileManagement.razor`, `PayoutsManagement.razor`, `TenantsManagement.razor` e `ExportReportModal.razor`.

### Tarefa 2.3: Criar implementações Web (Browser) em `CriaCerto.Web.Client` [CONCLUÍDO]
- [x] **Subtarefa 2.3.1:** Implementado `BrowserTokenStorageService` (com guarda `if (!OperatingSystem.IsBrowser()) return null;`).
- [x] **Subtarefa 2.3.2:** Implementado `BrowserFileExportService` (chamando `downloadFileFromByteArray`).
- [x] **Subtarefa 2.3.3:** Implementados `BrowserConnectivityService` e `BrowserOfflineSyncStorage` (interagindo com `criaCertoOfflineSync` / `IndexedDB`).
- [x] **Subtarefa 2.3.4:** Registradas todas as implementações no `CriaCerto.Web.Client/Program.cs` e no host SSR `CriaCerto.Web/Program.cs`. Formalizado ADR `docs/adrs/0029-platform-abstractions-for-hybrid-maui-and-web.md`.

### Validação da Fase 2 [CONCLUÍDA COM SUCESSO]
```bash
XDG_RUNTIME_DIR=/tmp dotnet build CriaCerto.slnx
# Build succeeded: 0 Warning(s), 0 Error(s)

XDG_RUNTIME_DIR=/tmp dotnet test tests/Unit/CriaCerto.Web.Client.UnitTests/CriaCerto.Web.Client.UnitTests.csproj
# Passed! - Failed: 0, Passed: 182, Skipped: 0, Total: 182

XDG_RUNTIME_DIR=/tmp dotnet test CriaCerto.slnx
# Passed! - Toda a solução integrada com sucesso
```

---

## Fase 3: Criação do Projeto .NET MAUI Blazor Hybrid (`CriaCerto.Mobile`)

### Tarefa 3.1: Criar e configurar `CriaCerto.Mobile.csproj`
- **Arquivo a criar:** `src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj`
- **Subtarefa 3.1.1:** Criar o projeto usando `<Project Sdk="Microsoft.NET.Sdk.Razor">` com `<UseMaui>true</UseMaui>`, `<SingleProject>true</SingleProject>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`.
- **Subtarefa 3.1.2:** Configurar `TargetFrameworks` de forma compatível com Linux (local) e macOS (CI/Mac):
  ```xml
  <TargetFrameworks>net10.0-android</TargetFrameworks>
  <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx')) or $([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-ios</TargetFrameworks>
  ```
- **Subtarefa 3.1.3:** Definir metadados do app:
  - `<ApplicationTitle>CriaCerto</ApplicationTitle>`
  - `<ApplicationId>br.com.criacerto.app</ApplicationId>`
  - `<ApplicationDisplayVersion>1.0.0</ApplicationDisplayVersion>`
  - `<ApplicationVersion>1</ApplicationVersion>`
  - `<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">15.0</SupportedOSPlatformVersion>`
  - `<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">24.0</SupportedOSPlatformVersion>`
- **Subtarefa 3.1.4:** Adicionar pacotes necessários:
  - `Microsoft.Maui.Controls` (`$(MauiVersion)`)
  - `Microsoft.AspNetCore.Components.WebView.Maui` (`$(MauiVersion)`)
  - `Microsoft.Extensions.Logging.Debug` (em `Debug`)
  - Referência de projeto para `..\..\Web\CriaCerto.Shared.UI\CriaCerto.Shared.UI.csproj`.
- **Subtarefa 3.1.5:** Configurar `MauiIcon` e `MauiSplashScreen` copiando `CriaCerto.png` (`1536x1536`) para `Resources/AppIcon/appicon.png` e `Resources/Splash/splash.png` (cor de fundo `#1f5638` alinhada ao `theme_color` do app).
- **Subtarefa 3.1.6:** Adicionar `src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj` em `CriaCerto.slnx`.

### Tarefa 3.2: Criar estrutura inicial do MAUI Blazor Hybrid
- **Subtarefa 3.2.1:** Criar `App.xaml`, `App.xaml.cs`, `MainPage.xaml` (contendo `<BlazorWebView x:Name="blazorWebView" HostPage="wwwroot/index.html">` com `<RootComponent Selector="#app" ComponentType="{x:Type shared:Routes}" />`) e `MainPage.xaml.cs`.
- **Subtarefa 3.2.2:** Criar `src/Mobile/CriaCerto.Mobile/wwwroot/index.html` carregando:
  - `_content/CriaCerto.Shared.UI/lib/bootstrap/dist/css/bootstrap.min.css`
  - `_content/CriaCerto.Shared.UI/app.css`
  - `_content/CriaCerto.Shared.UI/CriaCerto.Shared.UI.bundle.scp.css` (se existir)
  - Fonte `Material Symbols Outlined` (ou cópia local em `wwwroot/fonts/` para funcionamento 100% offline no curral)
  - `<div id="app">Carregando CriaCerto...</div>`
  - `<script src="_framework/blazor.webview.js" autostart="false"></script>`
- **Subtarefa 3.2.3:** Criar `MauiProgram.cs`:
  - Chamar `builder.UseMauiApp<App>()` e `builder.Services.AddMauiBlazorWebView()`.
  - Em `#if DEBUG`, adicionar `builder.Services.AddBlazorWebViewDeveloperTools()` e `builder.Logging.AddDebug()`.
  - Registrar `AuthorizationCore`, `CascadingAuthenticationState`, `CustomAuthStateProvider` e todos os `*ApiClient` e serviços de `CriaCerto.Shared.UI` (idealmente via um método de extensão compartilhado `services.AddCriaCertoSharedServices()`).
  - Configurar `HttpClient` apontando diretamente para a URL da `CriaCerto.Api` (sem YARP):
    - `#if DEBUG` + Android: `http://10.0.2.2:<porta_api>` (ou variável de configuração em `appsettings.json` embutido como `MauiAsset`).
    - `#if DEBUG` + iOS Simulator: `http://localhost:<porta_api>`.
    - `Release`: URL HTTPS de produção da `CriaCerto.Api`.

### Validação da Fase 3
```bash
export ANDROID_HOME=/usr/lib/android-sdk
dotnet build src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj -f net10.0-android
```

---

## Fase 4: Implementações Nativas MAUI (SecureStorage, SQLite, Share, Conectividade)

### Tarefa 4.1: `MauiTokenStorageService`
- **Arquivo:** `src/Mobile/CriaCerto.Mobile/Services/MauiTokenStorageService.cs`
- **Subtarefa 4.1.1:** Implementar `ITokenStorageService` usando `SecureStorage.Default.GetAsync("authToken")`, `SetAsync("authToken", token)` e `Remove("authToken")`.
- **Subtarefa 4.1.2:** Tratar exceções de keystore/keychain (ex.: em simuladores sem entitlements) com fallback seguro e log.

### Tarefa 4.2: `MauiFileExportService`
- **Arquivo:** `src/Mobile/CriaCerto.Mobile/Services/MauiFileExportService.cs`
- **Subtarefa 4.2.1:** Gravar os bytes em `Path.Combine(FileSystem.CacheDirectory, fileName)` e abrir a folha nativa de compartilhamento usando `Share.Default.RequestAsync(new ShareFileRequest { Title = fileName, File = new ShareFile(filePath, contentType) })`.

### Tarefa 4.3: `MauiConnectivityService`
- **Arquivo:** `src/Mobile/CriaCerto.Mobile/Services/MauiConnectivityService.cs`
- **Subtarefa 4.3.1:** Implementar `IConnectivityService` usando `Connectivity.Current.NetworkAccess == NetworkAccess.Internet` e assinar `Connectivity.Current.ConnectivityChanged` para disparar `ConnectivityChanged?.Invoke(...)`.

### Tarefa 4.4: Persistência Offline Nativa com SQLite (`MauiOfflineSyncStorage`)
- **Subtarefa 4.4.1:** Adicionar o pacote `sqlite-net-pcl` (e `SQLitePCLRaw.bundle_green`) ao `CriaCerto.Mobile.csproj`.
- **Subtarefa 4.4.2:** Criar entidade local `OfflineSyncOperationEntity` mapeando `Id` (PK), `ModuleName`, `ActionType`, `PayloadJson`, `CreatedAtUtc`, `RetryCount`, `Status`.
- **Subtarefa 4.4.3:** Implementar `MauiOfflineSyncStorage` salvando o banco em `Path.Combine(FileSystem.AppDataDirectory, "criacerto_offline.db3")`.
- **Subtarefa 4.4.4:** Registrar `MauiTokenStorageService`, `MauiFileExportService`, `MauiConnectivityService` e `MauiOfflineSyncStorage` no `MauiProgram.cs`.

### Validação da Fase 4
```bash
export ANDROID_HOME=/usr/lib/android-sdk
dotnet build src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj -f net10.0-android
dotnet test CriaCerto.slnx
```

---

## Fase 5: Configurações Específicas de Android e iOS

### Tarefa 5.1: Configuração Android (`Platforms/Android/`)
- **Subtarefa 5.1.1:** Em `Platforms/Android/AndroidManifest.xml`, garantir permissões:
  - `<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />`
  - `<uses-permission android:name="android.permission.INTERNET" />`
- **Subtarefa 5.1.2:** Para testes locais em `Debug` contra API HTTP (`10.0.2.2`), criar `Platforms/Android/Resources/xml/network_security_config.xml` liberando cleartext apenas para `10.0.2.2` / `localhost` e referenciar no `AndroidManifest.xml` (em `Release`, exigir HTTPS).
- **Subtarefa 5.1.3:** Verificar `MainActivity.cs` com `ConfigurationChanges` adequados (`ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density`).

### Tarefa 5.2: Configuração iOS (`Platforms/iOS/`)
- **Subtarefa 5.2.1:** Em `Platforms/iOS/Info.plist`, configurar `CFBundleDisplayName` (`CriaCerto`), `UILaunchStoryboardName` e `ITSAppUsesNonExemptEncryption` (`false`, se aplicável).
- **Subtarefa 5.2.2:** Se for testar em `Debug` contra API HTTP local no simulador iOS, adicionar exceção `NSAppTransportSecurity` -> `NSAllowsLocalNetworking` = `true` (mantendo HTTPS obrigatório em produção).
- **Subtarefa 5.2.3:** Configurar `Platforms/iOS/Entitlements.plist` (Keychain Access Groups se necessário para `SecureStorage` em builds assinados).

### Tarefa 5.3: Verificação de Endpoint na `CriaCerto.Api`
- **Subtarefa 5.3.1:** Confirmar que todos os endpoints chamados pelos `*ApiClient` em `CriaCerto.Shared.UI` usam Bearer JWT no header `Authorization` (não dependem exclusivamente de cookies de mesma origem do `CriaCerto.Web`). Caso algum endpoint use cookie de sessão Web, adaptar para aceitar JWT Bearer.

### Validação da Fase 5
```bash
export ANDROID_HOME=/usr/lib/android-sdk
dotnet build src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj -f net10.0-android -c Release
```

---

## Fase 6: Pipeline CI/CD Multiplataforma (GitHub Actions para Android & iOS)

### Contexto
Como o desenvolvimento local ocorre em Linux, o build e empacotamento de **iOS** precisam rodar em um runner `macos-latest`.

### Tarefa 6.1: Criar workflow `.github/workflows/mobile-build.yml`
- **Subtarefa 6.1.1 (Job `android` em `ubuntu-latest`):**
  - Setup `.NET 10.0.x`
  - `dotnet workload install maui-android`
  - `dotnet build src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj -f net10.0-android -c Release`
  - (Opcional) `dotnet publish -f net10.0-android -c Release` para gerar `.aab` / `.apk` como artifact.
- **Subtarefa 6.1.2 (Job `ios` em `macos-latest`):**
  - Setup `.NET 10.0.x` e selecionar Xcode compatível com .NET 10 MAUI
  - `dotnet workload install maui-ios`
  - `dotnet build src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj -f net10.0-ios -c Release`
  - Documentar os secrets necessários para assinatura na App Store / TestFlight (`APPLE_CERTIFICATE_BASE64`, `APPLE_CERTIFICATE_PASSWORD`, `APPLE_PROVISIONING_PROFILE_BASE64`).

### Validação da Fase 6
- Validar sintaxe YAML do workflow e confirmar que todos os caminhos de projeto em `.github/workflows/mobile-build.yml` existem exatamente no repositório.

---

## Checklist Final de Aceite

- [ ] `CriaCerto.Web` + `CriaCerto.Web.Client` continuam compilando e passando todos os testes unitários sem regressão.
- [ ] `CriaCerto.Shared.UI` concentra páginas, componentes, serviços e DTOs sem referências diretas a `localStorage` ou `IndexedDB`.
- [ ] `CriaCerto.Mobile` compila em `net10.0-android` no Linux e inicia em emulador/dispositivo conectando diretamente na `CriaCerto.Api`.
- [ ] Login persiste token no `SecureStorage` e sobrevive à reinicialização do app.
- [ ] Operações offline no curral são gravadas no SQLite local e sincronizadas automaticamente quando `Connectivity.Current` detecta internet.
- [ ] Exportação de relatórios funciona via folha de compartilhamento nativa (`Share.Default`).
- [ ] Workflow CI valida build de `net10.0-android` e `net10.0-ios`.

