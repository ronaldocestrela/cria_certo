# Funções Cria Certo

### 1. 🐄 Reprodução e Gestão do Plantel (`Breeding`)
Centralizado em [PlantelFeatures.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Breeding/CriaCerto.Modules.Breeding.Application/Features/Plantel/PlantelFeatures.cs) e [BreedingOpsFeatures.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Breeding/CriaCerto.Modules.Breeding.Application/Features/BreedingOps/BreedingOpsFeatures.cs):
* **Cadastro de Matrizes/Fêmeas e Touros:**
  * Cadastro completo de animais com Brinco (`EarTag`), Raça, Data de Nascimento, SISBOV, RFID, Tatuagem, Apelido, Registro PBB/Genealógico, Origem, Data e Peso de Entrada no Plantel, Filiação (Pai/Mãe) e Score de Condição Corporal (ECC/BCS de 1 a 5).
  * Validação de unicidade de brinco por fazenda (*Tenant*).
* **Edição e Atualização:** Atualização cadastral de dados zootécnicos e corporais de matrizes.
* **Consulta e Listagem do Plantel:** Listagem paginada e com filtros por status reprodutivo (*Vazia*, *Inseminada*, *Prenhe*, *Lactante*, *Seca*, *Descarte*) e busca por brinco/apelido.
* **Ficha Detalhada do Animal:** Cálculo automático de idade, tempo de permanência no plantel e linha do tempo de ciclos reprodutivos.
* **Catálogo de Touros:** Consulta e listagem dos reprodutores ativos da fazenda.
* **Protocolos de IATF:** Cadastro e acompanhamento de protocolos de Inseminação Artificial em Tempo Fixo com cronograma de dosagens hormonais (D0, D8, D9, D11).
* **Diagnóstico de Gestação (DG):** Registro de diagnóstico com resultado (*Prenhe* / *Vazia*), cálculo de dias de gestação, Data Provável de Parto (DPP) e sinalizador de DNP (Dias Não Prenhe).
* **Interface Web/Telas:**
  * [Registry.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/Registry.razor): Painel do plantel com filtros e busca.
  * [AnimalDetail.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/AnimalDetail.razor): Ficha do animal com linha do tempo.
  * [IatfProtocols.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/IatfProtocols.razor): Controle de lotes de inseminação.
  * [BatchBreedingLog.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/BatchBreedingLog.razor): Apontamento em lote.
  * [PregnancyCheckTasks.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Breeding/PregnancyCheckTasks.razor): Fila de tarefas para toque/ultrassom.

---

### 2. 🍼 Maternidade e Desmame (`Calving`)
Centralizado em [CalvingContracts.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Calving/CriaCerto.Modules.Calving.Application/Contracts/CalvingContracts.cs):
* **Registro de Partos:** Apontamento de nascimento com vínculo à matriz e touro/sêmen, tipo de parto (*Normal*, *Distócico*, *Cesárea*), sexo do bezerro, peso ao nascer e escore de vigor.
* **Histórico de Nascimentos:** Consulta e listagem de partos ocorridos por período e fazenda.
* **Registro de Desmame:** Apontamento de desmame com peso aferido e cálculo de ganho médio pré-desmame.
* **Interface Web/Telas:**
  * [CalvingRecords.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Calving/CalvingRecords.razor): Histórico de partos e registros de desmama.

---

### 3. 📈 Recria, Engorda, Pesagens e Pastagens (`Growth`)
Centralizado em [GrowthContracts.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Growth/CriaCerto.Modules.Growth.Application/Contracts/GrowthContracts.cs):
* **Gestão de Pastos e Piquetes:** Cadastro de piquetes com área em hectares, variedade forrageira e cálculo em tempo real da **Taxa de Lotação** (Unidade Animal por hectare: `UA/ha`).
* **Gestão de Lotes:** Criação de lotes, movimentação/troca de animais entre piquetes e encerramento de lotes.
* **Pesagens e GPD (Ganho de Peso Diário):**
  * Apontamento de pesagem individual e em lote.
  * Cálculo instantâneo de GPD entre a última pesagem e a pesagem atual.
  * Histórico evolutivo de peso por animal (`/api/growth/weighings/history/{tag}`).
  * Resumo de pesagens e média ponderal por lote (`/api/growth/weighings/lot-summary/{lotId}`).
* **Importação de Balança Eletrônica:** Upload de arquivos brutos de balanças digitais (Tru-Test, Coimma, etc.) com conversão de carcaça e importação em lote (`/api/growth/weighings/import`).
* **Detecção de Anomalias de Pesagem:** Algoritmo que sinaliza pesagens com variações estatísticas suspeitas ou erros de digitação (`/api/growth/weighings/anomalies`).
* **Expedição e Despacho:** Registro de saída/despacho individual ou por lote (abate, venda, transferência).
* **Interface Web/Telas:**
  * [PastureManagement.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Growth/PastureManagement.razor): Visão visual dos piquetes e taxa de lotação.
  * [CurralWeighingFastInput.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Growth/CurralWeighingFastInput.razor): Tela otimizada para lançamento rápido de pesagens no curral.

---

### 4. 🌾 Nutrição e Confinamento (`Nutrition`)
Centralizado em [SiloStockFeatures](file:///home/rony/LPR/CriaCerto/src/Modules/Nutrition/CriaCerto.Modules.Nutrition.Application/Features/SiloStockFeatures/SiloStockCommandsAndQueries.cs), [FeedRationFeatures](file:///home/rony/LPR/CriaCerto/src/Modules/Nutrition/CriaCerto.Modules.Nutrition.Application/Features/RationFeatures/FeedRationCommandsAndQueries.cs) e [FeedingFeatures](file:///home/rony/LPR/CriaCerto/src/Modules/Nutrition/CriaCerto.Modules.Nutrition.Application/Features/FeedingFeatures/FeedingCommands.cs):
* **Controle de Silos e Estoque:** Cadastro de silos/galpões, capacidade estática e apontamento de reabastecimento (`restock`) com custo por tonelada.
* **Formulações de Ração:** Cadastro de dietas e receitas de ração com custo por quilo.
* **Trato e Fornecimento de Cocho:**
  * Apontamento de suplementação mineral/proteica a pasto.
  * Apontamento de batidas de ração TMR (*Total Mixed Ration*) para confinamento.
* **Métricas Zootécnicas e Financeiras de Cocho:**
  * Cálculo da **Conversão Alimentar (CA)** de lotes em confinamento (`/api/nutrition/analytics/feed-conversion`).
  * Cálculo do **Custo por Arroba (@) Produzida** (`/api/nutrition/analytics/cost-per-arroba`).
* **Interface Web/Telas:**
  * [SiloStockManagement.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Nutrition/SiloStockManagement.razor): Painel de silos e saldos de insumos.
  * [DailyTroughLog.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Nutrition/DailyTroughLog.razor): Diário de fornecimento de cocho.
  * [PastureSupplementationLog.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Nutrition/PastureSupplementationLog.razor): Apontamento de suplementação a pasto.

---

### 5. 💉 Manejo Sanitário (`Sanitary`)
Centralizado em [SanitaryContracts.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Sanitary/CriaCerto.Modules.Sanitary.Application/Contracts/SanitaryContracts.cs):
* **Campanhas de Vacinação:** Criação e acompanhamento de campanhas sanitárias coletivas (febre aftosa, brucelose, clostridioses, etc.).
* **Tratamentos Veterinários:** Registro de aplicação de medicamentos com dosagem, lote farmacêutico e período de carência.
* **Bloqueio de Abate (Segurança Sanitária):** Validação automática de elegibilidade para abate (`/slaughter-validation/{animalId}`), impedindo o envio para frigorífico de animais dentro do período de carência de medicamentos.

---

### 6. 📊 Indicadores Executivos e Relatórios (`Analytics`)
Centralizado em [ExecutiveAnalyticsContracts.cs](file:///home/rony/LPR/CriaCerto/src/Modules/Analytics/CriaCerto.Modules.Analytics.Application/Contracts/ExecutiveAnalyticsContracts.cs):
* **Executive Scorecard:** Painel consolidado com taxa de prenhez, taxa de desmama, lotação média (UA/ha), GPD médio da fazenda, custo médio da arroba e contagem de animais em período de carência.
* **Exportação de Relatórios:** Geração e download de relatórios em CSV e formato estruturado (`/api/analytics/export`).
* **Interface Web/Telas:**
  * [ExecutiveDashboard.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Analytics/ExecutiveDashboard.razor): Painel de gráficos e KPIs executivos.

---

### 7. 🏢 Multi-Tenancy, Identidade e Faturamento (`Tenancy`)
Centralizado nas pastas de [Tenancy/Features](file:///home/rony/LPR/CriaCerto/src/Modules/Tenancy/CriaCerto.Modules.Tenancy.Application/Features):
* **Autenticação e Segurança:** Cadastro de usuários, login JWT com Refresh Token, confirmação de e-mail por link criptográfico, esqueci minha senha e redefinição de senha.
* **Gestão de Fazendas (Tenants):** Cadastro de novas propriedades rurais, alternância entre fazendas ativas e edição de perfil da fazenda.
* **Unidades de Produção:** Cadastro e manutenção de retiros/unidades produtivas vinculadas à fazenda.
* **Gestão de Equipe (RBAC):** Envio de convites por e-mail com perfis de acesso, aceite de convites e revogação/remoção de membros.
* **Faturamento SaaS & Stripe:**
  * Catálogo de planos de assinatura.
  * Checkout automático de assinatura integrado ao **Stripe Checkout**.
  * Portal de autoatendimento para cartões e notas fiscais (**Stripe Customer Portal**).
  * Webhook idempotente para processamento automático de eventos de pagamento e faturas do Stripe.
  * Worker em segundo plano para ciclo de vida de assinaturas e expiração de trial.
* **Interface Web/Telas:**
  * [Login.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Login.razor), [Register.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Register.razor), [ConfirmEmail.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/ConfirmEmail.razor), [ForgotPassword.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/ForgotPassword.razor).
  * [OnboardingWizard.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/OnboardingWizard.razor): Assistente inicial de configuração da fazenda.
  * [OrganizationManagement.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/OrganizationManagement.razor): Gestão de equipe e retiros.
  * [SubscriptionManagement.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/SubscriptionManagement.razor): Escolha e gestão de plano.
  * [TrialExpired.razor](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/TrialExpired.razor): Bloqueio de acesso pós-trial.

---

### 8. 🛡️ Backoffice Administrativo e Governança (`Backoffice`)
Módulo corporativo completo com endpoints em [Program.cs](file:///home/rony/LPR/CriaCerto/src/Host/CriaCerto.Api/Program.cs#L294-L1168) e telas dedicadas:
* **KPIs da Plataforma:** MRR, ARR, taxa de churn, total de fazendas ativas e alertas em aberto.
* **IAM de Administradores:** Gestão de usuários do backoffice com papéis granulares, MFA (TOTP com QR code e códigos de recuperação) e gestão/revogação de sessões ativas.
* **Operações de Tenants:** Suspensão, reativação, cancelamento e arquivamento de fazendas com registro de justificativa, além de segmentação comercial e etiquetas operacionais (`Tags`).
* **Catálogo e Versionamento de Planos:** Criação de versões imutáveis de planos (Draft/Publicado), comparação de versões e publicação com aprovação.
* **Impersonação de Suporte (Shadow Login Seguro):** Acesso à conta do cliente com motivo formal e ticket de suporte, gerando trilha de auditoria completa.
* **Playbooks de Suporte e Diagnóstico:** Diagnóstico automatizado de anomalias no tenant e execução orientada de scripts de remediação.
* **Aprovação Dupla (4-Eyes Approval):** Fluxo para exigir que ações críticas (ex.: cancelamento retroativo, alteração contratual) sejam solicitadas por um operador e validadas por outro.
* **Auditoria Forense (Audit Trail):** Registro imutável com cadeia criptográfica (SHA-256), verificação de integridade da trilha e exportação para auditoria externa.
* **Observabilidade e Anomalias:** Painel de saúde dos serviços, ingestão de alertas automáticos e simulação de incidentes.
* **Governança LGPD:** Trilha de acesso a dados de clientes, mascaramento de PII e fluxo de revelação de dados sensíveis com justificativa formal.
* **Feature Flags & Rollout Gradual:** Ativação progressiva de funcionalidades por ondas percentuais (0% a 100%), anéis de liberação e botão de emergência (*Kill Switch*).

---

### 9. 📚 Dados de Referência (`BuildingBlocks / ReferenceData`)
* **Raças Bovinas:** Catálogo padronizado de raças de corte e leite com especificações zootécnicas (`/api/reference/breeds`).
* **Calendário Vacinal:** Catálogo de vacinas e protocolos oficiais (`/api/reference/vaccines`).

---

### 10. 🎯 Em Andamento (Programa de Indicação - `Referral`)
* **Modelagem de Domínio Concluída:** Entidades de domínio ([ReferralCode](file:///home/rony/LPR/CriaCerto/src/Modules/Referral/CriaCerto.Modules.Referral.Application/Domain/ReferralCode.cs), [ReferralAttribution](file:///home/rony/LPR/CriaCerto/src/Modules/Referral/CriaCerto.Modules.Referral.Application/Domain/ReferralAttribution.cs), [PartnerContract](file:///home/rony/LPR/CriaCerto/src/Modules/Referral/CriaCerto.Modules.Referral.Application/Domain/PartnerContract.cs), [ReferralRewardLedger](file:///home/rony/LPR/CriaCerto/src/Modules/Referral/CriaCerto.Modules.Referral.Application/Domain/ReferralRewardLedger.cs)) e testes unitários de domínio implementados. Os endpoints de API e telas seguem em roadmap conforme [roadmap_referral_program.md](file:///home/rony/LPR/CriaCerto/docs/roadmaps/roadmap_referral_program.md).