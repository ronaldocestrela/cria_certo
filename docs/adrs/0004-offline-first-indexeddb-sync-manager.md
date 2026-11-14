# ADR 0004: Gerenciador de Sincronização Off-line PWA com IndexedDB e Mediação de Conflitos

* **Status:** Aceito
* **Data:** 2026-07-25
* **Contexto:** Sub-fase 4.1 do Roadmap Go-Live

## Contexto & Problema
O aplicativo CriaCerto opera predominantemente em ambientes rurais (mangueiro, curral e pastos), onde a conectividade com a internet costuma ser instável ou inexistente. As operações cotidianas de manejo zootécnico (pesagens, vacinações, tratamentos, IATF e partos) precisam ser registradas instantaneamente sem bloquear o operador e sincronizadas automaticamente quando o dispositivo retornar ao estado online.

## Decisões Tomadas
1. **Engine Local IndexedDB via JS Interop:**
   - Criou-se o módulo `offlineSync.js` que interage diretamente com a API nativa `IndexedDB` do navegador (`dbName: CriaCertoDb`, `store: offlineQueue`).
   - Registrou-se listeners de eventos de rede (`window.addEventListener('online')` e `offline`) para notificar o Blazor WASM via `JSInvokable`.

2. **Gerenciador de Estado Reativo (`OfflineSyncService`):**
   - Implementado serviço C# `OfflineSyncService` que mantém a fila de `SyncOperation` e a lista de `SyncConflictItem`.
   - Adota o **Result Pattern** (`Result<T>`) para comunicação com o backend sem lançar exceções.

3. **Componentização Visual (MCP Stitch Pattern):**
   - Componente global `SyncStatusHeader.razor` integrado ao `MainLayout.razor` exibindo badge reativo de estado (`Online`/`Offline`), contador de pendências e botão de forçar sincronização.
   - Componente `ConflictResolutionModal.razor` para exibição e comparação lado a lado de dados locais vs servidor quando a API responder com `409 Conflict` ou `Result.Failure(Error.Conflict)`.

4. **Estratégia de Mediação de Conflitos e Endpoint de Sincronização (`/api/v1/sync`):**
   - O operador/zootecnista tem opção de escolher entre:
     - **Manter Versão do Campo (UseLocal):** Envia flag de sobrescrita (`?force=true`) para forçar atualização no servidor.
     - **Manter Versão do Servidor (UseServer):** Descarta a transação local pendente.
   - O `OfflineSyncService` transmite o `PayloadJson` íntegro via `StringContent(..., Encoding.UTF8, "application/json")` para `POST /api/v1/sync/{moduleName}/{actionType}`.
   - No backend (`CriaCerto.Api`), o grupo `/api/v1/sync` é modularizado via `OfflineSyncEndpoints.cs` e orquestra comandos CQRS via `ISender` (`RegisterPregnancyDiagnosisCommand`, `RecordInseminationCommand`, `RegisterBreedingBatchCommand`, `RecordWeighingCommand`, `ApplyTreatmentCommand`) respeitando o isolamento modular e retornando HTTP `409 Conflict` quando `result.Error.Type == ErrorType.Conflict` e `force == false`. Homologado por `OfflineSyncEndpointsIntegrationTests`.

5. **Service Worker (`criacerto-shell-v2`) e Isolamento de Rotas de API:**
   - Configurado `<ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>` em `CriaCerto.Web.Client.csproj` com `service-worker.js` e `service-worker.published.js`.
   - Ativação imediata via `self.skipWaiting()` e `self.clients.claim()`.
   - Bypass estrito de requisições `/api/*`, `/health` e origens externas no listener `fetch`, garantindo que quedas de rede em chamadas de API lancem `HttpRequestException` limpa no cliente C# (acionando o fallback automático do `BreedingOpsApiClient` / `IOfflineSyncService`) sem retornar HTML (`offline.html`) no lugar de JSON.

## Consequências
* **Positivas:** Operação 100% resiliente em campo, instalabilidade PWA completa (manifesto `pt-BR`, ícones `192x192` e `512x512`, meta tags iOS/Android e fallback `offline.html` em português), feedback visual em tempo real nas telas de manejo (`BatchBreedingLog` e `PregnancyCheckTasks`), eliminação de perda de dados e resolução graciosa de conflitos.
* **Mitigações:** Suíte de testes unitários expandida em `CriaCerto.Web.Client.UnitTests` (`OfflineSyncServiceTests` e `BreedingOpsApiClientTests`) validando enfileiramento, envio de JSON íntegro, resolução `UseLocal` (`?force=true`) e fallback automático em quedas de rede.
