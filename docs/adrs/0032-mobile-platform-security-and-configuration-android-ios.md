# ADR 0032: Configurações Específicas de Plataforma Móvel e Segurança (Android & iOS)

## Status
Implementado na Fase 5 (Implemented in Phase 5 - Tarefas 5.1 e 5.2)

## Contexto
Após a criação do projeto hospedeiro .NET MAUI `CriaCerto.Mobile` ([ADR 0030](0030-hybrid-maui-blazor-mobile-host-project.md)) e a implementação dos serviços nativos de plataforma ([ADR 0031](0031-hybrid-maui-blazor-native-platform-services.md)), era necessário consolidar as configurações específicas de segurança e publicação exigidas pelos sistemas operacionais Android e iOS.

No ecossistema de dispositivos móveis em campo:
- **Android:** O sistema exige declaração explícita de permissões de conectividade (`ACCESS_NETWORK_STATE` e `INTERNET`) para o monitoramento de rede e sincronização offline. Além disso, a partir do Android 9 (API 28), o tráfego HTTP em texto claro (*cleartext*) é bloqueado por padrão, demandando uma política de segurança de rede (`network_security_config.xml`) que permita loopback local durante testes e desenvolvimento sem relaxar o HTTPS obrigatório em produção. Por fim, a atividade principal (`MainActivity.cs`) precisa tratar mudanças de configuração do dispositivo (orientação, tamanho de tela, modo noturno/UiMode, densidade) para evitar reinicializações e perda do estado de interface do WebView.
- **iOS:** A plataforma Apple impõe governança restrita de App Transport Security (ATS), bloqueando qualquer conexão HTTP a menos que explicitamente configurada. Para permitir testes e depuração contra APIs em loopback/rede local (`http://localhost:5274`) no simulador iOS, a diretiva `NSAllowsLocalNetworking` deve ser habilitada mantendo HTTPS obrigatório para todos os domínios externos. No âmbito da App Store, a chave `ITSAppUsesNonExemptEncryption` com valor `false` dispensa conformidade burocrática de exportação de criptografia. Por fim, para que o `SecureStorage.Default` persista credenciais com segurança no Apple Keychain em builds assinados, o arquivo `Entitlements.plist` deve declarar o grupo de acesso canônico (`keychain-access-groups`).
- **Living Documentation & Ambientes Linux:** Como o desenvolvimento primário e os contêineres locais rodam em Linux (onde ferramentas do Xcode não estão disponíveis), as validações dessas configurações não podem depender de ferramentas exclusivas de macOS, exigindo suítes automatizadas de testes unitários xUnit baseadas em inspeção estrutural de XML/MSBuild.

## Decisão

1. **Configuração de Plataforma Android (`Platforms/Android/`):**
   - **Permissões em `AndroidManifest.xml`:** Declaradas as permissões `android.permission.ACCESS_NETWORK_STATE` e `android.permission.INTERNET`.
   - **Governança de Rede em `network_security_config.xml`:** Criado o descritor de segurança liberando cleartext restrito a `10.0.2.2`, `localhost` e `127.0.0.1` sob `<domain-config>`, e impondo `<base-config cleartextTrafficPermitted="false">` com autoridades certificadoras do sistema (`<certificates src="system" />`). Referenciado em `application` via `android:networkSecurityConfig="@xml/network_security_config"`.
   - **Ciclo de Vida em `MainActivity.cs`:** Anotada a atividade com `ConfigurationChanges` abrangendo `ScreenSize | Orientation | UiMode | ScreenLayout | SmallestScreenSize | Density`.
   - **Living Documentation:** Cobertura de testes automatizados em `AndroidConfigurationTests.cs`.

2. **Configuração de Plataforma iOS (`Platforms/iOS/`):**
   - **Metadados em `Info.plist`:**
     - `CFBundleDisplayName`: `CriaCerto`.
     - `CFBundleIdentifier`: `br.com.criacerto.app`.
     - `UILaunchStoryboardName`: `MauiSplash` (gerado a partir do recurso vetorial `MauiSplashScreen`).
     - `ITSAppUsesNonExemptEncryption`: `<false/>`.
   - **App Transport Security (ATS) em `Info.plist`:**
     - Inserido dicionário `NSAppTransportSecurity` contendo `NSAllowsLocalNetworking` = `<true/>`, assegurando testes locais em simulador sem comprometer HTTPS em produção (`NSAllowsArbitraryLoads` permanece desativado/ausente).
   - **Entitlements e Keychain em `Entitlements.plist`:**
     - Criado o arquivo `Platforms/iOS/Entitlements.plist` declarando `keychain-access-groups` com o identificador canônico `$(AppIdentifierPrefix)br.com.criacerto.app`.
   - **Vínculo MSBuild no `CriaCerto.Mobile.csproj`:**
     - Adicionada propriedade condicional:
       ```xml
       <PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
           <CodesignEntitlements>Platforms\iOS\Entitlements.plist</CodesignEntitlements>
       </PropertyGroup>
       ```
   - **Living Documentation:** Cobertura de testes automatizados em `IosConfigurationTests.cs`.

## Consequências

### Positivas
- **Conformidade com Lojas Oficiais:** Aplicativo configurado com os metadados, telas de abertura e declarações de criptografia exigidos pela Google Play Store e Apple App Store Connect.
- **Segurança de Transporte Blindada:** HTTPS obrigatório em produção em ambas as plataformas (Android Network Security Config e iOS ATS), com permissão cirúrgica de HTTP apenas para loopbacks/emuladores locais de desenvolvimento.
- **Integridade Criptográfica do Keychain/KeyStore:** O `SecureStorage.Default` opera com garantia de acesso ao Keychain em builds iOS assinados e suporte resiliente ao KeyStore no Android.
- **Living Documentation 100% Automatizada no Linux:** Ambas as configurações de plataforma são continuamente validadas por testes de regressão executáveis via `dotnet test` no Linux (`AndroidConfigurationTests.cs` e `IosConfigurationTests.cs`), prevenindo documentation drift e alterações acidentais nos manifestos.

