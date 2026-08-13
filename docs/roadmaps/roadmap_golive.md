# Roadmap de Correções e Prontidão para Produção (Go-Live) - CriaCerto

## 1. Estratégia do Roadmap

Este roadmap complementar define o plano de ação detalhado para solucionar as pendências identificadas na auditoria de prontidão para produção. Ele estrutura as entregas em **5 Fases Sequenciais**, garantindo que o sistema passe da jornada inicial do usuário (Landing Page, Auto-cadastro e Onboarding da Fazenda) até a usabilidade plena dos módulos zootécnicos e prontidão de infraestrutura/deploy.

### Regra de Conclusão por Sub-fase (Definition of Done)
> **Uma sub-fase só é considerada "CONCLUÍDA" quando:**
> 1. O código Backend (.NET 10) e Frontend (Blazor WASM PWA) estiverem implementados com o Result Pattern (`Result<T>`).
> 2. Houver cobertura completa de testes unitários e de integração (TDD).
> 3. O suporte off-line (IndexedDB) for mantido para operações de curral/pasto.
> 4. A documentação viva (`/docs`) e o status da sub-fase forem atualizados.

---

## 2. Visão Geral das Fases

```
┌─────────────────────────────────────────────────────────┐
│ Phase 1: Chegada do Usuário, Auto-Cadastro & Onboarding │
└────────────────────────────┬────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────┐
│ Phase 2: Gestão da Organização, Unidades & Equipe (RBAC)│
└────────────────────────────┬────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────┐
│ Phase 3: Usabilidade Plena dos Módulos Zootécnicos      │
└────────────────────────────┬────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────┐
│ Phase 4: Central Global de Sincronização Off-line PWA   │
└────────────────────────────┬────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────┐
│ Phase 5: Infraestrutura, Segurança & Esteira CI/CD      │
└─────────────────────────────────────────────────────────┘
```

---

## 3. Detalhamento das Fases & Entregáveis

---

### Phase 1: Chegada do Usuário, Auto-Cadastro & Onboarding da Fazenda

#### Sub-fase 1.1: Landing Page Comercial & Portal de Boas-Vindas (`/`) [CONCLUÍDA]
* **Backend (.NET 10):**
  * Endpoint público de consulta de planos disponíveis (`GET /api/v1/tenancy/plans`).
* **Frontend (Blazor WASM):**
  * Substituir o preview legado de suinocultura em `Home.razor` por uma **Landing Page Comercial moderna** para o CriaCerto Bovino.
  * Seções: Hero Banner (proposta de valor), Funcionalidades Principais (Curral PWA, IATF, Balança, GPD, Sanidade), Tabela Comparativa de Planos (`Starter`, `Pro`, `Enterprise`) e botões de `Entrar` e `Experimentar Grátis`.
* **TDD & Validação:**
  * Testes de renderização dos componentes e redirecionamento de navegação.

#### Sub-fase 1.2: Auto-Cadastro de Usuário (Sign-Up) & Recuperação de Senha [CONCLUÍDA]
* **Backend:**
  * `RegisterUserCommand` (Nome Completo, E-mail, Senha, Telefone) com `FluentValidation` (formato de e-mail, senha forte, e-mail único).
  * `ForgotPasswordCommand` e `ResetPasswordCommand` para recuperação de acesso via token.
* **Frontend:**
  * Componente `Register.razor` gerado via padrão MCP Stitch com formulário reativo e feedback visual.
  * Modal/Página `ForgotPassword.razor` para solicitação de redefinição.
* **TDD & Validação:**
  * Testes unitários para `RegisterUserCommandHandler`, `RegisterUserCommandValidator`, `ForgotPasswordCommandHandler` e `ResetPasswordCommandHandler` garantindo cobertura completa e tratamento de e-mail duplicado via `Result.Failure(Error.Conflict)`.

#### Sub-fase 1.3: Wizard de Onboarding do Primeiro Acesso & Cadastro da Fazenda [CONCLUÍDA]
* **Backend:**
  * `CreateTenantCommand` / `CreateFarmCommand` (Nome da Fazenda, CNPJ/CPF, UF/Município, Inscrição Estadual, Área em Hectares, Plano Selecionado).
  * Vínculo automático do novo usuário como Administrador da fazenda criada no `UserTenant`.
* **Frontend:**
  * Componente `OnboardingWizard.razor` (Assistente de 3 passos):
    1. *Passo 1:* Perfil do Produtor / Responsável Técnico.
    2. *Passo 2:* Dados da Fazenda e Localização.
    3. *Passo 3:* Escolha do Plano & Capacidade Inicial de Cabeças.
* **TDD & Validação:**
  * Teste de integração garantindo que usuários recém-cadastrados consigam criar uma fazenda e recebam JWT válido sem travar no erro `Auth.NoTenantAssociation`.

---

### Phase 2: Gestão da Organização, Unidades & Permissões (RBAC)

#### Sub-fase 2.1: Perfil da Fazenda & Unidades de Produção [CONCLUÍDA]

* **Backend:**
  * `UpdateTenantProfileCommand` (Edição de CNPJ, Razão Social, Endereço, Capacidade).
  * CRUD de Unidades Produção (`CreateProductionUnitCommand`, `GetProductionUnitsQuery`).
* **Frontend:**
  * Conectar a página `OrganizationManagement.razor` aos comandos reais do backend.
  * Modal funcional para o botão **"Nova Unidade"** (Retiros, Invernadas, Confinamento) e **"Editar Perfil"**.
* **TDD & Validação:**
  * Testes unitários para regras de validação de CNPJ/CPF e capacidade máxima de cabeças por plano.

#### Sub-fase 2.2: Convites de Equipe & Controle de Acesso (RBAC) [CONCLUÍDA]
* **Backend:**
  * Tabela e domínio `UserTenantRole` com papéis: `Admin`, `Zootecnista`, `Veterinario`, `OperadorCurral`.
  * `InviteTeamMemberCommand` (Envio de convite por e-mail com token de expiração).
* **Frontend:**
  * Seção de "Membros da Equipe" em `OrganizationManagement.razor` com modal de convite e seletor de perfil de acesso.
* **TDD & Validação:**
  * Testes garantindo que operadores de curral não consigam acessar áreas administrativas/financeiras.

---

### Phase 3: Usabilidade Plena dos Módulos Zootécnicos

#### Sub-fase 3.1: Cadastro Individual & Ficha Completa do Bovino [CONCLUÍDA]
* **Backend:**
  * Endpoints `CreateAnimalCommand`, `UpdateAnimalCommand` e `GetAnimalDetailQuery` atendendo a especificação do `Funções MVP`.
* **Frontend:**
  * Tela/Modal de Cadastro Individual de Bovino em `Registry.razor` (Brinco SISBOV/Eletrônico, Apelido, Registro PBB, Raça, Origem, Data Nasc, Data Entrada, Peso Entrada, Pai, Mãe, ECC).
  * Ficha Detalhada do Animal (`AnimalDetail.razor`) exibindo linha do tempo unificada (pesagens, IATF, partos, vacinas).
  * Painel de Matrizes com filtros em tempo real por estado reprodutivo (Lactante, Gestante, Vazia) com contadores visuais.
* **TDD & Validação:**
  * Testes unitários cobrindo validação de brincos duplicados dentro do mesmo Tenant.

#### Sub-fase 3.2: Importação de Balanças de Curral & Análise de GPD [CONCLUÍDA]
* **Backend:**
  * `ImportWeighingFileCommand` (parser para arquivos de balança Tru-Test, Coimma, Toledo em formato CSV/TXT).
  * Serviço de cálculo e alerta de perda de peso (GPD negativo em 2 pesagens consecutivas).
* **Frontend:**
  * Modal de importação de arquivo em `CurralWeighingFastInput.razor`.
  * Componente `GpdTrendChart.razor` com realce visual para animais com alerta de anomalia de ganho de peso.
* **TDD & Validação:**
  * Testes unitários com arquivos de pesagem sintéticos de múltiplos modelos de balança.

#### Sub-fase 3.3: Integridade de Estoque & Trava Sanitária de Abate [CONCLUÍDA]
* **Backend:**
  * Handler de evento para baixa automática no `SiloStock` ao registrar trato (`DailyFeedBatch`) ou suplementação mineral (`PastureSupplementation`).
  * Regra de bloqueio em `DispatchAnimalCommand` que impede emissão de lote para abate se houver animal em Período de Carência Medicamentosa.
* **Frontend:**
  * Indicador de nível crítico de estoque nos widgets de Nutrição.
  * Badge e modal de bloqueio sanitário na listagem de animais e montagem de lote de venda.
* **TDD & Validação:**
  * Teste unitário e de integração garantindo que o despacho para abate seja rejeitado com `Result.Failure(Error.Validation)` para animais medicados.

#### Sub-fase 3.4: Relatórios Executivos & Exportação de Dados (GTA/Inventário) [CONCLUÍDA]
* **Backend:**
  * Endpoint `ExportBovineReportQuery` gerando arquivos CSV, Excel e PDF estilizado para inventário de rebanho e relatórios de suporte à emissão de GTA.
* **Frontend:**
  * Modal de exportação no `ExecutiveDashboard.razor` com seletores de período (Safra/Entressafra, Mês, Personalizado).
* **TDD & Validação:**
  * Testes de integração validando a geração dos arquivos de exportação.

---

### Phase 4: Central Global de Sincronização Off-line PWA

#### Sub-fase 4.1: Gerenciador de Sincronização & Mediação de Conflitos [CONCLUÍDA]
* **Frontend (Blazor WASM PWA):**
  * Criar o componente global `SyncStatusHeader.razor` integrado ao `MainLayout.razor`.
  * Exibir contagem reativa de operações armazenadas no `IndexedDB` pendentes de envio.
  * Indicador visual de estado de conexão (`Online` / `Offline`).
  * Botão de **"Forçar Sincronização"** e modal de resolução de conflitos para tratar dados divergentes entre campo e servidor.
* **TDD & Validação:**
  * Testes de componente Blazor simulação de perda e restauração de conexão com a rede.

---

### Phase 5: Infraestrutura, Segurança & Esteira CI/CD

#### Sub-fase 5.1: Seeders de Banco de Dados & Dados de Referência [CONCLUÍDA]
* **Backend:**
  * `SystemDataSeeder` para popular automaticamente em produção as tabelas de referência (Raças Bovinas Brasileiras: Nelore, Angus, Brahman, Senepol, Gir, Girolando; Calendário de vacinas oficiais MAPA/Estaduais).
* **TDD & Validação:**
  * Testes de integração com `Testcontainers` verificando a execução idempotente dos seeders.

#### Sub-fase 5.2: Segurança, Variáveis de Ambiente & UI Feedback [CONCLUÍDA]
* **Backend & Frontend:**
  * Substituição das chaves JWT de desenvolvimento por variáveis de ambiente de produção (`appsettings.Production.json`).
  * Ajuste de políticas de CORS e redirecionamento HTTPS obrigatório.
  * Implementação do serviço de **Toast Notifications** (mensagens flutuantes de Sucesso, Erro, Alerta) e container de tratamento de exceções Blazor `ErrorBoundary`.
* **TDD & Validação:**
  * Verificação de segurança de cabeçalhos e validação de inicialização da API em modo de produção.

#### Sub-fase 5.3: Containerização & Esteira de Deploy CI/CD [CONCLUÍDA]
* **DevOps:**
  * Criar `Dockerfile` otimizado em múltiplos estágios para `CriaCerto.Api` e `CriaCerto.Web`.
  * Atualizar o `docker-compose.yml` de produção incluindo PostgreSQL, Redis e containers da aplicação.
  * Configurar o workflow do GitHub Actions (`.github/workflows/deploy.yml`) para compilar, executar a suíte de testes (`dotnet test`) e realizar o push das imagens.
* **Validação:**
  * Execução do build completo e publicação de staging verificada.

---

## 4. Checklist de Sign-Off por Sub-Fase

Para marcar qualquer sub-fase como **CONCLUÍDA**, a checklist abaixo deve ser preenchida e validada:

```markdown
- [ ] Endpoints e serviços backend implementados em .NET 10 com Result Pattern (Result<T>).
- [ ] Interface Blazor WASM PWA desenvolvida seguindo os padrões visuais MCP Stitch.
- [ ] Testes unitários e de integração desenvolvidos e aprovados (TDD Red/Green/Refactor).
- [ ] Persistência local (IndexedDB) e sincronização testadas para operações de campo.
- [ ] Documentação viva (/docs) atualizada refletindo as alterações do domínio.
```

---
*Roadmap oficial de correções e Go-Live do projeto CriaCerto.*
