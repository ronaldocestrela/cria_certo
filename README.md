# CriaCerto

Plataforma SaaS modular para gestao de granjas, com foco atual em suinocultura, baseada em .NET 10, Blazor Web App (WASM) e arquitetura Modular Monolith.

## 1. Visao Geral

O CriaCerto foi estruturado para competir com solucoes legadas de gestao agro, com uma base preparada para:

- Operacoes de reproducao e manejo de matrizes
- Maternidade e acompanhamento de ninhadas
- Crescimento e terminacao por lotes
- Nutricao, sanidade e analytics zootecnico
- Evolucao para multi-tenant com feature gating por plano

Nesta fase inicial (Sub-phase 0.1), o foco esta na fundacao tecnica: arquitetura, shared kernel, pipelines transversais, PWA baseline e documentacao viva.

## 2. Stack Tecnologica

### Backend

- .NET 10 (C#)
- ASP.NET Core Web API
- MediatR
- FluentValidation
- Entity Framework Core 10
- SQL Server (Microsoft.Data.SqlClient / EF Core SqlServer)
- Result Pattern customizado

### Frontend

- Blazor Web App (.NET 10)
- Interactive WebAssembly render mode
- PWA baseline (manifest + service worker + offline shell)

### Qualidade e Testes

- xUnit
- FluentAssertions

## 3. Estrutura do Repositorio

```text
.
|-- CriaCerto.slnx
|-- agents.md
|-- roadmap.md
|-- Funções MVP
|-- docs/
|   |-- adrs/
|   |   `-- 0001-foundation-modular-monolith.md
|   |-- architecture/
|   |   `-- foundation-0.1.md
|   `-- contributing/
|       `-- tdd-workflow.md
|-- src/
|   |-- BuildingBlocks/
|   |   |-- CriaCerto.BuildingBlocks.Abstractions/
|   |   |-- CriaCerto.BuildingBlocks.Application/
|   |   `-- CriaCerto.BuildingBlocks.Infrastructure/
|   |-- Host/
|   |   `-- CriaCerto.Api/
|   |-- Modules/
|   |   |-- Breeding/
|   |   `-- Maternity/
|   `-- Web/
|       `-- CriaCerto.Web/
|           |-- CriaCerto.Web/
|           `-- CriaCerto.Web.Client/
|-- tests/
|   |-- Unit/
|   |   |-- CriaCerto.BuildingBlocks.UnitTests/
|   |   |-- CriaCerto.Modules.Tenancy.UnitTests/
|   |   `-- ...
|   `-- Integration/
```

## 4. Principios Arquiteturais

### 4.1 Modular Monolith

- Modulos isolados por dominio em `src/Modules/*`
- Sem acoplamento de persistencia entre modulos
- Composicao no host (`src/Host/CriaCerto.Api`)

### 4.2 Shared Kernel

- Contratos e tipos base em `src/BuildingBlocks`
- `Result`, `Result<T>`, `Error`, `ErrorType` como contrato padrao

### 4.3 Regra de Erros de Negocio

- Nao usar exceptions para fluxo esperado de negocio
- Retornar sempre `Result`/`Result<T>` para sucesso/falha esperada

### 4.4 TDD Obrigatorio

Fluxo Red -> Green -> Refactor em qualquer entrega funcional.

## 5. Estado Atual (Go-Live Phase 1 - CONCLUÍDA)

Concluído:

- **Sub-fase 1.1 (CONCLUÍDA):** Landing Page Comercial (`/`) com catálogo de planos (`/api/v1/tenancy/plans`), design system visual Stitch.
- **Sub-fase 1.2 (CONCLUÍDA):** Auto-Cadastro de Usuário (`/register`) e Recuperação/Redefinição de Senha (`/forgot-password`), validados com FluentValidation e Result Pattern (`Result.Failure(Error.Conflict)`).
- **Sub-fase 1.3 (CONCLUÍDA):** Wizard de Onboarding do Primeiro Acesso & Cadastro da Fazenda (`/onboarding`), vinculando automaticamente o produtor como Administrador em `UserTenant` via `CreateTenantCommand` e emitindo JWT token sem o erro `Auth.NoTenantAssociation`.
- Solucao monorepo em `CriaCerto.slnx` com 110 testes automatizados (100% de aprovação).
- BuildingBlocks base (Abstractions, Application, Infrastructure).
- Blazor Web App + WASM PWA com cache offline.
- Documentação viva e ADRs atualizadas em `/docs`.

Em andamento / Próxima entrega:

- **Phase 2:** Gestão da Organização, Unidades & Permissões (RBAC).

## 6. Requisitos de Ambiente

- .NET SDK 10.x
- SQL Server 2022+ (recomendado)
- Git

Opcional:

- VS Code + C# Dev Kit

## 7. Configuracao Local

### 7.1 Clonar e restaurar dependencias

```bash
git clone <url-do-repositorio>
cd CriaCerto
dotnet restore CriaCerto.slnx
```

### 7.2 Connection string

A API usa `ConnectionStrings:SqlServer` em configuracao. Se ausente, cai no fallback local:

```text
Server=localhost,1433;Database=criacerto_foundation;User Id=sa;Password=CriaCerto@123;TrustServerCertificate=True;Encrypt=False
```

Recomendacao: definir a connection string em `appsettings.Development.json` da API ou via variavel de ambiente.

## 8. Como Executar

### 8.1 Build

```bash
dotnet build CriaCerto.slnx -c Debug
```

### 8.2 Testes

Todos os testes:

```bash
dotnet test CriaCerto.slnx -c Debug
```

Somente unitarios de BuildingBlocks:

```bash
dotnet test tests/Unit/CriaCerto.BuildingBlocks.UnitTests/CriaCerto.BuildingBlocks.UnitTests.csproj -c Debug
```

### 8.3 Rodar API

```bash
dotnet run --project src/Host/CriaCerto.Api/CriaCerto.Api.csproj
```

Ao iniciar, a API aplica automaticamente as migrations EF Core pendentes de todos os `DbContext` registrados no host.
Para bancos legados sem histórico, a API valida tabelas, colunas, tipos e nulabilidade antes de registrar
automaticamente o baseline. Se houver drift ou schema parcial, o startup é interrompido sem alterar dados.
Os scripts manuais em [`scripts/database/baseline/README.md`](scripts/database/baseline/README.md) permanecem
disponíveis para operações controladas.

Health check:

- `GET /health`

OpenAPI (development):

- `/openapi/v1.json`

### 8.4 Rodar Web

```bash
dotnet run --project src/Web/CriaCerto.Web/CriaCerto.Web/CriaCerto.Web.csproj
```

## 9. Convencoes de Desenvolvimento

### 9.1 Result Pattern

- Retornos esperados devem usar `Result`/`Result<T>`
- Falhas de validacao/conflito/nao encontrado devem ser explicitadas por `Error`

### 9.2 Fronteiras de Modulo

- Nao criar joins ou dependencias de persistencia entre modulos
- Integracao entre modulos deve ocorrer por contratos/eventos internos

### 9.3 Living Documentation

Sempre atualizar `docs/` junto com alteracoes arquiteturais ou de regra de negocio:

- ADRs em `docs/adrs/`
- Guias tecnicos em `docs/architecture/`
- Fluxo de contribuicao em `docs/contributing/`

## 10. Roadmap de Produto

O roadmap detalhado esta em `roadmap.md`, organizado por fases:

- Phase 0: Foundation, Architecture & Tooling Setup
- Phase 1: Multi-Tenancy, Authentication & Licensing
- Phase 2: Breeding & Sow Management
- Phase 3: Maternity & Farrowing Operations
- Phase 4: Nursery, Growth & Finishing
- Phase 5: Nutrition, Sanitary & Zootecnic Analytics

Regra de conclusao: uma fase so e considerada concluida com backend + frontend + testes + offline (quando aplicavel) + docs atualizados.

## 11. Alertas Conhecidos

No estado atual, o build sinaliza vulnerabilidade conhecida (NU1903) relacionada a `Microsoft.OpenApi` 2.0.0 em projetos que consomem OpenAPI transitivamente.

Acao recomendada:

- Revisar e atualizar dependencias OpenAPI/ASP.NET quando houver versao corrigida compativel com a stack em uso.

## 12. Referencias do Projeto

- `agents.md`: diretrizes de arquitetura, DDD, Result Pattern, TDD e limites de modulo
- `roadmap.md`: plano por fases e gates de qualidade
- `Funções MVP`: escopo funcional inicial de produto

## 13. Licenca

Licenca ainda nao definida neste repositorio.
