# ADR 0011: Compliance LGPD, Mascaramento Contextual Progressivo e Governança de Acesso a Dados Pessoais

## Status
Aceito (Accepted)

## Contexto
O SaaS CriaCerto gerencia operações pecuárias de produtores rurais constituídos tanto como Pessoas Jurídicas (CNPJ) quanto Pessoas Físicas (CPF com Inscrição Estadual). No Backoffice administrativo, operadores de suporte (N1 e N2), financeiro e auditoria interagem com informações cadastrais de fazendas, proprietários técnicos e comerciais, dados de contato e trilhas de transações. Sob a égide da **Lei Geral de Proteção de Dados (LGPD - Lei Federal nº 13.709/2018)** e frameworks de segurança corporativa (SOC 2, ISO 27701):
1. **Princípio da Minimização e Necessidade (Art. 6º, I e III)**: Operadores não devem ser expostos a dados pessoais não essenciais para o exercício de sua função específica.
2. **Segurança e Confidencialidade (Art. 6º, VII)**: Dados cadastrais sensíveis (CPF, CNPJ, e-mails de titulares, telefones, endereços IP) devem ser protegidos por padrão sob redaction progressivo.
3. **Prestação de Contas e Rastreabilidade Compulsória (Art. 6º, X e Art. 37)**: Qualquer desmascaramento em claro (*unmask*) ou exportação de dossiê de acesso deve ser precedido de justificativa operacional formal e auditado forense e criptograficamente (SHA-256 encadeado).
4. **Atendimento a Titulares e Fiscalizações Regulatórias (Art. 18 e 19)**: O SaaS deve dispor de mecanismo seguro para emissão estruturada da trilha de acesso e tratamento de dados de qualquer titular para fins de auditoria interna, externa ou fiscalização da ANPD.

## Decisão
Implementar a camada de **Compliance LGPD, Mascaramento Contextual e Governança de Acesso** no módulo `Modules.Backoffice`:

1. **Serviço Centralizado de Mascaramento (`IPiiDataMasker` / `PiiDataMasker`)**:
   - Algoritmos determinísticos e sem vazamento de informação:
     - **CPF**: `***.456.789-**` (preserva miolo, protege início e dígitos verificadores).
     - **CNPJ**: `12.***.***/0001-**` (preserva raiz e sufixo de filial operacional, mascara bloco intermediário e controle).
     - **E-mail**: `u***a@dominio.com.br` (preserva domínio corporativo e mascara parte local).
     - **Telefone**: `(11) 9****-**21` (preserva DDD e final).
     - **IP**: `192.168.***.***` ou `2804:14d:***` (mascara octetos de host).
     - **Higienização JSON**: Redaction recursivo em payloads que contenham chaves de senhas, tokens ou dados pessoais.

2. **Governança Granular de Acesso RBAC**:
   - Novas permissões no catálogo `BackofficePermissions`:
     - `compliance.read`: Visualização de relatórios, métricas de privacidade e histórico de acessos.
     - `compliance.export`: Emissão de Dossiês Formais de Acesso para fiscalizações e auditorias externas.
     - `compliance.unmask`: Permissão para solicitar desmascaramento Just-In-Time com justificativa.
   - Atribuição restrita por papel (`BackofficeRoles`):
     - `PlatformOwner`: Acesso irrestrito auditado.
     - `ReadOnlyAuditor`: Possui `compliance.read` e `compliance.export`, porém visualiza dados mascarados (`compliance.unmask` negado por padrão).
     - `SupportN1`: Nenhum privilégio de compliance (visualização 100% sob redaction).
     - `SupportN2`: Pode consultar contexto de chamados, com unmask bloqueado ou condicionado a sessão ativa.

3. **Desmascaramento Just-In-Time Auditado (`RevealSensitiveDataCommand`)**:
   - Endpoint seguro `POST /api/v1/backoffice/compliance/reveal-pii`.
   - Exigência imperativa de justificativa operacional com validação mínima de 10 caracteres.
   - Persistência imediata de registro forense em `AuditLog`:
     - `Category = AuditCategory.Compliance`
     - `Action = "PII_DATA_UNMASKED"`
     - `Severity = AuditSeverity.High`
     - Assinado com hash canônico SHA-256 encadeado (`RecordHash`, `PreviousRecordHash`).

4. **Dossiê Formal de Acesso e Auditoria Regulatória (`ExportAccessTrailQuery`)**:
   - Endpoint `GET /api/v1/backoffice/compliance/access-trail/export`.
   - Gera exportação canônica (CSV ou JSON) com metadados do auditor, finalidade declarada e carimbo de tempo UTC.
   - Grava evento de auditoria com severidade crítica (`Action = "COMPLIANCE_DOSSIER_EXPORTED"`, `Severity = AuditSeverity.Critical`).

5. **Experiência Visual de Redaction Progressivo no Blazor (.NET 10)**:
   - Componente atômico `MaskedDataField.razor` integrado em `TenantsManagement.razor` e `SupportWorkbench.razor`.
   - Modal de confirmação e justificativa `RevealPiiModal.razor`.
   - Console dedicado `/backoffice/compliance` (`ComplianceGovernance.razor`) com 4 KPIs operacionais, explorer da trilha de acesso, gerador de dossiês e matriz executiva de privilégios.

## Consequências
- **Positivas**:
  - Conformidade plena com os princípios de minimização, segurança e prestação de contas da LGPD.
  - Mitigação de vazamento acidental de dados pessoais em telas de suporte e auditoria.
  - Rastreabilidade forense de 100% dos eventos de visualização de dados pessoais em formato original.
  - Facilidade na emissão de evidências para auditorias externas (ISO 27701, SOC 2) e ANPD.
- **Negativas**:
  - Operadores autorizados realizam uma etapa adicional (justificativa no modal) para visualizar dados em claro quando necessário.
