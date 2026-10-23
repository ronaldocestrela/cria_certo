# Módulo de Indicações e Parceiros Comerciais (`Modules.Referral`)

## Visão Geral
O módulo `Modules.Referral` é responsável por orquestrar o **Programa de Indicação do CriaCerto**, cobrindo tanto clientes ativos (programa "Indique e Ganhe" com descontos em fatura) quanto parceiros comerciais (consultores, veterinários, revendas e associações com repasse de comissão financeira).

O módulo opera em nível de plataforma (Master Context), no banco de dados principal (`criacerto_foundation`), com isolamento no schema SQL dedicado `[referral]`.

---

## 1. Arquitetura e Limites de Fronteira

* **Estilo Arquitetural:** Monólito Modular com Clean Architecture e Domain-Driven Design (DDD).
* **Isolamento de Banco:** Mapeado no schema `[referral]` com controle de migrações em `[referral].[__EFMigrationsHistory]`.
* **Fronteiras Modulares:** Referências a outros módulos (ex: `TenantId`, `UserId` do módulo `Tenancy`, e `PlanCatalogId` do `Backoffice`) são mantidas estritamente como tipos de valor (`Guid`), sendo proibidas chaves estrangeiras ou navegações físicas diretas cross-module via Entity Framework Core.
* **Error & Response Pattern:** 100% de conformidade com o `Result<T>` pattern.

---

## 2. Entidades de Domínio e Tabelas do Schema `[referral]`

### 2.1 `ReferralProgramParticipant` (`[referral].[Participants]`)
Representa o participante ativo ou suspenso no programa. Pode ser um cliente (`TenantId` preenchido) ou um parceiro comercial cadastrado pela administração (`CreatedByAdminId` preenchido, `TenantId` nulo).

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `TenantId`: `uniqueidentifier?` (opcional, preenchido para clientes)
  - `UserId`: `uniqueidentifier?` (opcional)
  - `ParticipantType`: `nvarchar(30)` (`Customer` | `CommercialPartner`)
  - `Status`: `nvarchar(30)` (`Active` | `Suspended`)
  - `SuspensionReason`: `nvarchar(500)?`
  - `CreatedByAdminId`: `uniqueidentifier?`
  - `CreatedAtUtc`: `datetime2`
  - `UpdatedAtUtc`: `datetime2?`
* **Índices**: `TenantId`, `UserId`, `Status`, `ParticipantType`, `CreatedAtUtc`.

### 2.2 `ReferralCode` (`[referral].[ReferralCodes]`)
Código alfanumérico ou slug único para geração de links rastreáveis (`?ref=CODIGO`).

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `ParticipantId`: `uniqueidentifier` (FK interna para `Participants.Id`)
  - `Code`: `nvarchar(50)` (único, normalizado em maiúsculas)
  - `CampaignTitle`: `nvarchar(150)?` (identificador de canal/campanha)
  - `ClicksCount`: `bigint` (contador de acessos ao link)
  - `IsActive`: `bit` (default `true`)
  - `CreatedAtUtc`: `datetime2`
* **Índices**: Unique index em `Code`, índices em `ParticipantId` e `IsActive`.

### 2.3 `PartnerContract` (`[referral].[PartnerContracts]`)
Contratos comerciais individualizados para parceiros, com histórico de versões e vigência.

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `ParticipantId`: `uniqueidentifier` (FK interna para `Participants.Id`)
  - `CommissionType`: `nvarchar(40)` (`PercentageRecurring`, `PercentageFirstMonth`, `FixedPerConversion`)
  - `CommissionValue`: `decimal(18, 4)`
  - `MaxRecurringMonths`: `int?`
  - `PaymentTermsDays`: `int` (default `30` dias)
  - `PixKey`: `nvarchar(150)`
  - `PixKeyType`: `nvarchar(30)` (`Cpf`, `Cnpj`, `Email`, `Phone`, `RandomKey`)
  - `LegalDocumentNumber`: `nvarchar(30)` (CPF ou CNPJ)
  - `ValidFromUtc`: `datetime2`
  - `ValidToUtc`: `datetime2?` (nulo enquanto vigente)
  - `IsActive`: `bit`
  - `CreatedAtUtc`: `datetime2`
* **Índices**: `ParticipantId`, `IsActive`, índice composto `(ValidFromUtc, ValidToUtc)`.

### 2.4 `PlanReferralDiscountPolicy` (`[referral].[PlanDiscountPolicies]`)
Parametrização das políticas de desconto concedidas aos clientes por indicação ativa com base no plano contratado.

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `PlanCatalogId`: `uniqueidentifier`
  - `PlanCode`: `nvarchar(50)`
  - `DiscountType`: `nvarchar(30)` (`FixedValue`, `Percentage`)
  - `DiscountValue`: `decimal(18, 2)`
  - `MaxDiscountCapPercentage`: `decimal(5, 2)` (default `100.00%`)
  - `IsActive`: `bit`
  - `CreatedAtUtc`: `datetime2`
* **Índices**: `PlanCatalogId`, `PlanCode`, `IsActive`.

### 2.5 `ReferralAttribution` (`[referral].[Attributions]`)
Rastreia a atribuição da indicação originada em onboarding ou checkout.

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `ReferralCodeId`: `uniqueidentifier` (FK interna para `ReferralCodes.Id`)
  - `ReferrerParticipantId`: `uniqueidentifier` (FK interna para `Participants.Id`)
  - `ReferredTenantId`: `uniqueidentifier` (Tenant que se cadastrou)
  - `AttributionDateUtc`: `datetime2`
  - `ConvertedAtUtc`: `datetime2?` (preenchido após confirmação do 1º pagamento)
  - `Status`: `nvarchar(30)` (`Pending`, `Converted`, `Churned`, `Fraudulent`)
  - `OriginIpAddress`: `nvarchar(45)?`
  - `UserAgent`: `nvarchar(500)?`
  - `DeviceFingerprintHash`: `nvarchar(128)?`
  - `FraudReason`: `nvarchar(500)?`
* **Índices**: `ReferredTenantId`, `ReferralCodeId`, `ReferrerParticipantId`, `Status`, `AttributionDateUtc`, índice composto `(ReferredTenantId, Status)`.

### 2.6 `ReferralRewardLedger` (`[referral].[RewardLedger]`)
Livro-razão contábil e imutável para controle de créditos em fatura e comissões a pagar.

* **Colunas**:
  - `Id`: `uniqueidentifier` (PK)
  - `ReferralAttributionId`: `uniqueidentifier` (FK interna para `Attributions.Id`)
  - `ParticipantId`: `uniqueidentifier` (FK interna para `Participants.Id`)
  - `RewardType`: `nvarchar(30)` (`InvoiceDiscountCredit`, `CashCommission`)
  - `GrossAmount`: `decimal(18, 2)`
  - `NetAmount`: `decimal(18, 2)`
  - `Currency`: `nvarchar(3)` (default `BRL`)
  - `Status`: `nvarchar(30)` (`PendingMaturity`, `AvailableForPayout`, `AppliedToInvoice`, `PaidOut`, `Cancelled`)
  - `MaturityDateUtc`: `datetime2?` (D+30 para comissões)
  - `StripeInvoiceId`: `nvarchar(100)?`
  - `StripeCreditBalanceTransactionId`: `nvarchar(100)?`
  - `PayoutTransactionReference`: `nvarchar(150)?`
  - `CancellationReason`: `nvarchar(500)?`
  - `CreatedAtUtc`: `datetime2`
  - `UpdatedAtUtc`: `datetime2?`
* **Índices**: `ParticipantId`, `ReferralAttributionId`, `Status`, `MaturityDateUtc`, `StripeInvoiceId`, índice composto `(Status, MaturityDateUtc)`.

---

## 3. Contexto EF Core e Migrações

- **Abstração:** `IReferralDbContext` localizada em `CriaCerto.Modules.Referral.Application.Abstractions`.
- **Implementação:** `ReferralDbContext` localizada em `CriaCerto.Modules.Referral.Infrastructure.Persistence`.
- **Fábrica de Design-Time:** `ReferralDbContextFactory` localizada em `.../Persistence/DesignTimeDbContextFactory.cs`.
- **Baseline Metadata:** Registrado em `MigrationBaselineMetadata.Referral` para auditoria e controle de integridade da base.

---

## 4. Rastreamento, Captura e Atribuição no Onboarding (Fase 2.1)

### 4.1 Rastreamento do Link de Indicação
* **Links Amigáveis:** Acesso via `/r/{code}` grava o cookie `cc_referral_code` (expiração de 30 dias, `SameSite=Lax`) e redireciona o lead para `/register?ref={code}`.
* **Contador de Cliques:** Endpoint `POST /api/v1/referral/clicks/{code}` incrementa o contador `ClicksCount` da entidade `ReferralCode` de forma assíncrona.
* **Validação Prévia do Código:** Endpoint `GET /api/v1/referral/codes/validate/{code}` permite checagem inline no frontend sobre a validade do código sem expor dados sensíveis do titular (LGPD).
* **Camada de Cliente (Blazor WASM):** O serviço `IReferralTrackingService` (`ReferralTrackingService`) centraliza a leitura/gravação em `localStorage` e cookies do navegador, com limpeza pós-conclusão da fazenda.

### 4.2 Captura no Onboarding & Evento de Integração Cross-Module
* **`CreateTenantCommand`:** Campo opcional `ReferralCode`, enriquecido automaticamente com `OriginIpAddress` e `UserAgent` na camada HTTP.
* **Desacoplamento por Eventos:** Ao concluir o provisionamento do novo tenant no banco, `CreateTenantCommandHandler` publica o evento `TenantRegisteredIntegrationEvent`.
* **`TenantRegisteredReferralHandler` (`Modules.Referral`):**
  - Consome o evento via MediatR (`INotificationHandler`).
  - Localiza o código ativo correspondente.
  - Bloqueia auto-indicação (caso a fazenda indicada pertença ao mesmo participante cliente, registra atribuição com status `Fraudulent` e motivo de auditoria).
  - Garante idempotência para evitar duplicidade de atribuição para o mesmo `TenantId`.
  - Cria o registro em `[referral].[Attributions]` com status inicial `AttributionStatus.Pending`.

---

## 5. Gestão de Parceiros Comerciais no Backoffice (Fase 3.1)

### 5.1 Entidade `CommercialPartnerProfile` (`[referral].[PartnerProfiles]`)
Armazena os metadados cadastrais, categoria e notas de compliance do parceiro comercial (consultorias agronômicas, veterinários de campo, revendas agropecuárias, associações de raça).
* **Relação**: 1:1 com `ReferralProgramParticipant` (`ParticipantType.CommercialPartner`).
* **Camadas de Segurança e LGPD**:
  - `LegalDocumentNumber` (CPF ou CNPJ formatado e validado por algoritmo oficial de dígitos verificadores).
  - Mascaramento nativo no frontend via componente `<MaskedDataField>` (`ReferralPartnersRead`).
  - Categoria (`PartnerCategory`: `Consultant`, `Veterinarian`, `Dealer`, `BreedAssociation`, `Other`).

### 5.2 Provisionamento Automático de Conta e Convite
Ao cadastrar um parceiro comercial via `RegisterCommercialPartnerCommand`:
1. Valida unicidade de código/slug de indicação e unicidade de documento fiscal.
2. Cria o participante `ReferralProgramParticipant` e seu `ReferralCode` com link base `?ref=slug`.
3. Registra o contrato inicial em `PartnerContract`.
4. Provisiona usuário (`UserId`) no módulo Identity via `IPartnerAccountProvisioner`, gerando senha temporária e disparando e-mail de ativação transacional via Resend (`IEmailSender`).

### 5.3 Governança Dual-Control (4-Eyes Approval)
* Alterações cadastrais e suspensões possuem efeito imediato acompanhadas de auditoria forense com hash SHA-256 (`BackofficePermissions.ReferralPartnersManage`).
* Reajustes ou alterações nas taxas de comissionamento (`CommissionType`, `CommissionValue`, `MaxRecurringMonths`) exigem aprovação de um segundo administrador via fluxo 4-Olhos (`AdminApprovalRequest` do tipo `UpdatePartnerContractCommission`).
* Uma vez aprovado por outro operador, o comando `UpdatePartnerContractCommissionCommand` fecha a vigência do contrato anterior (`ValidToUtc = Now`, `IsActive = false`) e cria a nova versão do contrato com vigência imediata global.

### 5.4 Endpoints Administrativos (`/api/v1/backoffice/partners`)
* `GET /api/v1/backoffice/partners`: Listagem paginada com busca textual, filtro por status e tipo de comissão.
* `GET /api/v1/backoffice/partners/{id}`: Detalhes completos do parceiro, perfil, contratos vigentes/anteriores e KPIs de conversão.
* `GET /api/v1/backoffice/partners/suggest-code?name={name}&tradeName={tradeName}`: Sugestor de slug amigável e normalizado para link de indicação.
* `POST /api/v1/backoffice/partners`: Cadastro de novo parceiro comercial com contrato inicial e provisionamento de acesso seguro:
  * **Novo usuário**: Criação da conta com senha temporária legível (`CriaCerto#XXXXXX`), tokens de ativação e de definição de senha (7 dias de TTL) e disparo de e-mail transacional via Resend (`IEmailSender`) contendo o link de indicação (`/r/{code}`), credenciais provisórias, botão de ativação rápida (`/confirm-email`) e atalho direto para criar senha própria (`/forgot-password?email=...&token=...`).
  * **Usuário pré-existente**: Vinculação associativa com preservação de hash de senha original e envio de e-mail de boas-vindas com orientações de acesso ao Portal do Parceiro com as credenciais habituais.
* `PUT /api/v1/backoffice/partners/{id}`: Atualização cadastral do perfil do parceiro.
* `POST /api/v1/backoffice/partners/{id}/propose-contract-change`: Submissão de proposta de ajuste de comissão para aprovação 4-Olhos.
* `POST /api/v1/backoffice/partners/{id}/suspend`: Suspensão de parceiro com justificativa auditada.
* `POST /api/v1/backoffice/partners/{id}/reactivate`: Reativação de parceiro suspenso.

### 5.5 Interface WebAssembly (`/backoffice/partners`)
* **KPIs em Destaque**: Total de Parceiros Ativos, Comissões Geradas no Mês, Faturamento Atribuído e Taxa Média de Conversão.
* **Tabela com Busca e Filtros**: Nome, Razão Social, Categoria, Código de Indicação, Comissão Atual, Status e Ações Rápidas.
* **Componentes LGPD**: CPF/CNPJ, e-mail de contato e chave Pix ofuscados por padrão.
* **Modais Interativos**: Cadastro de parceiro com auto-sugestão de código e validação inline; submissão de alteração de comissão com advertência 4-Olhos; suspensão fundamentada e modal de detalhes com histórico completo de versões contratuais.

---

## 6. Painel Financeiro de Comissões e Repasses no Backoffice (Fase 3.2)

### 6.1 Visão Geral e Governança
O painel financeiro (`/backoffice/referral/payouts`) centraliza a conciliação bancária, liberação de comissões acumuladas por parceiros e liquidação financeira.
* **Carência Estrita (D+30)**: As comissões de parceiros comerciais permanecem no status `PendingMaturity` durante os 30 dias de garantia contra estorno ou cancelamento do plano, não sendo permitida antecipação manual.
* **Governança de Baixa**: Operadores com permissão `referral.payouts.manage` podem efetuar a baixa imediata mediante preenchimento do código de transação bancária / End-to-End Pix e upload compulsório do comprovante. Todas as ações geram log de auditoria forense com hash encadeado SHA-256 (`Category = Financial`, `Severity = High`).
* **Cancelamento Administrativo**: Permite o estorno de lançamentos em carência com justificativa auditada obrigatória (mínimo 10 caracteres) em casos de chargeback ou suspeita de fraude.

### 6.2 Armazenamento Seguro de Comprovantes
* **Serviço**: `IPayoutReceiptStorageService` (`LocalPayoutReceiptStorageService`).
* **Configuração**: Diretório local configurado em `Storage:PayoutReceiptsDirectory` (default `storage/receipts`).
* **Restrições**: Arquivos limitados a 5 MB com extensões permitidas `.pdf`, `.png`, `.jpg`, `.jpeg`. Nomes sanitizados com GUID único e proteção contra *path traversal*.
* **Acesso**: Streaming autenticado com RBAC via endpoint `GET /api/v1/backoffice/referral/payouts/receipts/{fileName}`.

### 6.3 Exportação de Lote Pix
* **Gerador**: `IPixBatchCsvGenerator` (`PixBatchCsvGenerator`).
* **Formato**: CSV delimitado por ponto e vírgula (`;`), encoding UTF-8 com BOM para compatibilidade com Excel.
* **Colunas**: `ID_PARTICIPANTE`, `NOME_FAVORECIDO`, `NOME_FANTASIA`, `DOCUMENTO_CPF_CNPJ`, `TIPO_CHAVE_PIX`, `CHAVE_PIX`, `VALOR_LIQUIDO`, `QUANTIDADE_LANCAMENTOS`, `IDENTIFICADOR_REMESSA`.

### 6.4 Endpoints da API REST (`/api/v1/backoffice/referral/payouts`)
* `GET /api/v1/backoffice/referral/payouts/kpis`: Métricas agregadas (Retido em Carência, Liberado para Repasse e Total Pago no Mês).
* `GET /api/v1/backoffice/referral/payouts/pending`: Listagem de comissões em carência D+30 com contagem regressiva de dias.
* `GET /api/v1/backoffice/referral/payouts/available`: Listagem consolidada por parceiro comercial com drill-down das comissões liberadas.
* `GET /api/v1/backoffice/referral/payouts/history`: Histórico de repasses com link para download do comprovante.
* `GET /api/v1/backoffice/referral/payouts/export-csv`: Download da planilha de remessa Pix em lote.
* `GET /api/v1/backoffice/referral/payouts/receipts/{fileName}`: Download/visualização segura do arquivo de comprovante.
* `POST /api/v1/backoffice/referral/payouts/batch-payout`: Registro de baixa em lote por parceiro (`multipart/form-data`).
* `POST /api/v1/backoffice/referral/payouts/{id}/cancel`: Cancelamento de comissão com justificativa auditada.

---

## 7. Configuração de Descontos por Plano no Backoffice (Fase 3.3)

### 7.1 Visão Geral e Fronteiras Modulares
A tela `/backoffice/referral/discount-policies` permite a calibração dos incentivos concedidos na fatura dos produtores rurais participantes do "Indique e Ganhe":
* **Integração sem Acoplamento Direto:** O módulo `Referral` descobre os planos ativos cadastrados no `Backoffice` por meio da abstração `IPlanCatalogLookupService` (`PlanCatalogLookupService`), sem realizar junções ou dependências diretas de banco de dados.
* **Governança de Auditoria:** Toda parametrização de política de desconto registra evento forense com hash encadeado SHA-256 no `AuditLog` (`Category = AuditCategory.Referral`, `Action = "Referral.PlanDiscountConfigured"`).
* **Segurança e RBAC:**
  - `referral.discounts.read`: Permissão para visualizar a listagem e o dashboard de KPIs de descontos.
  - `referral.discounts.manage`: Permissão exclusiva para configurar valores, percentuais, tetos máximos e alternar o status ativo/inativo das políticas.

### 7.2 Endpoints da API REST (`/api/v1/backoffice/referral/discount-policies`)
* `GET /api/v1/backoffice/referral/discount-policies`: Consulta combinada de planos disponíveis e suas respectivas políticas de desconto configuradas.
* `PUT /api/v1/backoffice/referral/discount-policies/{planCatalogId}`: Criação ou atualização in-place da política de desconto do plano (`DiscountType`, `DiscountValue`, `MaxDiscountCapPercentage`).
* `POST /api/v1/backoffice/referral/discount-policies/{id}/toggle-status`: Alternância de status ativo/inativo da política com auditoria.

### 7.3 Interface WebAssembly (`/backoffice/referral/discount-policies`)
* **KPIs em Tempo Real**: Total de Planos, Políticas Ativas, Descontos Percentuais (%), Descontos Fixos (R$) e Pendentes de Configuração.
* **Tabela de Calibração**: Código do Plano, Nome, Modalidade, Desconto por Indicação Ativa, Teto Máximo (% da Assinatura) e Status.
* **Modal Interativo**:
  - Seleção intuitiva de modalidade (`%` vs `R$`).
  - Inputs com validação reativa e máscaras de moeda/percentual.
  - Slider de teto de desconto (`10% a 100%`) com indicação visual de limite.
  - Simulador de economia dinâmico ilustrando a redução da fatura para 1, 2 ou 3 indicações ativas do cliente.

---

## 8. Portal do Produtor / Cliente: Área "Indique e Ganhe" (Fase 4.1)

### 8.1 Visão Geral e Fronteiras Modulares
A tela `/settings/referrals` (com alias `/referral`) no Portal do Produtor permite que as fazendas parceiras divulguem o Cria Certo e acompanhem seus abatimentos mensais:
* **Geração Sob Demanda & Anti-Colisão:** O participante e seu código inicial são gerados automaticamente na primeira visita com base no nome da fazenda (`FAZENDA-NOME`). Em caso de colisão, aplica-se sufixo numérico incremental (`FAZENDA-NOME-2`, `FAZENDA-NOME-3`).
* **Personalização pelo Produtor:** O produtor administrador pode editar seu próprio código (`CustomizeReferralCodeCommand`), com verificação de unicidade global e normalização em maiúsculas sem caracteres especiais.
* **Isolamento Modular com Tenancy:** Consultas de metadados da fazenda e status de planos são mediadas pela abstração `ITenantInfoLookupService` (`TenantInfoLookupService`) implementada no Host API.
* **Proteção LGPD Nativa:** O serviço determinístico `LgpdFarmNameMasker` protege o nome das fazendas indicadas na tabela de transparência utilizando a regra de preservar a primeira e última letra de palavras com 3+ caracteres (`F*****a S***a M***a`).

### 8.2 Endpoints da API REST (`/api/v1/referral/customer`)
* `GET /api/v1/referral/customer/dashboard`: Retorna o resumo do participante, link formatado, cálculo de desconto da próxima fatura com base na política do plano, contagem de ativos e em trial, texto de compartilhamento e listagem mascarada de indicações.
* `POST /api/v1/referral/customer/code/customize`: Altera o código de indicação do tenant autenticado (`{ "newCode": "FAZENDA-MODELO" }`).

### 8.3 Interface Blazor WebAssembly (`/settings/referrals`)
* **Card de Link Exclusivo:** URL destacada com botões para "Copiar Link" (clipboard com toast notification) e "Compartilhar no WhatsApp" (com mensagem amigável pré-formatada).
* **Modal de Personalização:** Permite ao administrador editar a slug com pré-visualização em tempo real do link resultante.
* **Cards de KPIs de Economia:**
  - *Desconto Estimado na Próxima Fatura*: Exibido em moeda (`R$ 150,00`) ou percentual com descrição da política do plano.
  - *Amigos com Assinatura Ativa*: Fazendas com pagamento confirmado gerando desconto.
  - *Em Avaliação Gratuita (Trial)*: Fazendas com cadastro pendente de primeiro faturamento.
* **Tabela de Transparência de Indicações:**
  - Nome da Fazenda Indicada (mascarado LGPD).
  - Data de Cadastro.
  - Status do Benefício com badges reativos (`Ativa — Desconto Aplicado` vs `Avaliação Gratuita (Trial) — Desconto liberado após o 1º pagamento da assinatura` vs `Assinatura Inativa / Cancelada`).
  - Impacto Estimado na Mensalidade (`+ R$ 50,00/mês` ou `Aguardando 1º Pagamento`).

---

## 9. Portal Exclusivo do Parceiro Comercial (Fase 4.2)

### 9.1 Visão Geral e Experiência do Parceiro
O portal `/partner/portal` (com rota alternativa `/partner`) oferece um ambiente independente e focado para parceiros de negócios (médicos veterinários, engenheiros agrônomos, consultores de nutrição, revendas agropecuárias e associações):
* **Layout Dedicado (`PartnerLayout.razor`):** Header escuro institucional, identificação da categoria de parceiro, atalho para a chave Pix e botão de encerramento de sessão, sem poluição com menus de pecuária de campo.
* **Autenticação Unificada Sem Fazenda:** Parceiros cadastrados via Backoffice possuem conta de usuário no `Modules.Tenancy` sem granjas vinculadas. O `LoginCommandHandler` identifica o participante via `IPartnerRoleLookupService`, emite o JWT com a role `CommercialPartner` e realiza redirecionamento automático para o portal do parceiro.
* **Visão Comercial Completa:** No extrato, o parceiro visualiza o nome integral da fazenda indicada e o valor base do faturamento gerado para conciliação transparente da sua carteira comercial.

### 9.2 Endpoints da API REST (`/api/v1/referral/partner`)
* `GET /api/v1/referral/partner/dashboard`: Retorna métricas consolidadas do parceiro, cliques, conversões, saldos do ledger e lista de campanhas.
* `GET /api/v1/referral/partner/statement`: Extrato detalhado de comissões paginado com filtros por período, status de liquidação e busca por nome da fazenda.
* `PUT /api/v1/referral/partner/pix-key`: Atualização da chave Pix no contrato ativo vigente com validação por tipo.
* `POST /api/v1/referral/partner/campaigns`: Criação de novo link de indicação rastreado com título de campanha e slug personalizado.

### 9.3 Componentes da Interface Blazor WebAssembly
* **Grid de 6 KPIs Comerciais & Financeiros:**
  1. *Cliques Totais*: Soma de todos os acessos com badge de taxa de conversão (`ConversionRatePercentage`).
  2. *Cadastros em Trial*: Atribuições com status `Pending` em avaliação de 14 dias.
  3. *Assinaturas Pagas Ativas*: Atribuições com primeiro pagamento confirmado (`Converted`).
  4. *Saldo Pendente de Carência*: Comissões retidas no período de segurança D+30.
  5. *Saldo Liberado para Saque*: Comissões com carência expirada (`AvailableForPayout`), prontas para repasse.
  6. *Total Repassado Acumulado*: Comissões quitadas via Pix (`PaidOut`).
* **Gerenciador de Links & Campanhas:** Card do link oficial com botões "Copiar Link" e "Compartilhar no WhatsApp", acompanhado de tabela com campanhas secundárias e modal de criação rápida.
* **Modal de Edição de Chave Pix:** Seletor de tipo (CPF, CNPJ, E-mail, Telefone, Chave Aleatória) e validação em tempo real.
---

## 10. Background Job de Maturação e Liquidação de Comissões (Fase 5.1)

### 10.1 Arquitetura do Worker e Ciclo Diário
* **Hospedagem & Agendamento:** `ReferralMaturityAndEnforcementWorker` (implementado como `BackgroundService` com `PeriodicTimer`), parametrizável via `IOptionsMonitor<ReferralEnforcementOptions>` no `appsettings.json` (`Referral:Enforcement:IntervalHours = 24`, `InitialDelaySeconds = 20`).
* **Regra de Maturação D+30:**
  - Varre o livro-razão (`RewardLedger`) identificando comissões de parceiros comerciais (`RewardType.CashCommission`) em status `PendingMaturity` com `MaturityDateUtc <= Now`.
  - Transiciona compulsoriamente os lançamentos elegíveis para `AvailableForPayout` via método de domínio `ledger.ReleaseForPayout()`.

### 10.2 Tratamento de Estornos e Reembolsos Stripe (Estratégia Híbrida - Opção C)
1. **Camada Reativa (Tempo Real):**
   - O webhook Stripe `charge.refunded` é processado pelo `StripePaymentService`, que dispara o evento de integração `StripeChargeRefundedIntegrationEvent`.
   - O handler `StripeChargeRefundedReferralHandler` consome o evento e cancela imediatamente qualquer lançamento pendente associado à fatura (`ledger.Cancel(...)`).
2. **Checagem Preventiva no Job (Polling Seguro):**
   - Antes de liberar lançamentos maduros para saque, o serviço consulta ativamente a API do Stripe via `IReferralPaymentVerificationService` (`StripePaymentVerificationService`).
   - Faturas canceladas (`void`, `uncollectible`) ou com emissão de notas de crédito pós-pagamento (`PostPaymentCreditNotesAmount > 0`) cancelam a comissão compulsoriamente com justificativa auditada.

### 10.3 Tratamento de Inadimplência e Cancelamento da Fazenda Indicada
* **Consulta Desacoplada:** Realizada em lote via `ITenantInfoLookupService.GetTenantSummariesAsync(tenantIds)` no Host API, sem cruzamentos ou joins físicos cross-module.
* **Ações Compulsórias:**
  - Caso a fazenda indicada esteja em status `PastDue`, `Cancelled` ou `Suspended`:
    1. Cancela o lançamento pendente: `ledger.Cancel(...)`.
    2. Migra a indicação para `Churned`: `attribution.MarkChurned()`.
    3. Registra auditoria forense no Backoffice via `IReferralAuditSink`.

### 10.4 Auditoria Forense e Disparo Manual
* **Sink de Auditoria Forense:** Implementado em `ReferralBackofficeAuditSink`, inserindo registros com hash criptográfico SHA-256 encadeado no `AuditLog` do Backoffice (`Category = Billing`, `Severity = High`).
* **Endpoint Administrativo:** Operadores financeiros com permissão `referral.payouts.manage` podem forçar a execução imediata do ciclo por meio do endpoint `POST /api/v1/backoffice/referral/payouts/run-enforcement`.
