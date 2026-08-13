# Matriz de Permissões Granulares do Backoffice Administrativo (RBAC + Policies)

## 1. Visão Geral
Este documento define o modelo de segurança e autorização granular do **Backoffice Administrativo do CriaCerto**, baseado em **RBAC (Role-Based Access Control)**, **Políticas por Recurso/Ação (ASP.NET Core Authorization)** e **Escopos de Acesso** (`Global`, `Tenant`, `Unidade`).

O módulo adota a diretiva **Default Deny**: qualquer requisição para a rota `/api/v1/backoffice/*` sem autenticação administrativa e sem a permissão requerida é negada com respostas HTTP 401 (Unauthorized) ou 403 (Forbidden) padronizadas em JSON com `Result.Failure`.

---

## 2. Papéis Padrão Administrativos (Admin Roles)

| Papel | Código | Descrição |
| :--- | :--- | :--- |
| **PlatformOwner** | `PlatformOwner` | Super-administrador da plataforma SaaS. Possui acesso total e irrestrito (`*.*`) em todos os escopos. |
| **SupportN1** | `SupportN1` | Atendimento ao cliente Nível 1. Acesso somente leitura a tenants, assinaturas e logs de auditoria. |
| **SupportN2** | `SupportN2` | Atendimento técnico Nível 2. Leitura e edição de tenants, gestão de assinaturas, logs e disparo de impersonação. |
| **FinanceOps** | `FinanceOps` | Operações financeiras. Gestão do catálogo de planos, criação e publicação de preços, controle de assinaturas. |
| **ReadOnlyAuditor** | `ReadOnlyAuditor` | Auditoria e compliance. Acesso somente leitura para auditorias fiscais e de segurança. |

---

## 3. Matriz de Permissões por Papel (Role x Permission)

| Recurso / Permissão | Código | PlatformOwner | SupportN1 | SupportN2 | FinanceOps | ReadOnlyAuditor |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **Leitura de Tenants** | `tenants.read` | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Edição de Tenants** | `tenants.write` | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Suspensão de Tenants** | `tenants.suspend` | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Leitura de Planos** | `plans.read` | ✅ | ❌ | ❌ | ✅ | ✅ |
| **Edição de Planos** | `plans.write` | ✅ | ❌ | ❌ | ✅ | ❌ |
| **Publicação de Planos** | `plans.publish` | ✅ | ❌ | ❌ | ✅ | ❌ |
| **Leitura de Assinaturas** | `subscriptions.read` | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Gestão de Assinaturas** | `subscriptions.manage` | ✅ | ❌ | ✅ | ✅ | ❌ |
| **Iniciar Impersonação** | `impersonation.start` | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Encerrar Impersonação** | `impersonation.stop` | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Leitura de Auditoria** | `audit.read` | ✅ | ✅ | ✅ | ❌ | ✅ |
| **Gestão de Usuários Admin** | `users_admin.manage` | ✅ | ❌ | ❌ | ❌ | ❌ |

---

## 4. Escopos de Acesso

- **`Global`**: Aplica-se a toda a plataforma SaaS (padrão para ações de Backoffice).
- **`Tenant`**: Restrito ao contexto de um Tenant específico.
- **`Unidade`**: Restrito a uma unidade de produção/fazenda específica do tenant.

---

## 5. Como Utilizar no Backend e Frontend

### Backend (.NET 10 Web API)
Decore a controller ou action com o atributo `HasPermission`:
```csharp
[ApiController]
[Route("api/v1/backoffice/tenants")]
[HasPermission(BackofficePermissions.TenantsRead)]
public class TenantsController : ControllerBase
{
    [HttpPost("{id}/suspend")]
    [HasPermission(BackofficePermissions.TenantsSuspend, BackofficePermissions.ScopeGlobal)]
    public async Task<IActionResult> SuspendTenant(Guid id)
    {
        // Apenas PlatformOwner tem essa permissão
    }
}
```

### Frontend (Blazor Web App)
Utilize o componente `<PermissionGuard>`:
```razor
<PermissionGuard Permission="@BackofficePermissions.TenantsSuspend">
    <Authorized>
        <button class="btn btn-danger" @onclick="Suspend">Suspender Tenant</button>
    </Authorized>
</PermissionGuard>
```

---

## 6. Autenticação Multi-Fator (MFA) e Gestão de Sessões

### MFA Obrigatório
Contas administrativas que possuam permissões sensíveis (`impersonation.start`, `impersonation.stop`, `plans.publish`, `tenants.suspend`) exigem obrigatoriamente autenticação de dois fatores (TOTP / RFC 6238).
- Na tentativa de login de usuários com permissões sensíveis sem MFA ativado ou sem envio de `MfaCode`, a API responde com o erro padronizado `Backoffice.MfaRequired`.
- Desativação de MFA para contas com permissões sensíveis é bloqueada via regra de negócio (`Backoffice.MfaRequiredForRole`).

### Governança de Sessões (`AdminSession`)
- **TTL de Sessão**: 30 minutos por token de sessão (`SessionToken`).
- **TTL de Refresh**: 8 horas por refresh token (`RefreshToken`) com rotação automática (*Refresh Token Rotation*).
- **Revogação em Tempo Real**: Desativação de conta ou alteração de senha revoga imediatamente todas as sessões ativas do usuário.
- **Trilha de Auditoria**: Todas as operações de login, rotação de credenciais, MFA e revogação de sessões geram registros imutáveis em `AuditLog`.
