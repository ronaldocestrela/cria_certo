# ADR 0031: Implementações Nativas MAUI de Plataforma (SecureStorage, SQLite, Share e Conectividade)

## Status
Implementado na Fase 4 (Implemented in Phase 4)

## Contexto
Após a extração da biblioteca de componentes `CriaCerto.Shared.UI` ([ADR 0028](0028-hybrid-maui-blazor-shared-ui-rcl.md)), a definição das abstrações de plataforma desacopladas ([ADR 0029](0029-platform-abstractions-for-hybrid-maui-and-web.md)) e a criação do projeto hospedeiro .NET MAUI `CriaCerto.Mobile` ([ADR 0030](0030-hybrid-maui-blazor-mobile-host-project.md)), era necessário substituir os stubs provisórios em memória pelas implementações móveis nativas definitivas.

No ambiente operacional do aplicativo CriaCerto em campo (fazendas, currais e pastos remotos):
- A persistência de tokens JWT não pode ser vulnerável a expurgos de cache de WebView e deve aproveitar os cofres de segurança de hardware (Android KeyStore e iOS Keychain).
- A exportação de relatórios pecuários (balancetes de pesagem, comprovantes de vacinação, histórico IATF) precisa acionar a folha de compartilhamento nativa do sistema operacional (WhatsApp, e-mail, Bluetooth, impressoras térmicas).
- A conectividade celular e Wi-Fi oscila constantemente, exigindo detecção reativa instantânea de transições online/offline.
- As operações de manejo registradas no mangueiro sem conexão devem ser gravadas em um banco de dados transacional durável e ACID (SQLite) local, garantindo que não se percam caso o aplicativo seja encerrado pelo usuário ou pelo sistema operacional.

## Decisão

1. **`MauiTokenStorageService` via `SecureStorage` com Fallback Resiliente:**
   - Utilização de `Microsoft.Maui.Storage.SecureStorage.Default` para persistência encriptada do token JWT (`authToken`).
   - Tratamento defensivo de exceções de hardware (ex.: simuladores sem bloqueio de tela, emuladores sem KeyStore inicializado) com fallback em memória transparente e logging estruturado (`ILogger`), assegurando que o app nunca trave no fluxo de autenticação.

2. **`MauiFileExportService` via `Share` Nativo e `FileSystem.CacheDirectory`:**
   - Gravação dos bytes em `Path.Combine(FileSystem.CacheDirectory, fileName)`.
   - Disparo da folha de compartilhamento do sistema operacional móvel através de `Share.Default.RequestAsync(new ShareFileRequest { Title = fileName, File = new ShareFile(filePath, contentType) })`.

3. **`MauiConnectivityService` via `Connectivity.Current`:**
   - Monitoramento direto do estado da rede via `Connectivity.Current.NetworkAccess == NetworkAccess.Internet`.
   - Assinatura assíncrona ao evento `ConnectivityChanged` para notificar componentes Blazor em tempo real, implementando `IDisposable` para evitar vazamentos de memória.

4. **`MauiOfflineSyncStorage` via SQLite Durável (`sqlite-net-pcl`):**
   - Adicionados os pacotes `sqlite-net-pcl` e `SQLitePCLRaw.bundle_green` ao `CriaCerto.Mobile.csproj`.
   - Criada a entidade `OfflineSyncOperationEntity` mapeando a tabela `OfflineSyncOperations` com índices em `ModuleName` e `CreatedAtUtc`.
   - Implementado `MauiOfflineSyncStorage` gerenciando o arquivo local `criacerto_offline.db3` em `FileSystem.AppDataDirectory` com operações assíncronas transacionais (`InsertOrReplaceAsync`, `DeleteAsync`, `Table<T>().OrderBy()`).

5. **Registro de Injeção de Dependências em `MauiProgram.cs`:**
   - Expurgado o arquivo temporário `MobileAbstractionsStubs.cs`.
   - Registradas as implementações nativas com escopo (`AddScoped`) em `MauiProgram.cs`.

## Consequências

### Positivas
- **Operação de Campo 100% Offline First:** Produtores e capatazes podem registrar pesagens, movimentação de lotes e vacinações sem sinal; os registros persistem no SQLite e sincronizam automaticamente ao restaurar conectividade.
- **Segurança de Nível Bancário:** Chaves de autenticação protegidas por criptografia de hardware no KeyStore / Keychain.
- **Zero Regressão na Aplicação Web:** As implementações de browser continuam isoladas em `CriaCerto.Web.Client.Services.Browser`, mantendo a suíte de testes 100% verde (185 testes passando).
- **Compartilhamento Flexível de Arquivos:** Relatórios podem ser enviados diretamente via mensageiros ou armazenados no Google Drive/iCloud através do Share nativo.

