# ADR 0022: Formalização Contratual B2B de Processamento de Dados (DPA - Data Processing Agreement)

## Status
Aceito (Accepted)

## Contexto
A operação da plataforma **CriaCerto Bovino SaaS** envolve duas relações jurídicas distintas perante a Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018):

1. **Relação B2C/Direta**: A CriaCerto figura como **Controladora** (Art. 5º, VI) dos dados cadastrais, credenciais e dados financeiros dos titulares das contas (produtores rurais, gestores da fazenda e parceiros comerciais). Esta relação é disciplinada diretamente pelos [Termos de Uso](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Legal/TermsOfService.razor) e pela [Política de Privacidade](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Pages/Legal/PrivacyPolicy.razor).
2. **Relação B2B/Subcontratada**: Quando a fazenda utiliza recursos colaborativos como convites de equipe (`TeamInvite`), designação de operadores de curral, vaqueiros e veterinários responsáveis por manejos e vacinações com carência sanitária, **o Produtor Rural atua como Controlador** perante seus empregados e prestadores de serviços. Por sua vez, a CriaCerto atua como **Operadora** (Art. 5º, VII e Art. 39), realizando o tratamento de dados pessoais estritamente em nome e segundo as instruções da fazenda.

Sem um instrumento formal de aditivo contratual de processamento de dados (DPA):
- A delimitação de responsabilidade civil e administrativa entre Controlador e Operador ficava vulnerável em eventuais autuações da ANPD ou litígios trabalhistas/civis de colaboradores.
- Faltava previsão formal para o dever de assistência do CriaCerto no atendimento a requisições de titulares (Art. 18 da LGPD) e fluxo de notificação mútua em incidentes de segurança (Art. 48).
- As obrigações de salvaguardas técnicas (criptografia em repouso AES-256-GCM, blind indexing, isolamento multi-tenant) e prazos de descarte seguro pós-rescisão não estavam contratualmente consolidadas para os clientes B2B.

## Decisão

Instituiu-se o **Termo de Processamento de Dados Pessoais (DPA - Data Processing Agreement)** como anexo vinculante e indissociável aos Termos de Uso da Plataforma CriaCerto Bovino SaaS, acessível publicamente via `/dpa` e `/aditivo-processamento-dados`:

### 1. Qualificação dos Papéis e Escopo do Tratamento (Art. 5º e Art. 39)
- **Controlador**: O Produtor Rural / Fazenda contratante, titular da conta e responsável pela legalidade das bases legais da inserção de dados de seus empregados e operadores.
- **Operador**: A CriaCerto, obrigada a processar os dados exclusivamente para execução do SaaS e segundo as instruções documentadas do Controlador, vedada a utilização para fins publicitários ou cessão a terceiros.
- **Dados Cobertos**: Registros de convites (`TeamInvite`), e-mails, nomes, perfis de permissão zootécnica e assinaturas de lançamentos operacionais.

### 2. Salvaguardas Técnicas e Administrativas Vinculantes (Art. 46)
- Criptografia simétrica autenticada **AES-256-GCM** em repouso e chaveiro rotativo gerenciado para documentos e PII sensível.
- **Blind Indexing (HMAC-SHA256)** para validações de unicidade e buscas em $O(1)$ sem expor PII.
- **Trilha Forense Imutável**: Hashes SHA-256 encadeados por registro para prova de integridade e não repúdio de operações administrativas.
- **Isolamento Multi-Tenant**: Barreiras lógicas que segregam absolutamente os dados zootécnicos e de operadores entre organizações distintas.

### 3. Governança de Subprocessadores Essenciais
- Autorização genérica do Produtor condicionada à exigência de níveis equivalentes de salvaguarda e aviso prévio de 15 dias para alterações em subprocessadores de infraestrutura:
  - **Provedor Cloud / PostgreSQL**: Hospedagem de banco de dados e volumes criptografados (ISO 27001 / SOC 2).
  - **Resend Inc.**: Disparo de e-mails transacionais de convite (`TeamInvite`) e autenticação (SOC 2 Tipo II).
  - **Stripe Inc.**: Cobrança e conciliação de faturas do SaaS (PCI-DSS Nível 1).

### 4. Assistência Técnica e Gestão de Incidentes (Art. 18 e Art. 48)
- **Atendimento a Titulares**: Suporte da CriaCerto em até 5 dias úteis para pedidos complexos de exclusão ou portabilidade submetidos à fazenda por seus colaboradores, somado aos recursos de gestão self-service no painel de equipe.
- **Gestão de Incidentes**: Obrigação da CriaCerto em notificar o Produtor Rural com celeridade sobre incidentes relevantes confirmados, municiando o Controlador com subsídios técnicos detalhados para comunicação à ANPD e aos titulares.
- **Ciclo de Descarte**: Carência garantida de 30 dias para download/exportação do banco de dados da fazenda após rescisão, seguido de expurgo irreversível de dados em produção, resguardadas apenas obrigações legais (Marco Civil / CTN).

### 5. Integração na Aplicação e Living Documentation
- Modelagem no catálogo centralizado `LegalModels.cs` (`LegalDocumentCatalog.GetDataProcessingAgreementSections()`), página Blazor dedicada `DataProcessingAgreement.razor`, links de cabeçalho/rodapé em `LegalLayout.razor`, referências cruzadas em `TermsOfService.razor` e consentimento explícito no cadastro em `Register.razor`.
- Cobertura por testes unitários contínuos em `LegalPagesTests.cs`.

## Consequências

- **Positivas**:
  - Segurança jurídica plena para clientes agropecuários empresariais que necessitam de conformidade formal com a LGPD para auditorias de certificação (ex: ESG, rastreabilidade bovina, exportação).
  - Atendimento estrito aos artigos 39, 46 e 48 da Lei nº 13.709/2018.
  - Alinhamento total com as práticas recomendadas da ANPD e padrões internacionais de contratos de processamento de dados (DPA B2B).
  - Transparência total sobre subprocessadores autorizados e medidas de segurança.
- **Negativas / Limitações**:
  - Necessidade de manter a lista de subprocessadores atualizada e notificar os clientes em caso de novas contratações ou substituições de provedores fundamentais.
