# ADR 0008: Gestão de Solicitações Administrativas e Princípio 4-Eyes (Dual Control)

## Status
Aceito (Accepted)

## Contexto
No ecossistema SaaS de gestão pecuária CriaCerto, certas ações administrativas possuem elevado impacto operacional, financeiro ou de segurança. Entre elas destacam-se:
1. Publicação de novas versões de planos de assinatura no catálogo oficial (afetando precificação e quotas de rebanho/UA).
2. Suspensão massiva de tenants (bloqueio operacional de produtores e confinamentos).
3. Concessão ou elevação emergencial de papéis e privilégios administrativos para operadores.

A execução unilateral dessas ações por um único administrador, sem deliberação prévia ou evidências auditadas, incorre em riscos severos de fraude, erros operacionais críticos e não conformidade com frameworks de governança e SOC 2 / ISO 27001.

## Decisão
Implementar a **Gestão de Solicitações Administrativas sob o Princípio 4-Eyes (Dual Control)** no módulo `Modules.Backoffice`:

1. **Entidade de Domínio e Ciclo de Vida Imutável**:
   - Criação da entidade `AdminApprovalRequest` com ciclo de vida rigoroso: `Pending` ➔ (`Approved` | `Rejected` | `Cancelled` | `Expired`) ➔ `Executed`.
   - Registro de payload JSON de execução (`PayloadJson`) e visual diff estruturado (`DiffJson`).
   - Expiração temporal automática (TTL padrão de 48 horas). Solicitações vencidas não podem ser deliberadas ou executadas.

2. **Segregação Estrita de Privilégios (4-Eyes Enforcement)**:
   - O solicitante da ação administrativa (`RequestedByAdminUserId`) é **categoricamente impedido** pelo domínio de autoaprovar ou autorejeitar sua própria solicitação (`ApprovalErrors.CannotSelfApprove`).
   - A deliberação requer a intervenção de um segundo administrador credenciado detentor da permissão `approvals.review`.
   - Permissões granulares estabelecidas: `approvals.request` (solicitar) e `approvals.review` (revisar/decidir).

3. **Execução Atômica e Rastreabilidade Forense**:
   - Na aprovação, o sistema despacha a execução automática do payload da ação (`PublishPlanVersion`, `MassTenantSuspension` ou `ExtendedAccessGrant`) e atualiza o estado para `Executed` com o resultado gravado.
   - Registro imutável em `AuditLog` para cada etapa: `Approval.Requested`, `Approval.ApprovedAndExecuted`, `Approval.Rejected` e `Approval.Cancelled`.

4. **Experiência Visual e Salvaguarda no Frontend Blazor**:
   - Console `ApprovalsManagement.razor` com KPIs de governança e visão 360 de pendências.
   - Modal `ApprovalDetailModal.razor` com painel de **Diff de Impacto** e banner dinâmico de salvaguarda 4-eyes quando visualizado pelo próprio autor.

## Consequências
- **Positivas**:
  - Eliminação de pontos únicos de falha humana ou abuso de privilégios em operações de alto risco.
  - Trilhas forenses completas com histórico antes/depois (`DiffJson`) para auditorias e compliance.
  - Conformidade nativa com princípios de segregação de funções (SoD - Segregation of Duties).
- **Negativas**:
  - Introdução de fricção intencional para ações críticas, exigindo coordenação entre pelo menos dois administradores qualificados.
