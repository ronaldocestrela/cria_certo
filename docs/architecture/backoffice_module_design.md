# Desenho Arquitetural: Módulo Administrativo Global (`Modules.Backoffice`)

## 1. Visão Geral
O módulo `Modules.Backoffice` é o núcleo administrativo isolado da plataforma SaaS CriaCerto, responsável pelo gerenciamento de tenants, clientes, permissões granulares (RBAC), governança de licenças e suporte assistido.

## 2. Princípios de Arquitetura & Boundaries
* **Monólito Modular:** O módulo possui limites de domínio estritos e seu próprio contexto de banco de dados (`BackofficeDbContext`) com schema relacional isolado (`backoffice`).
* **Comunicação Cross-Module:** Junções diretas de tabelas entre o `BackofficeDbContext` e os DbContexts de outros módulos (`Tenancy`, `Breeding`, etc.) são **estritamente proibidas**. A comunicação ocorre via eventos in-memory (MediatR) ou contratos de query expostos pelos módulos.
* **Result Pattern (`Result<T>`):** Manipulação funcional de respostas e validações. Nenhuma exceção é utilizada para controle de fluxo ou erros de regra de negócio.

## 3. Modelo de Domínio e Agregados
* **`AdminUser` (Agregado Raiz):** Representa operadores e administradores globais da plataforma (Nome, Email, PasswordHash, IsActive, MfaEnabled, Roles).
* **`AdminRole` & `Permission`:** Definição de papéis (`PlatformOwner`, `SupportN1`, `SupportN2`, `FinanceOps`, `ReadOnlyAuditor`) e permissões com escopo (`Global` ou `Tenant`).
* **`AdminSession`:** Controle e revogação de sessões ativas com rastreamento de IP e User-Agent.
* **`AuditLog`:** Trilha forense imutável de ações administrativas efetuadas no sistema.

## 4. Integração Backoffice ↔ Tenancy (Sub-fase 2.1)
* Handlers em `Modules.Backoffice` delegam persistência a comandos/queries em `Modules.Tenancy` via MediatR (`ISender`).
* Auditoria (`Tenant.Created`, `Tenant.Updated`) é gravada no `BackofficeDbContext`.
* Proibido join direto entre DbContexts.

## 4.1 Integração de Ciclo de Vida (Sub-fase 2.2)
* Comandos admin (`SuspendTenantAdminCommand`, etc.) delegam para `*ForAdminCommand` no Tenancy.
* Auditoria: `Tenant.Suspended`, `Tenant.Reactivated`, `Tenant.Cancelled`, `Tenant.Archived`, `Tenant.ProtectionChanged`.
* `DetailsJson` inclui `FromStatus`, `ToStatus`, `Reason`, `IsProtected`.
* Enforcement de acesso produtor via `LoginCommand`, `SelectTenantCommand` e `TenantAccessMiddleware`.

## 4.2 Segmentação Operacional e Filtros Salvos (Sub-fase 2.3)
* Taxonomias persistidas no Tenancy (`SizeSegment`, `CommercialRegion`, `ProductiveProfile`, `ChurnRisk`) com catálogo em `TenantSegmentationCatalog`.
* Etiquetas operacionais (`OperationalTag`, `TenantOperationalTag`) no schema `tenancy` para consultas indexáveis em uma única query.
* Filtros salvos (`AdminSavedFilter`) no schema `backoffice`, escopo por `AdminUserId`.
* Facades Backoffice delegam segmentação/tags/export para Tenancy via MediatR; auditoria local:
  * `Tenant.SegmentationUpdated`, `Tenant.TagsReplaced`, `Tenant.TagCreated`, `Tenant.TagDeactivated`, `Tenant.Exported`.
* Exportação CSV UTF-8 com BOM, teto de 10.000 registros por recorte.

## 4.3 Faturamento Operacional e Eventos de Cobrança (Sub-fase 3.3)
* Entidades no schema `backoffice`: `BillingInvoice`, `BillingEvent`.
* Régua de aging de inadimplência (1-30d, 31-60d, 61-90d, >90d) e job periódico `ProcessBillingDelinquencyJob`.
* Conciliação financeira automática e assistida: quitação de fatura ou anistia com justificativa restaura o status do tenant de `PastDue` / `Suspended` para `Active`.
* Ações auditadas: `Billing.PaymentRecorded`, `Billing.InvoiceRegularized`.

## 4.4 Solicitações Administrativas e Princípio 4-Eyes (Sub-fase 4.3)
* Entidade de domínio `AdminApprovalRequest` com ciclo de vida rigoroso (`Pending`, `Approved`, `Rejected`, `Executed`, `Cancelled`, `Expired`).
* Salvaguarda estrita contra autoaprovação (`ApprovalErrors.CannotSelfApprove`) e expiração temporal automática (padrão 48h).
* Execução atômica despachada pós-deliberação com visual diff de impacto (`DiffJson`).
* Auditoria: `Approval.Requested`, `Approval.ApprovedAndExecuted`, `Approval.Rejected`, `Approval.Cancelled`.

## 4.5 Auditoria Forense, Integridade Criptográfica e Retenção (Sub-fase 5.1)
* Modelo estruturado `AuditLog`: ator (`AdminUserId`, `AdminUserEmail`, `ActorRole`), rede (`IpAddress`, `UserAgent`), alvo (`Resource`, `TargetTenantId`), categorização (`AuditCategory`), severidade (`AuditSeverity`) e mutação (`OldValuesJson`, `NewValuesJson`).
* Integridade tamper-evident com hash canônico SHA-256 (`RecordHash`) e encadeamento sequencial (`PreviousRecordHash`).
* Detecção em tempo real de adulteração em banco de dados (`VerifyIntegrity()`).
* Política de retenção estratificada por criticidade com comando `ApplyAuditRetentionPolicyCommand` (suporte a `DryRun` e auditoria de execução):
  * `Critical`: permanente/1825d (nunca expurgado); `High`: 1095d (arquivamento); `Medium`: 365d (arquivamento); `Low`: 90d (expurgo físico).
* Console interativo `AuditExplorer.razor` com KPIs, filtros avançados, diff visual e exportação CSV.

## 5. Política de Segurança Default Deny
Todas as requisições destinadas ao prefixo `/api/v1/backoffice/*` passam pelo middleware de segurança `BackofficeAccessMiddleware`.
* Se o usuário não possuir credenciais autenticadas ou a claim/permissão administrativa correspondente, a requisição é bloqueada imediatamente com status `401 Unauthorized` ou `403 Forbidden`.

## 6. Interface Blazor Web App (Shell Admin)
O acesso ao Backoffice na camada de apresentação utiliza um Shell próprio (`BackofficeLayout.razor`) e menus com verificação de autorização (`BackofficeNavMenu.razor`), isolados da interface operacional dos produtores.
