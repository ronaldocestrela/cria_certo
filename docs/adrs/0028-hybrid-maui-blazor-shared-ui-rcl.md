# ADR 0028: Extração da Razor Class Library Compartilhada (CriaCerto.Shared.UI) para Arquitetura Híbrida Web e .NET MAUI

## Status
Implementado na Fase 1 (Implemented in Phase 1)

## Contexto
O frontend do CriaCerto foi originalmente concebido como uma aplicação Blazor Web App com cliente WebAssembly (`CriaCerto.Web.Client`) e recursos PWA para operação offline no curral e no pasto.

Com a expansão da plataforma para suporte mobile nativo em dispositivos móveis Android e iOS através do .NET MAUI Blazor Hybrid (`BlazorWebView`), identificou-se o acoplamento da camada de interface ao SDK do cliente WebAssembly (`Microsoft.NET.Sdk.BlazorWebAssembly`). Esse acoplamento impede que o projeto .NET MAUI compartilhe diretamente as páginas Razor, componentes atômicos, layouts, rotas, modelos e serviços HTTP sem duplicação de código.

Para viabilizar a reutilização de 100% dos componentes de interface entre a Web (WASM/Server) e os aplicativos móveis nativos (Android e iOS), é necessário desacoplar o núcleo de apresentação em uma **Razor Class Library (RCL)** padronizada em .NET 10 (`Microsoft.NET.Sdk.Razor`).

## Decisão

1. **Criação da Razor Class Library `CriaCerto.Shared.UI` (`src/Web/CriaCerto.Shared.UI/CriaCerto.Shared.UI.csproj`):**
   - Utilização do SDK `Microsoft.NET.Sdk.Razor` com target framework unificado `net10.0`.
   - Inclusão dos pacotes de abstração de componentes Web do ASP.NET Core: `Microsoft.AspNetCore.Components.Web (10.0.10)` e `Microsoft.AspNetCore.Components.Authorization (10.0.0)`.
   - Configuração de visibilidade de tipos internos para a suíte de testes unitários existente via `<InternalsVisibleTo Include="CriaCerto.Web.Client.UnitTests" />`.
   - Vínculo direto às camadas de aplicação dos módulos de domínio (`CriaCerto.Modules.*.Application`) e blocos fundamentais (`CriaCerto.BuildingBlocks.Abstractions`), preservando o isolamento de módulos e o padrão Clean Architecture & DDD.

2. **Registro e Governança de Solução:**
   - Inclusão de `CriaCerto.Shared.UI.csproj` no descritor de solução principal `CriaCerto.slnx` sob o diretório virtual `/src/Web/`.
   - Alinhamento da arquitetura em fases conforme definido no [hybrid_maui_roadmap.md](../../hybrid_maui_roadmap.md).

3. **Estratégia de Migração em Fases (Roadmap Híbrido):**
   - **Fase 1 (Estrutura e RCL - Concluída):** Criação da RCL (Tarefa 1.1), migração atômica de todo o código Razor (`Pages/`, `Components/`, `Layout/`, `Auth/`, `Models/`, `Services/`, `Routes.razor`, `_Imports.razor`) e assets estáticos compartilhados (`wwwroot/js`, `wwwroot/app.css`, `wwwroot/lib`, `wwwroot/imgs`) para `src/Web/CriaCerto.Shared.UI` (Tarefa 1.2), refatoração limpa para namespaces `CriaCerto.Shared.UI.*`, reconfiguração de `CriaCerto.Web.Client`, `CriaCerto.Web/Components/App.razor` (usando caminhos `_content/CriaCerto.Shared.UI/...`) e suíte de 170 testes unitários integrados (Tarefa 1.3).
   - **Fase 2 (Abstrações de Plataforma):** Isolamento de dependências de navegador (`localStorage`, `IndexedDB`, downloads e detecção de conectividade) atrás de interfaces desacopladas (`ITokenStorageService`, `IConnectivityService`, `IFileExportService`).
   - **Fases 3 a 6 (Casca .NET MAUI, Android e iOS):** Criação do projeto mobile nativo com `BlazorWebView`, suporte a SQLite/SecureStorage nativo e esteira CI/CD.

4. **Homologação e Validação Técnica:**
   - Compilação limpa em `CriaCerto.slnx` com 0 warnings e 0 erros.
   - 100% de aprovação nos 170 testes unitários de `CriaCerto.Web.Client.UnitTests` e 105 testes de `CriaCerto.Architecture.IntegrationTests`.
   - Preservação da integridade do PWA existente com `manifest.webmanifest`, `service-worker.js` e `service-worker.published.js` mantidos em `CriaCerto.Web.Client/wwwroot`.

## Consequências

### Positivas
- **Base de Código de UI Única (DRY):** Componentes Razor, formulários de manejo (Pesagem, Partos, IATF, Vacinação), dashboards e tabelas de dados passam a ser mantidos em um único projeto compartilhado.
- **Independência de Host:** A interface torna-se agnóstica em relação ao ambiente de execução (Blazor WebAssembly no browser ou BlazorWebView dentro de aplicativo nativo Android/iOS).
- **Sem Risco de Regressão Imediata:** A criação isolada do `.csproj` da RCL na Tarefa 1.1 permite validação e compilação limpas antes da movimentação física dos arquivos de código.

### Mitigações e Desafios
- **Caminhos de Recursos Estáticos (`_content/CriaCerto.Shared.UI/`):** A movimentação de arquivos CSS e JS para a RCL exige atualização precisa nos scripts e links do host `App.razor` e no shell nativo do MAUI.
- **Divergências de Plataforma no Runtime:** Chamadas diretas a `localStorage` ou `OperatingSystem.IsBrowser()` serão refatoradas na Fase 2 para serviços injetáveis por inversão de controle (IoC).

