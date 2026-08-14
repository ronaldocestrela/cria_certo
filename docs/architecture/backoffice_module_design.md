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

## 5. Política de Segurança Default Deny
Todas as requisições destinadas ao prefixo `/api/v1/backoffice/*` passam pelo middleware de segurança `BackofficeAccessMiddleware`.
* Se o usuário não possuir credenciais autenticadas ou a claim/permissão administrativa correspondente, a requisição é bloqueada imediatamente com status `401 Unauthorized` ou `403 Forbidden`.

## 6. Interface Blazor Web App (Shell Admin)
O acesso ao Backoffice na camada de apresentação utiliza um Shell próprio (`BackofficeLayout.razor`) e menus com verificação de autorização (`BackofficeNavMenu.razor`), isolados da interface operacional dos produtores.
