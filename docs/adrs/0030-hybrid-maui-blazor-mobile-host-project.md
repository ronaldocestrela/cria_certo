# ADR 0030: Criação do Host Mobile Nativo .NET MAUI Blazor Hybrid (`CriaCerto.Mobile`)

## Status
Implementado na Fase 3 (Implemented in Phase 3)

## Contexto
Após a extração da biblioteca de componentes e rotas para a Razor Class Library (`CriaCerto.Shared.UI`, [ADR 0028](0028-hybrid-maui-blazor-shared-ui-rcl.md)) e a criação das abstrações de plataforma desacopladas ([ADR 0029](0029-platform-abstractions-for-hybrid-maui-and-web.md)), fazia-se necessária a criação da casca móvel nativa para as plataformas Android e iOS.

No ecossistema corporativo da plataforma pecuária CriaCerto:
- O desenvolvimento primário e os ambientes de compilação em contêineres de desenvolvimento utilizam sistemas operacionais baseados em Linux (Ubuntu), onde o SDK do .NET 10 (`net10.0-android`) e as ferramentas do Android SDK (`/usr/lib/android-sdk`) estão disponíveis localmente.
- O destino iOS (`net10.0-ios`), entretanto, requer ferramentas privativas da Apple (Xcode e macOS), inviabilizando compilações locais em hosts Linux sem condicionamento de alvos no MSBuild.
- Para evitar duplicação de código de injeção de dependências e regras de interface entre a aplicação Blazor WebAssembly (`CriaCerto.Web.Client`) e o novo aplicativo móvel (`CriaCerto.Mobile`), os clientes de API e serviços de autenticação precisavam de uma convenção centralizada de registro.

## Decisão

1. **Criação do Projeto `CriaCerto.Mobile.csproj` (.NET MAUI Single Project):**
   - Utilização do SDK `Microsoft.NET.Sdk.Razor` com as propriedades `<UseMaui>true</UseMaui>`, `<SingleProject>true</SingleProject>`, `<ImplicitUsings>enable</ImplicitUsings>` e `<Nullable>enable</Nullable>`.
   - Condicionamento de plataformas-alvo para compatibilidade multiplataforma contínua:
     ```xml
     <TargetFrameworks>net10.0-android</TargetFrameworks>
     <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx')) or $([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-ios</TargetFrameworks>
     ```
   - Definição dos metadados oficiais:
     - `ApplicationTitle`: `CriaCerto`
     - `ApplicationId`: `br.com.criacerto.app`
     - `ApplicationDisplayVersion`: `1.0.0`
     - `ApplicationVersion`: `1`
     - Versão mínima Android: 24.0 (Android 7.0 Nougat)
     - Versão mínima iOS: 15.0

2. **Reaproveitamento Integral de UI via `BlazorWebView`:**
   - Em `MainPage.xaml`, o componente `<BlazorWebView>` carrega `wwwroot/index.html` e injeta diretamente a raiz de navegação da RCL compartilhada:
     ```xml
     <BlazorWebView x:Name="blazorWebView" HostPage="wwwroot/index.html">
         <BlazorWebView.RootComponents>
             <RootComponent Selector="#app" ComponentType="{x:Type shared:Routes}" />
         </BlazorWebView.RootComponents>
     </BlazorWebView>
     ```
   - O projeto `CriaCerto.Mobile` não possui componentes `.razor` nem páginas duplicadas, atuando estritamente como casca nativa hospedeira.

3. **Centralização de Injeção de Dependências (`AddCriaCertoSharedServices`):**
   - Criação da extensão `CriaCertoSharedServiceCollectionExtensions` em `CriaCerto.Shared.UI.Services`, registrando todos os `*ApiClient` (`PlantelApiClient`, `BreedingOpsApiClient`, `GrowthApiClient`, etc.), autenticação (`CustomAuthStateProvider`) e serviços de toast/permissões.
   - Tanto `CriaCerto.Web.Client/Program.cs` quanto `CriaCerto.Mobile/MauiProgram.cs` utilizam a mesma extensão.

4. **Configuração de Conexão com a API Backend:**
   - O cliente móvel conecta-se diretamente à `CriaCerto.Api` sem passar pelo proxy YARP da aplicação Web.
   - Resolução dinâmica da URL base em `MauiProgram.cs`:
     - Emulação Android em `Debug`: `http://10.0.2.2:5274/` (redirecionamento do loopback do host).
     - Simulador iOS em `Debug`: `http://localhost:5274/`.
     - Ambiente de `Release`: `https://app.criacerto.com.br/`.

5. **Organização da Solução:**
   - Adicionada a pasta `/src/Mobile/` contendo `src/Mobile/CriaCerto.Mobile/CriaCerto.Mobile.csproj` na solução `CriaCerto.slnx`.

## Consequências

### Positivas
- **Build Android Verde no Linux:** Compilação completa e limpa para `net10.0-android` com 0 erros via `dotnet build`.
- **Zero Duplicação de Telas:** Telas de manejo (pesagem, vacinação, lotes, IATF, partos) desenvolvidas para a Web rodam de imediato dentro do aplicativo móvel.
- **Isolamento de Plataformas:** O condicionamento em MSBuild preserva a integridade do pipeline local em Linux e permite compilação de iOS em runners macOS no GitHub Actions (Fase 6).
- **Consistência de Dependências:** O método de extensão compartilhado previne discrepâncias no registro de clientes HTTP entre Web e Mobile.

### Próximos Passos (Fases 4, 5 e 6)
- [x] Substituídos os stubs iniciais de serviços pelas implementações móveis nativas completas ([ADR 0031](0031-hybrid-maui-blazor-native-platform-services.md)):
  - `MauiTokenStorageService` via `SecureStorage.Default`
  - `MauiFileExportService` via `Share.Default`
  - `MauiConnectivityService` via `Connectivity.Current`
  - `MauiOfflineSyncStorage` via SQLite (`sqlite-net-pcl`)
- [x] Adicionadas configurações de segurança e descritores de plataforma para Android e iOS ([ADR 0032](0032-mobile-platform-security-and-configuration-android-ios.md)):
  - Android: `network_security_config.xml`, `AndroidManifest.xml` e `MainActivity.cs`.
  - iOS: `Info.plist` (ATS local bypass e metadados de loja), `Entitlements.plist` (`keychain-access-groups`) e `<CodesignEntitlements>`.
- [ ] Pipeline CI/CD Multiplataforma (Fase 6: GitHub Actions com runner `macos-latest` para iOS e `ubuntu-latest` para Android).

