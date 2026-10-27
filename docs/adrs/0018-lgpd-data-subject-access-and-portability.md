# ADR 0018: Direito de Acesso e Portabilidade dos Dados Pessoais (LGPD Art. 18, II e V)

## Status
Aceito (Accepted)

## Contexto
O Artigo 18, incisos II e V da Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018) estabelece como direitos essenciais do titular dos dados:
1. **Acesso aos dados (Art. 18, II)**: O direito de obter do controlador, a qualquer momento e mediante requisição, a confirmação da existência de tratamento e o acesso transparente e integral a todos os seus dados pessoais.
2. **Portabilidade dos dados (Art. 18, V)**: O direito à portabilidade dos dados a outro fornecedor de serviço ou produto, mediante requisição expressa, de acordo com a regulamentação da autoridade nacional (ANPD), observados os segredos comercial e industrial.

No ecossistema CriaCerto (SaaS Agropecuário Multi-tenant), os usuários (produtores, veterinários, operadores e parceiros comerciais) necessitam de um mecanismo de autoatendimento ágil, interoperável e auditável para exportar seus dados pessoais cadastrais, histórico de consentimentos, associações a granjas e trilha de eventos no sistema, sem dependência de solicitações manuais ao suporte ou ao Encarregado (DPO).

Além disso, a entrega do pacote de portabilidade deve ser inviolável e rastreável, gerando prova imutável de cumprimento da obrigação legal em conformidade com o princípio da responsabilização e prestação de contas (*accountability* - Art. 6º, X).

## Decisão

1. **Estrutura Canônica do Dossiê de Portabilidade (`DataSubjectPortabilityDossierDto`)**:
   - O pacote de dados é emitido em formato canônico interoperável estruturado (**JSON - RFC 8259**, codificação UTF-8 indentada).
   - O arquivo exportado (`meus-dados-lgpd-{userId}-{timestamp}.json`) é composto por:
     - **Metadados do Dossiê**: Especificação legal (LGPD Art. 18, II e V), finalidade, identificação da controladora (*CriaCerto Agropecuária Digital Ltda.*) e timestamp UTC da emissão.
     - **Perfil Pessoal do Titular (`UserDataExportDto`)**: Identificador único, nome completo, e-mail, telefone, confirmação de e-mail e data de cadastro da conta (`CreatedAtUtc`). Hashes de senha e tokens temporários são estritamente omitidos por segurança.
     - **Histórico de Consentimento (`ConsentMetadataExportDto`)**: Versões dos Termos de Uso e Política de Privacidade aceitas, carimbos de data/hora UTC e endereço IP registrado no momento do aceite.
     - **Vínculos Organizacionais (`TenantAffiliationExportDto`)**: Identificador e nome de cada fazenda associada, papel exercido (RBAC), data de ingresso e plano contratado.
     - **Trilha Resumida de Auditoria do Titular (`UserAuditTrailEntryExportDto`)**: Eventos de governança do usuário (aceites de termos, retificações cadastrais, alterações de credenciais e exportações anteriores) registrados na plataforma.

2. **Isolamento Modular e Inversão de Dependências (Modular Monolith)**:
   - Em conformidade com [`agents.md`](file:///home/rony/LPR/CriaCerto/agents.md), o módulo `Modules.Tenancy` não referencia diretamente o contexto de banco de dados ou entidades de `Modules.Backoffice`.
   - A obtenção da trilha histórica de auditoria é realizada através da abstração `IUserAuditTrailLookupService` declarada em `CriaCerto.Modules.Tenancy.Application.Abstractions`.
   - A camada Host (`CriaCerto.Api.Services.TenancyBackofficeAuditSink`) implementa `IUserAuditTrailLookupService` e `ITenancyAuditSink`, efetuando consultas indexadas em `BackofficeDbContext.AuditLogs`.

3. **Garantia Forense e Hash Encadeado SHA-256**:
   - Toda emissão de dossiê de portabilidade dispara compulsoriamente `ITenancyAuditSink.RecordDataSubjectPortabilityExportAuditAsync`.
   - Um registro imutável é gravado no `AuditLog` com `Action = "DATA_SUBJECT_PORTABILITY_EXPORTED"`, `Category = AuditCategory.Compliance`, `Severity = AuditSeverity.High` e `Resource = $"Users/{userId}"`.
   - O registro é encadeado na cadeia de custódia criptográfica com hash SHA-256 (`RecordHash`), garantindo não-repúdio e rastreabilidade perante auditorias da ANPD.

4. **Endpoint RESTful Autenticado (`CriaCerto.Api`)**:
   - `GET /api/v1/auth/my-data/export`:
     - Exige autenticação JWT válida (Bearer Token).
     - Extrai deterministicamente o identificador do titular a partir das claims de identidade (`ClaimTypes.NameIdentifier` / `sub` / `UserId`).
     - Resolve o IP de origem do cliente considerando cabeçalhos de proxy reverso (`X-Forwarded-For`).
     - Retorna o arquivo com `Content-Type: application/json` e `Content-Disposition: attachment; filename="meus-dados-lgpd-{userId}-{timestamp}.json"`.
     - Isento de restrição de tenant no middleware de tenancy, garantindo autoatendimento para usuários em qualquer status.

5. **Interface de Autoatendimento no Blazor Web App (WASM)**:
   - Disponibilizado o Card de **Acesso & Portabilidade de Dados Pessoais** em [`ProfileManagement.razor`](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Settings/ProfileManagement.razor) (`/settings/profile` e `/settings/privacy`).
   - Apresenta os pilares técnicos: formato canônico JSON RFC 8259, segurança criptográfica SHA-256 e download instantâneo.
   - Botão interativo **"Exportar Meus Dados Pessoais (Portabilidade LGPD)"** com estados reativos de carregamento e acionamento nativo de download via `downloadFileFromByteArray`.
   - Inclusão do atalho **"Privacidade & Portabilidade (LGPD)"** no catálogo de navegação lateral (`NavigationCatalog.cs`).

6. **Estratégia de Testes (TDD & Regressão)**:
   - Suíte unitária em `ExportUserDataPortabilityQueryHandlerTests` cobrindo cenários de sucesso, integridade do JSON gerado, omissão de credenciais secretas, usuário inexistente (`Result.Failure(Error.NotFound)`) e acionamento do sink de auditoria.
   - Suíte de integração forense em `UserProfileAndSecurityAuditIntegrationTests` validando a gravação de `DATA_SUBJECT_PORTABILITY_EXPORTED`, encadeamento do hash SHA-256 e recuperação da trilha do titular.
   - Suíte de testes do cliente Web em `TenancyApiClientTests` cobrindo a execução de download de bytes com headers `Content-Disposition` e tratamento de erros.

## Consequências

- **Positivas**:
  - Cumprimento integral e autônomo dos Artigos 18, II e 18, V da LGPD.
  - Formato interoperável padronizado permitindo ingestão automática por outros sistemas.
  - Segurança aprimorada: credenciais sensíveis (senhas e tokens) nunca são expostas no arquivo gerado.
  - Rastreabilidade forense total da entrega com cadeia criptográfica SHA-256 inviolável.
  - Zero acoplamento entre os esquemas de banco `tenancy` e `backoffice`.
- **Negativas / Limitações**:
  - O volume de registros da trilha é delimitado aos últimos 100 eventos para manter tempos de resposta submétricos e arquivos leves; volumes maiores permanecem sob demanda judicial/regulatória via `ExportAccessTrailQuery` no Backoffice.
