# ADR 0026: Formalização do Inventário de Dados (ROPA - Art. 37) e Relatório de Impacto (RIPD - Art. 38)

## Status
Aceito (Accepted)

## Contexto
O ecossistema **CriaCerto Bovino SaaS** opera como plataforma multi-tenant em nuvem, manipulando dados cadastrais, credenciais, movimentações financeiras e registros de manejos pecuários. A Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018) estabelece obrigações documentais e de governança corporativa indelegáveis para operadores e controladores:

1. **Art. 37 da LGPD**: O controlador e o operador devem manter o **Registro das Operações de Tratamento de Dados Pessoais (ROPA - Record of Processing Activities)**, contendo a descrição das categorias de dados tratados, finalidades, bases legais, compartilhamento com subprocessadores, medidas de segurança e prazos de retenção.
2. **Art. 5º, XVII e Art. 38 da LGPD**: A autoridade nacional (ANPD) pode determinar a elaboração e apresentação do **Relatório de Impacto à Proteção de Dados Pessoais (RIPD / DPIA)** para operações de tratamento que apresentem maior risco às liberdades civis e aos direitos fundamentais, notadamente processos fundamentados no legítimo interesse ou que envolvam dados sensíveis e acessos privilegiados.

Na ausência de uma documentação viva, formalizada e rastreável:
- A organização ficava sujeita a penalidades administrativas por infração ao dever de prestação de contas (*Accountability* - Art. 6º, X da LGPD) em caso de requisição ou fiscalização pela ANPD.
- Não havia um catálogo único consolidando o mapeamento ponta a ponta das operações técnicas dos módulos (`Tenancy`, `Breeding`, `Calving`, `Growth`, `Nutrition`, `Sanitary`, `Referral` e `Backoffice`).
- Faltava atestação formal e documentada de que funcionalidades de alto privilégio (como a impersonação de suporte e o processamento de pagamentos com Stripe e chaves Pix de parceiros) possuem salvaguardas que reduzem o risco inerente para níveis residuais baixos e aceitáveis.

## Decisão

Instituiu-se a formalização documental mandatória e contínua do inventário de dados e do relatório de impacto à proteção de dados:

### 1. Registro das Operações de Tratamento — ROPA (`docs/compliance/ropa_data_inventory.md`)
Adotou-se o inventário estruturado cobrindo os 7 macroprocessos da plataforma:
- **ROPA-ACT-01**: Onboarding, Cadastro e Autenticação de Titulares.
- **ROPA-ACT-02**: Cobrança, Assinatura de Planos e Faturamento SaaS (Stripe).
- **ROPA-ACT-03**: Gestão Zootécnica, Sanitária e Operação do Rebanho (CriaCerto como Operador sob DPA).
- **ROPA-ACT-04**: Suporte Assistido, Governança Backoffice e Impersonação.
- **ROPA-ACT-05**: Programa de Afiliados e Parcerias Comerciais (Referral).
- **ROPA-ACT-06**: Canal Oficial do DPO e Atendimento a Solicitações dos Titulares (DSR).
- **ROPA-ACT-07**: Resposta a Incidentes de Segurança e Notificação ANPD (RUNBOOK-07 & Art. 48).

Para cada macroprocesso, consolidaram-se as finalidades, bases legais do Art. 7º, categorias de titulares e dados, subprocessadores autorizados (Stripe, Resend, Provedor Cloud), transferências internacionais (Art. 33), salvaguardas técnicas (AES-256-GCM, HMAC-SHA256, hash encadeado SHA-256, RBAC) e prazos legais de retenção/descarte (Marco Civil, CTN e Código Civil).

### 2. Relatório de Impacto à Proteção de Dados Pessoais — RIPD (`docs/compliance/ripd_dpia_overview.md`)
Elaborou-se o RIPD detalhado com metodologia aderente ao Guia Orientativo da ANPD, focando nas duas áreas de maior risco da aplicação:
- **Impersonação de Suporte**: Avaliação dos riscos de abuso de privilégios e elevação de acesso, neutralizados pelas salvaguardas de Dupla Salvaguarda (ticket + justificativa de min. 10 caracteres), bloqueio de endpoints administrativos por token de impersonação em `BackofficeAccessMiddleware`, mascaramento contextual dinâmico (`IPiiDataMasker`), trilha forense imutável com encadeamento de hash SHA-256 no `AuditLog`, circuit breaker com corte imediato em < 1s ([RUNBOOK-01](file:///home/rony/LPR/CriaCerto/docs/operations/runbook_incident_response.md)) e detecção analítica de anomalias (`AnomalyDetectionEngine`).
- **Processamento Financeiro e Afiliados (Stripe & Referral)**: Avaliação dos riscos de vazamento de dados de cartão e dados bancários, neutralizados por isolamento PCI-DSS Level 1 terceirizado via Stripe Elements, criptografia em repouso **AES-256-GCM** (`$cc1$`) para Chaves Pix e CPFs, **Blind Indexing (HMAC-SHA256)** para busca $O(1)$, carência estrita D+30 contra estornos, motor antifraude de fingerprint de cartão e aprovação 4-Eyes para contratos.
- **Matriz de Riscos Consolidada**: Demonstração quantitativa da redução de todos os riscos inerentes de patamares Médio/Alto para o nível **BAIXO**.
- **Parecer Conclusivo do DPO**: Homologação formal pelo Encarregado de Dados.

### 3. Living Documentation e Garantia Contínua por Testes
- A conformidade do ROPA e RIPD é integrada à suíte de testes automatizados (`LgpdComplianceDocumentationTests`), validando a existência, completude dos macroprocessos essenciais, salvaguardas técnicas e canais do DPO no pipeline de CI/CD.

## Consequências

### Positivas
- **Conformidade Regulatória Plena**: Atendimento integral aos Artigos 37 e 38 da LGPD e aos requisitos de auditorias de segurança e certificações (ISO 27001, ISO 27701, SOC 2).
- **Accountability e Transparência**: Registro claro das operações e bases legais para consulta imediata em fiscalizações da ANPD ou questionamentos de clientes empresariais B2B.
- **Segurança Jurídica**: Mitigação comprovada de riscos nas funcionalidades de maior exposição através de salvaguardas técnicas de ponta a ponta.
- **Sinergia com o DPA e Runbook de Incidentes**: Encadeamento coerente entre o contrato B2B ([ADR 0022](file:///home/rony/LPR/CriaCerto/docs/adrs/0022-lgpd-b2b-data-processing-agreement-dpa.md)), o canal do DPO ([ADR 0024](file:///home/rony/LPR/CriaCerto/docs/adrs/0024-lgpd-dpo-communication-channel-and-dsr-workflow.md)), o protocolo de incidentes ([ADR 0025](file:///home/rony/LPR/CriaCerto/docs/adrs/0025-lgpd-incident-response-and-anpd-notification.md)) e a gestão de inventário.

### Negativas / Mitigações
- Exige governança contínua e atualização compulsória pelo DPO e líderes técnicos sempre que novos módulos ou integrações forem incorporados ao software.
