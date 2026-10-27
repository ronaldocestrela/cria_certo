# ADR 0024: Canal Oficial do Encarregado (DPO - Art. 41) e Fluxo de Atendimento a Requisições de Titulares (DSR)

## Status
Aceito (Accepted)

## Contexto
O Artigo 41 da Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018) determina expressamente que o controlador de dados deve indicar formalmente um Encarregado pelo Tratamento de Dados Pessoais (Data Protection Officer - DPO) e divulgar sua identidade e informações de contato publicamente, de forma clara e objetiva, preferencialmente em seu sítio eletrônico (§ 1º).

Adicionalmente, os Artigos 18 e 19 da LGPD conferem aos titulares de dados (produtores rurais, operadores de fazenda, parceiros comerciais e visitantes) uma gama de direitos fundamentais — incluindo confirmação de tratamento, acesso, retificação, anonimização, bloqueio, eliminação, portabilidade, esclarecimento sobre compartilhamentos e revogação de consentimentos —, estipulando no Art. 19, II que o atendimento formal deve ocorrer no prazo legal improrrogável de até **15 (quinze) dias úteis**.

Até então, o CriaCerto mantinha a identificação preliminar do canal via e-mail e componentes isolados de retificação, portabilidade e anonimização autenticados no painel do produtor. Contudo, faltava um canal público institucional centralizado (`/dpo`), com formulário de Data Subject Request (DSR) capaz de atender tanto titulares ativos quanto ex-colaboradores ou terceiros não autenticados, munido de geração automática de protocolo, notificação transacional com prazo legal de resposta (15 dias úteis) e trilha forense imutável com encadeamento criptográfico SHA-256 no `AuditLog`.

## Decisão

Implementou-se a infraestrutura institucional e técnica completa para o Canal do Encarregado (DPO) e fluxo de solicitações DSR sob o padrão Modular Monolith:

### 1. Modelagem CQRS e Domínio no Módulo Tenancy
- **Contratos e Enums**:
  - `DataSubjectRole`: qualificação do titular perante a plataforma (`ProducerAdmin`, `FarmTeamMember`, `CommercialPartner`, `VisitorOrCandidate`, `Other`).
  - `DataSubjectRequestType`: catálogo exhaustivo dos direitos do Artigo 18 da LGPD (`AccessAndConfirmation`, `Rectification`, `AnonymizationOrDeletion`, `Portability`, `ThirdPartySharingInfo`, `ConsentRevocation`, `GeneralInquiryOrAnpdIssue`).
  - `DataSubjectRequestReceiptDto`: comprovante oficial com protocolo canônico, data limite de 15 dias úteis calculada, e-mail oficial do DPO (`dpo@criacerto.com.br`) e orientações legais.
- **Comando & Validação Fluente**:
  - `SubmitDataSubjectRequestCommand` e `SubmitDataSubjectRequestCommandValidator` garantindo higienização de e-mails, tamanho de nome e detalhamento de solicitação (mínimo de 20 caracteres) sem propagação de exceptions de fluxo (`Result Pattern`).
- **Handler & Resiliência Transacional**:
  - `SubmitDataSubjectRequestCommandHandler`:
    - Geração determinística de número de protocolo canônico `DSR-YYYYMMDD-XXXX`.
    - Algoritmo de cálculo de prazo útil (`CalculateBusinessDaysDeadline`) avançando 15 dias úteis (desconsiderando sábados e domingos).
    - Disparo duplo de e-mail via `IEmailSender` (Resend): (1) recibo formal de confirmação ao titular e (2) alerta imediato para a caixa postal oficial do DPO (`dpo@criacerto.com.br`). Falhas transitórias no provedor de e-mail são registradas em log sem abortar a emissão do protocolo ao solicitante.
    - Gravação compulsória de evento de auditoria no `AuditLog` com severidade média (`Category = Compliance, Action = "DATA_SUBJECT_REQUEST_SUBMITTED"`), dados estruturados em JSON sanitizado e integridade garantida por encadeamento de hash SHA-256.

### 2. Exposição de Endpoint REST Defensivo no Host API
- Mapeado endpoint público `POST /api/v1/compliance/dpo/requests` em `Program.cs`.
- Captura de metadados contextuais de rede (`RemoteIpAddress` com suporte a `X-Forwarded-For` e `UserAgent`).
- Retorno estritamente padronizado com `Result Pattern` (`StatusCodes.Status201Created` em caso de sucesso).

### 3. Portal Público do DPO e Componentes Blazor (Web Client)
- **Portal Institucional (`/dpo`, `/privacidade/canal-dpo`, `/privacidade/dpo`)**:
  - Página `DpoPortal.razor` sob o `LegalLayout.razor`, expondo a identidade formal do Encarregado, endereço físico, prazos de resposta (15 dias úteis), atribuições conforme Art. 41 § 2º da LGPD e catálogo de direitos.
- **Formulário Reutilizável de Requisição (`DataSubjectRequestForm.razor`)**:
  - Formulário com tipografia moderna, contador de caracteres em tempo real, validações dinâmicas e painel de sucesso com número de protocolo copiável e orientações de acompanhamento.
- **Integração no Ecossistema**:
  - Link destacado na barra de navegação superior e rodapé de `LegalLayout.razor`.
  - Atualização dos links de rodapé na Landing Page `Home.razor` apontando para `/dpo` e `/dpa`.
  - Adição de link de requisição formal no card de Governança LGPD em `ProfileManagement.razor`.
  - Catálogo de documentos atualizado em `LegalDocumentCatalog` (`LegalModels.cs`).

## Consequências

- **Positivas**:
  - Conformidade plena e auditável com o Artigo 41 (§ 1º e § 2º) e Artigo 19, II da LGPD.
  - Segurança jurídica e rastreabilidade total: cada manifestação de titular gera protocolo oficial instantâneo e registro imutável com encadeamento de hash SHA-256 no `AuditLog`.
  - Acessibilidade universal: permite a titulares ativos ou desligados (que não possuem login) exercerem seus direitos de privacidade com simplicidade.
  - Agilidade na resposta: o DPO é notificado imediatamente no e-mail com todas as informações requeridas para abertura do dossiê interno.
- **Negativas / Limitações**:
  - Como o canal é aberto ao público em geral, requer monitoramento contínuo da caixa postal `dpo@criacerto.com.br` e triagem contra solicitações duplicadas ou infundadas.
