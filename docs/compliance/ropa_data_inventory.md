# Registro das Operações de Tratamento de Dados Pessoais (ROPA - Art. 37 LGPD)

> **Documento Oficial de Conformidade Regulatória**  
> **Controlador / Operador:** CriaCerto Bovino SaaS (CriaCerto Tecnologia Agropecuária Ltda.)  
> **Fundamentação Legal:** Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018, Art. 37)  
> **Versão:** 1.0 — Outubro/2026  
> **Periodicidade de Revisão:** Anual ou mediante alteração arquitetural / introdução de novos módulos  
> **Canal do Encarregado (DPO):** `dpo@criacerto.com.br` / `privacidade@criacerto.com.br` | Portal: `/dpo`

---

## 1. Identificação dos Agentes de Tratamento e Governança

| Papel | Razão Social / Identificação | Contato Oficial | Atuação no Ecossistema |
|---|---|---|---|
| **Controlador** | CriaCerto Tecnologia Agropecuária Ltda. (CNPJ: 00.000.000/0001-00) | `contato@criacerto.com.br` | Tratamento de dados cadastrais, autenticação, faturamento de assinaturas, programa de afiliados e logs de governança da plataforma SaaS. |
| **Operador** | CriaCerto Tecnologia Agropecuária Ltda. | `suporte@criacerto.com.br` | Tratamento de dados de colaboradores da fazenda (vaqueiros, veterinários, operadores de curral) inseridos pelos produtores rurais para manejos zootécnicos e sanitários, sob o [DPA B2B (ADR 0022)](file:///home/rony/LPR/CriaCerto/docs/adrs/0022-lgpd-b2b-data-processing-agreement-dpa.md). |
| **Controladores Parceiros (B2B)** | Produtores Rurais / Fazendas Contratantes | Conforme cadastro em `Tenant` | Gestores das fazendas que contratam o SaaS e inserem dados de suas equipes e propriedades rurais. |
| **Encarregado de Dados (DPO)** | Comitê de Privacidade e Segurança da Informação CriaCerto | `dpo@criacerto.com.br` | Supervisão regulatória, canal oficial de atendimento aos titulares e interlocução com a ANPD (Art. 41). |

---

## 2. Princípios Gerais e Salvaguardas Globais da Plataforma

Todas as operações de tratamento registradas neste ROPA atendem estritamente aos princípios do **Art. 6º da LGPD**:
1. **Finalidade e Adequação**: Tratamento restrito à viabilização da gestão pecuária e obrigações contratuais/legais.
2. **Necessidade (Minimização)**: Coleta estrita dos campos indispensáveis à funcionalidade requerida.
3. **Livre Acesso e Qualidade dos Dados**: Autoatendimento via `/settings/profile`, `/settings/privacy` e canal `/dpo`.
4. **Transparência**: Informações claras em [Termos de Uso](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Legal/TermsOfService.razor), [Política de Privacidade](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Legal/PrivacyPolicy.razor) e [DPA](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Legal/DataProcessingAgreement.razor).
5. **Segurança e Prevenção**: Criptografia em repouso **AES-256-GCM** com blind indexing **HMAC-SHA256**, hashing PBKDF2 para senhas, segregação multi-tenant, mascaramento contextual de PII (`IPiiDataMasker`) e trilha forense SHA-256 encadeada (`AuditLog`).
6. **Não Discriminação e Responsabilização**: Living documentation auditável, runbooks de resposta a incidentes ([RUNBOOK-07](file:///home/rony/LPR/CriaCerto/docs/operations/runbook_incident_response.md)) e expurgo periódico automatizado.

---

## 3. Inventário Detalhado das Operações de Tratamento

### Macroprocesso 01: Onboarding, Cadastro e Autenticação de Titulares
- **ID da Atividade:** `ROPA-ACT-01`
- **Nome da Atividade:** Cadastro de Usuários, Ativação de Contas e Gestão de Sessões
- **Módulo Responsável:** `Modules.Tenancy`
- **Papel da CriaCerto:** 
  - **Controlador** para contas diretas de produtores rurais e administradores de conta.
  - **Operador** para operadores de campo convidados pelo produtor via `TeamInvite` (sob instruções do Controlador).
- **Finalidade do Tratamento:** Criação da conta de acesso, verificação de identidade por e-mail, controle de acesso baseado em papéis (RBAC), emissão de tokens JWT e gestão de sessões de uso seguro do SaaS.
- **Bases Legais (Art. 7º LGPD):**
  - **Execução de Contrato (Art. 7º, V)**: Prestação do serviço de gestão pecuária acordado nos Termos de Uso.
  - **Cumprimento de Obrigação Legal (Art. 7º, II)**: Armazenamento de registros de conexão e acesso a aplicações de internet pelo prazo de 6 meses (Art. 15 da Lei nº 12.965/2014 - Marco Civil da Internet).
  - **Consentimento Auditável (Art. 7º, I)**: Registro imutável de aceite dos Termos de Uso e Política de Privacidade com timestamp e versão em `UserConsent`.
- **Categorias de Titulares:** Produtores rurais, administradores de fazenda, colaboradores rurais convidados.
- **Categorias de Dados Pessoais:**
  - *Identificação e Contato*: Nome completo, endereço de e-mail, número de telefone celular.
  - *Dados Cadastrais da Granja*: CPF ou CNPJ do produtor rural, nome da fazenda.
  - *Credenciais de Acesso*: Hash de senha gerado com PBKDF2 (com salt criptográfico e constant-time comparison).
  - *Metadados de Conexão*: Endereço IP de origem, User-Agent do navegador, carimbo de data/hora de login e refresh tokens.
- **Subprocessadores e Compartilhamento:**
  - **Resend Inc.**: Disparo de e-mails transacionais de ativação (`ConfirmEmailCommand`) e redefinição de senha com tokens de uso único (TTL de 24 horas).
  - **Cloud Provider (PostgreSQL)**: Armazenamento persistente em volumes criptografados com isolamento lógico multi-tenant.
- **Salvaguardas de Segurança:**
  - Hashing irreversível de senhas com PBKDF2.
  - Dummy verification em tempo constante para eliminar enumeração de usuários por side-channel de timing.
  - Rate limiting nativo .NET 10 em endpoints de autenticação (15 req/min).
  - Background worker periódico (`DataRetentionPurgingBackgroundService`) limpando tokens de ativação e redefinição expirados há mais de 7 dias (`User.ClearExpiredTokens`).
- **Prazo de Retenção e Descarte:**
  - Dados cadastrais: Durante a vigência do contrato + 5 anos após a rescisão (prescrição de obrigações cíveis - Art. 206, § 5º do Código Civil).
  - Logs de conexão (IP/Data/Hora): 6 meses compulsórios (Art. 15 do Marco Civil da Internet).
  - Tokens temporários: Expurgo em D+7 após expiração.

---

### Macroprocesso 02: Cobrança, Assinatura de Planos e Faturamento SaaS
- **ID da Atividade:** `ROPA-ACT-02`
- **Nome da Atividade:** Gestão de Assinaturas, Faturamento Recorrente e Conciliação Financeira
- **Módulo Responsável:** `Modules.Tenancy` (Integração Stripe)
- **Papel da CriaCerto:** **Controlador**
- **Finalidade do Tratamento:** Cobrança de mensalidades/anuidades dos planos SaaS (Starter, Pro, Enterprise), emissão de faturas, controle de inadimplência (`PastDue`), aplicação de cupons/descontos e cumprimento de deveres tributários.
- **Bases Legais (Art. 7º LGPD):**
  - **Execução de Contrato (Art. 7º, V)**: Cobrança dos serviços contratados pelo produtor.
  - **Cumprimento de Obrigação Legal e Regulatória (Art. 7º, II)**: Emissão de documentos fiscais, escrituração contábil e prestação de contas à Receita Federal do Brasil (Art. 173 e 174 do Código Tributário Nacional).
- **Categorias de Titulares:** Produtores rurais contratantes e responsáveis financeiros da fazenda.
- **Categorias de Dados Pessoais:**
  - Razão Social / Nome completo, CPF / CNPJ, endereço de cobrança completo.
  - Identificador de cliente no gateway (`stripe_customer_id`), identificador da assinatura (`stripe_subscription_id`), status de adimplemento, histórico de faturas e valores pagos.
  - **Dados de Cartão de Crédito:** *Nenhum número de cartão, CVV ou data de validade é recebido, processado ou armazenado pela infraestrutura do CriaCerto*. O processamento é realizado diretamente no navegador via Stripe Elements sob certificação PCI-DSS Level 1.
- **Subprocessadores e Compartilhamento:**
  - **Stripe Inc.**: Gateway internacional de pagamento, conciliação e faturamento eletrônico (PCI-DSS Nível 1).
  - **Autoridades Fiscais e Fazendárias**: Emissão de notas fiscais de serviço eletrônicas (NFS-e).
- **Salvaguardas de Segurança:**
  - Tokenização ponta a ponta sem trânsito de dados de cartão nos servidores internos.
  - Validação rigorosa de assinatura criptográfica HMAC-SHA256 em webhooks recebidos do Stripe (`StripeWebhookController`).
  - Mascaramento contextual de dados fiscais e de faturamento nas interfaces administrativas do Backoffice (`IPiiDataMasker`).
  - Quarentena de proteção contra estornos e conciliação idempotente.
- **Prazo de Retenção e Descarte:**
  - 5 anos a contar do primeiro dia do exercício seguinte à emissão do documento fiscal (Art. 173, I do Código Tributário Nacional).

---

### Macroprocesso 03: Gestão Zootécnica, Sanitária e Operação do Rebanho
- **ID da Atividade:** `ROPA-ACT-03`
- **Nome da Atividade:** Registro de Manejo Reprodutivo, Ganho de Peso, Vacinações e Carência Sanitária
- **Módulos Responsáveis:** `Modules.Breeding`, `Modules.Calving`, `Modules.Growth`, `Modules.Nutrition`, `Modules.Sanitary`
- **Papel da CriaCerto:** **Operador** (O Produtor Rural / Fazenda atua como **Controlador** sob o DPA)
- **Finalidade do Tratamento:** Registro de eventos pecuários da propriedade (inseminação artificial IATF, partos, pesagens para cálculo de Ganho de Peso Diário - GPD/@, protocolos sanitários e tratamentos veterinários com carência de abate), gerando relatórios de produtividade e rastreabilidade.
- **Bases Legais (Art. 7º e Art. 39 LGPD):**
  - **Tratamento em nome do Controlador (Art. 39)**: A CriaCerto realiza o tratamento exclusivamente sob as instruções documentadas do produtor rural para execução do SaaS.
  - **Cumprimento de Obrigação Legal pelo Controlador (Art. 7º, II)**: Obrigações sanitárias estaduais e federais (Ministério da Agricultura e Pecuária - MAPA, controle de Febre Aftosa, Brucelose, Guia de Trânsito Animal - GTA e SISBOV).
- **Categorias de Titulares:** Vaqueiros, capatazes, operadores de curral, zootecnistas e médicos veterinários contratados pela fazenda.
- **Categorias de Dados Pessoais:**
  - Nome do operador que realizou o procedimento/manejo.
  - Registro profissional (CRMV) do médico veterinário responsável por receituários e vacinações obrigatórias.
  - Data, hora e identificação do lote/animal atendido.
- **Subprocessadores e Compartilhamento:**
  - **Cloud Provider (PostgreSQL)**: Persistência estruturada em esquema com isolamento lógico rígido por TenantId.
  - **Armazenamento Offline (IndexedDB)**: Criptografia e isolamento local no dispositivo móvel do operador no mangueiro até sincronização em segundo plano.
- **Salvaguardas de Segurança:**
  - Isolamento multi-tenant absoluto (impossibilidade de junções entre bancos/esquemas de fazendas distintas).
  - Registro de autoria operacional para atendimento às exigências de conformidade agropecuária e auditoria sanitária.
  - Política de carência de 30 dias após término de contrato para que o produtor rural exporte seu rebanho completo antes da eliminação física permanente.
- **Prazo de Retenção e Descarte:**
  - Durante o contrato de prestação de serviços da fazenda.
  - 30 dias pós-rescisão para exportação segura de relatórios canônicos (ADR 0022).
  - Prazos legais sanitários de rastreabilidade animal (até 5 anos conforme regulamentação do MAPA/SISBOV mantidos pelo produtor).

---

### Macroprocesso 04: Suporte Assistido, Governança Backoffice e Impersonação
- **ID da Atividade:** `ROPA-ACT-04`
- **Nome da Atividade:** Atendimento de Suporte N2/N3, Impersonação Assistida e Triage Operacional
- **Módulo Responsável:** `Modules.Backoffice`
- **Papel da CriaCerto:** **Controlador** (na relação com operadores internos) e **Operador** (na visualização assistida de dados do produtor)
- **Finalidade do Tratamento:** Resolução de dúvidas técnicas, investigação de incidentes de usabilidade e suporte emergencial solicitado por produtores rurais através do espelhamento supervisionado da interface.
- **Bases Legais (Art. 7º LGPD):**
  - **Legítimo Interesse com Salvaguardas Reforçadas (Art. 7º, IX)**: Diagnóstico e suporte assistido solicitados pelo cliente para assegurar a continuidade do negócio.
  - **Execução de Contrato (Art. 7º, V)**: Cumprimento dos níveis de serviço de suporte técnico acordados.
- **Categorias de Titulares:** Produtores rurais solicitantes de suporte e analistas de suporte/engenheiros da CriaCerto.
- **Categorias de Dados Pessoais:**
  - E-mail e credenciais corporativas do analista de suporte.
  - Justificativa administrativa detalhada para a sessão de impersonação.
  - Dados contextuais do produtor visualizados durante a sessão de suporte.
- **Subprocessadores e Compartilhamento:**
  - Nenhum subprocessador externo. Tratamento estritamente interno e confinado.
- **Salvaguardas de Segurança:**
  - **Dupla Salvaguarda Mandatória**: Autorização prévia documentada do produtor + justificativa fundamentada de no mínimo 10 caracteres registrada no Backoffice.
  - **Contenção Estrita em Middleware**: `BackofficeAccessMiddleware` intercepta e rejeita chamadas de tokens de impersonação a endpoints administrativos (`Backoffice.ImpersonationRestricted`).
  - **Mascaramento Contextual por Padrão**: Todos os CPFs, e-mails pessoais e dados financeiros são ofuscados na interface via `IPiiDataMasker` e `<MaskedDataField>`, exigindo comando formal de revelação Just-In-Time (`RevealSensitiveDataCommand`) com justificativa.
  - **Trilha Forense Imutável**: Registro compulsório no `AuditLog` com severidade `High` ou `Critical` e encadeamento criptográfico SHA-256 (`RecordHash` vinculado ao `PreviousRecordHash`).
  - **Kill-Switch Imediato**: Revogação de sessões em menos de 1 segundo através de circuitos emergenciais ([RUNBOOK-01](file:///home/rony/LPR/CriaCerto/docs/operations/runbook_incident_response.md)).
  - **Detecção de Anomalias**: Motor analítico `AnomalyDetectionEngine` alertando em tempo real sobre surtos de impersonação (`ALR_IMPERSONATION_BURST`) ou acessos fora de horário comercial (`ALR_OFF_HOURS_CRITICAL_ACTION`).
- **Prazo de Retenção e Descarte:**
  - Sessão de impersonação: Expiração automática em 30 minutos (TTL volátil).
  - Logs de auditoria forense: Retenção imutável por no mínimo 3 anos (nível High) a 5 anos (nível Critical), invioláveis e protegidos contra expurgo automatizado.

---

### Macroprocesso 05: Programa de Afiliados e Parcerias Comerciais (Referral)
- **ID da Atividade:** `ROPA-ACT-05`
- **Nome da Atividade:** Apuração de Comissões, Prevenção a Fraudes e Pagamento de Bonificações via Pix
- **Módulo Responsável:** `Modules.Referral`
- **Papel da CriaCerto:** **Controlador**
- **Finalidade do Tratamento:** Cadastramento de parceiros comerciais e produtores embaixadores, geração de links de indicação (`/r/{code}`), rastreamento de atribuição de novos clientes, apuração de comissões, validação antifraude e liquidação de repasses via Pix.
- **Bases Legais (Art. 7º LGPD):**
  - **Execução de Contrato (Art. 7º, V)**: Termos do Programa de Parcerias Comerciais e Indicação.
  - **Cumprimento de Obrigação Legal e Fiscal (Art. 7º, II)**: Escrituração de despesas comerciais, recolhimento de tributos na fonte e informes de rendimentos à Receita Federal.
  - **Exercício Regular de Direitos (Art. 7º, VI)**: Comprovação de liquidação bancária e quitação de repasses financeiros em eventuais disputas.
- **Categorias de Titulares:** Parceiros comerciais (zootecnistas, consultores pecuários, revendedores de insumos) e produtores rurais participantes.
- **Categorias de Dados Pessoais:**
  - Nome completo / Razão Social, CPF / CNPJ do parceiro.
  - Endereço de e-mail corporativo e credenciais de acesso provisórias.
  - Chave Pix (CPF, e-mail, telefone celular ou chave aleatória EVP).
  - Metadados do pagamento: Código de liquidação Pix E2E, número NSU bancário, valor do repasse e comprovante bancário digital (PDF/PNG).
- **Subprocessadores e Compartilhamento:**
  - **Instituição Financeira / Provedor Bancário Pix**: Execução da transferência monetária instantânea e conciliação bancária.
  - **Resend Inc.**: Envio de credenciais temporárias seguras, instruções de primeiro acesso e recibos de pagamento.
- **Salvaguardas de Segurança:**
  - **Criptografia Simétrica em Repouso AES-256-GCM**: A Chave Pix (`PixKey`) e o documento legal (`LegalDocumentNumber`) são gravados exclusivamente cifrados no banco de dados via envelope canônico `$cc1$` com integridade autenticada.
  - **Blind Indexing (HMAC-SHA256)**: Geração de hash indexado determinístico para permitir verificação de unicidade em $O(1)$ sem expor os dados em claro no banco.
  - **Quarentena D+30**: Período de maturação de 30 dias para impedir repasses prematuros sujeitos a estorno ou chargeback.
  - **Prevenção de Colisão de Cartão**: Bloqueio de auto-indicação e detecção de mesmo fingerprint de cartão entre padrinho e afilhado (`HasCardFingerprintCollisionAsync`).
  - **Governança 4-Eyes**: Alterações contratuais em percentuais de comissão exigem aprovação obrigatória por duplo controle administrativo (`AdminApprovalRequest`).
  - **Sanitização de Comprovantes**: Armazenamento de arquivos com validação contra path traversal e extensões perigosas.
- **Prazo de Retenção e Descarte:**
  - Dados contratuais e fiscais: 5 anos a contar do término da relação de parceria (Art. 173 do Código Tributário Nacional e Art. 206, § 5º do Código Civil).
  - Logs de comissões e transações no `AuditLog`: 5 anos com hash criptográfico SHA-256 encadeado.

---

### Macroprocesso 06: Canal Oficial do DPO e Gestão de Direitos dos Titulares (DSR)
- **ID da Atividade:** `ROPA-ACT-06`
- **Nome da Atividade:** Recepção, Protocolo e Atendimento a Solicitações de Titulares de Dados (DSR)
- **Módulos Responsáveis:** `Modules.Tenancy` (Sub-módulo Compliance DPO) e `Web.Client`
- **Papel da CriaCerto:** **Controlador** (e apoio técnico como Operador aos Produtores Rurais sob DPA)
- **Finalidade do Tratamento:** Garantir o exercício pleno de todos os direitos fundamentais garantidos pelo **Art. 18 da LGPD** (confirmação de tratamento, acesso a dados, retificação de dados incompletos/incorretos, anonimização ou eliminação de dados desnecessários, portabilidade estruturada em formato canônico, revogação de consentimento e informações sobre compartilhamento).
- **Bases Legais (Art. 7º LGPD):**
  - **Cumprimento de Obrigação Legal (Art. 7º, II)**: Dever mandatório de atendimento a solicitações de titulares no prazo improrrogável de até 15 dias úteis (Art. 19, II da LGPD) e disponibilização de canal de comunicação com o DPO (Art. 41).
  - **Exercício Regular de Direitos (Art. 7º, VI)**: Registro probatório para fins de fiscalização pela ANPD ou defesa em litígios administrativos/judiciais.
- **Categorias de Titulares:** Quaisquer pessoas físicas cadastradas ou que tenham dados tratados pela plataforma CriaCerto (produtores, colaboradores, parceiros ou visitantes).
- **Categorias de Dados Pessoais:**
  - Nome completo e endereço de e-mail do solicitante.
  - Perfil de relação com o sistema (Produtor Rural, Colaborador, Parceiro ou Outros).
  - Tipo de direito pleiteado do catálogo oficial do Art. 18.
  - Descrição da requisição e número de protocolo oficial gerado (`DSR-YYYYMMDD-XXXX`).
  - Data de submissão e prazo limite de resposta computado em 15 dias úteis.
- **Subprocessadores e Compartilhamento:**
  - **Resend Inc.**: Despacho de e-mail de confirmação ao titular com o número do protocolo e notificação simultânea à caixa postal do Encarregado (`dpo@criacerto.com.br`).
- **Salvaguardas de Segurança:**
  - Portal público dedicado em `/dpo` e `/privacidade/canal-dpo` com formulário reativo seguro (`DataSubjectRequestForm.razor`).
  - Endpoint defensivo `POST /api/v1/compliance/dpo/requests` com validação fluente de formato e proteção contra spam.
  - Registro de auditoria com severidade média no `AuditLog` e encadeamento SHA-256 (`DATA_SUBJECT_REQUEST_SUBMITTED`).
  - SLA legal estrito de 15 dias úteis com rastreamento no painel de controle do DPO.
- **Prazo de Retenção e Descarte:**
  - Registros de protocolo DSR: 5 anos a contar do encerramento da solicitação para fins de comprovação regulatória perante a ANPD.

---

### Macroprocesso 07: Resposta a Incidentes de Segurança e Notificação ANPD
- **ID da Atividade:** `ROPA-ACT-07`
- **Nome da Atividade:** Classificação de Risco, Contenção e Comunicação Regulatória de Incidentes
- **Módulos Responsáveis:** `Modules.Backoffice` (Compliance & Incident Response)
- **Papel da CriaCerto:** **Controlador** (e **Operador** com SLA B2B de 24h para notificação à Fazenda)
- **Finalidade do Tratamento:** Avaliação determinística de dano relevante, contenção técnica emergencial, comunicação tempestiva à ANPD e aos titulares afetados, e emissão de relatórios conclusivos em conformidade com o Art. 48 da LGPD e a Resolução CD/ANPD nº 15/2024.
- **Bases Legais (Art. 7º LGPD):**
  - **Cumprimento de Obrigação Legal e Regulatória (Art. 7º, II)**: Notificação compulsória à ANPD e aos titulares afetados em até 3 dias úteis a contar da ciência inequívoca (T0) e elaboração de relatório conclusivo em até 20 dias úteis.
  - **Execução de Contrato (Art. 7º, V)**: Cumprimento do SLA de notificação em até 24 horas estabelecido no DPA com clientes B2B.
- **Categorias de Titulares:** Pessoas físicas potencialmente impactadas pelo incidente de segurança.
- **Categorias de Dados Pessoais:**
  - Metadados do incidente: Cronologia (T0), classificação de severidade (P1-PRIV a P4-PRIV), tipologia de dados afetados (texto claro vs dados protegidos por AES-256-GCM), volume estimado de titulares.
  - Dados dos relatórios preliminares e conclusivos de notificação regulatória enviados à autoridade.
- **Subprocessadores e Compartilhamento:**
  - **Autoridade Nacional de Proteção de Dados (ANPD)**: Notificação oficial preliminar e relatório conclusivo.
  - **Resend Inc.**: Canal de comunicação direta por e-mail com os titulares impactados.
- **Salvaguardas de Segurança:**
  - Procedimento operacional consolidado em [RUNBOOK-07](file:///home/rony/LPR/CriaCerto/docs/operations/runbook_incident_response.md).
  - Motor analítico `PrivacyIncidentEvaluator` para determinação matemática de prazos em dias úteis e avaliação objetiva de risco.
  - Registro de auditoria forense com severidade `Critical` no `AuditLog` com hash SHA-256 encadeado.
  - Sala de crise estruturada (`#incident-privacy-war-room`) e post-mortem blameless.
- **Prazo de Retenção e Descarte:**
  - Livro de Registro de Incidentes de Segurança: Guarda permanente para efeito de histórico institucional e governança de compliance.

---

## 4. Transferência Internacional de Dados (Art. 33 LGPD)

| Fornecedor / Subprocessador | Sede / Região | Finalidade do Tratamento | Mecanismo Legal de Salvaguarda (Art. 33) |
|---|---|---|---|
| **Stripe Inc.** | Estados Unidos / União Europeia | Processamento de pagamentos, faturamento e prevenção a fraudes com cartões | Cláusulas Contratuais Padrão (SCC), Certificação PCI-DSS Level 1 e padrão internacional SOC 2. |
| **Resend Inc.** | Estados Unidos | Disparo de e-mails transacionais (ativação, redefinição de senha, DSR) | Cláusulas Contratuais Padrão (SCC), conformidade com GDPR/LGPD e certificação SOC 2 Tipo II. |
| **Provedor Cloud / PostgreSQL** | Região Brasil (Primária) / Fallback EUA | Hospedagem de infraestrutura, contêineres e banco de dados | Armazenamento de dados prioritariamente em datacenter nacional com criptografia de volume em repouso e certificações ISO 27001 e SOC 2. |

---

## 5. Medidas Técnicas e Organizacionais de Segurança da Informação (Art. 46)

O CriaCerto adota uma arquitetura em camadas de defesa em profundidade (*Defense-in-Depth*):
1. **Criptografia em Repouso**: Algoritmo simétrico autenticado **AES-256-GCM** para documentos e chaves financeiras sensíveis (`$cc1$`), e chaveiro de rotação programada.
2. **Blind Indexing**: Hashes determinísticos blindados via **HMAC-SHA256** permitindo operações de busca sem expor chaves Pix ou CPFs em logs ou no banco.
3. **Criptografia em Trânsito**: Transporte exclusivo sob **TLS 1.3** com HSTS (*HTTP Strict Transport Security*) e cabeçalhos de segurança estritos (CSP, Permissions-Policy, no-cache para áreas logadas).
4. **Trilha Forense Imutável**: Logs de auditoria estruturados com `RecordHash` encadeado ao `PreviousRecordHash` (SHA-256) em `AuditLog`, com verificação automática de integridade (`VerifyAuditTrailIntegrityQuery`).
5. **Automação de Higienização**: Expurgo automático diário de tokens e convites obsoletos (`DataRetentionPurgingBackgroundService`) e motor centralizado de redaction (`IPiiRedactor`) impedindo vazamento de senhas e dados em logs e exceções.
6. **Controle de Acesso Fino**: Princípio do Menor Privilégio via RBAC, Princípio dos Quatro Olhos (4-Eyes) para operações críticas de faturamento e planos, e isolamento de tokens de suporte.

---

## 6. Governança e Revisão do Inventário

Este inventário é mantido sob responsabilidade direta do **Encarregado pelo Tratamento de Dados Pessoais (DPO)** em estreita articulação com as equipes de Engenharia de Software e Segurança da Informação.

- **Gatilhos de Atualização Compulsória:**
  1. Criação de novo módulo arquitetural no Modular Monolith (ex: novos módulos de zootecnia ou integrações bancárias).
  2. Integração ou substituição de subprocessadores essenciais (aviso prévio de 15 dias aos clientes B2B conforme DPA).
  3. Alteração em finalidades de tratamento ou bases legais.
  4. Revisão periódica anual mandatória (próxima revisão programada: Outubro/2027).
