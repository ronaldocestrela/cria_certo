# Roadmap de Implementação do Backoffice Administrativo - CriaCerto

## 1. Estratégia do Roadmap

Este roadmap define o plano de implementação do **Backoffice Administrativo** da plataforma CriaCerto, com foco em operação SaaS multi-tenant, governança de acesso e eficiência de suporte/financeiro.  
As entregas estão organizadas em **6 Fases Sequenciais**, cobrindo fundação de segurança, gestão de tenants/clientes, controle de planos, impersonação auditável, observabilidade e prontidão operacional.

### Regra de Conclusão por Sub-fase (Definition of Done)
> **Uma sub-fase só é considerada "CONCLUÍDA" quando:**
> 1. Backend (.NET 10) e Frontend (Blazor Web App) estiverem implementados com Result Pattern (`Result<T>`).
> 2. Houver cobertura de testes unitários, integração e autorização (TDD Red/Green/Refactor).
> 3. Permissões granulares (RBAC por política) estiverem aplicadas em API e UI.
> 4. Auditoria e trilha de ações administrativas estiverem registradas.
> 5. Documentação viva (`/docs`) e status da sub-fase forem atualizados.

---

## 2. Visão Geral das Fases

```
┌──────────────────────────────────────────────────────────────┐
│ Phase 1: Fundação de Backoffice, IAM e Permissões Granulares│
└─────────────────────────────┬────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────┐
│ Phase 2: Gestão de Tenants, Clientes e Ciclo de Vida        │
└─────────────────────────────┬────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────┐
│ Phase 3: Catálogo de Planos, Assinaturas e Feature Gating   │
└─────────────────────────────┬────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────┐
│ Phase 4: Impersonação Segura, Suporte e Operações Assistidas│
└─────────────────────────────┬────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────┐
│ Phase 5: Compliance, Auditoria, Risco e Observabilidade     │
└─────────────────────────────┬────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────┐
│ Phase 6: Hardening, Rollout Gradual e Go-Live do Backoffice │
└──────────────────────────────────────────────────────────────┘
```

---

## 3. Detalhamento das Fases & Entregáveis

---

### Phase 1: Fundação de Backoffice, IAM e Permissões Granulares

#### Sub-fase 1.1: Módulo Administrativo Global (`Modules.Backoffice`) [CONCLUÍDA]
* **Backend (.NET 10):**
  * Criar módulo interno de backoffice com boundaries próprios (sem acoplamento direto cross-module).
  * Definir agregados iniciais: `AdminUser`, `AdminRole`, `Permission`, `AdminSession`, `AuditLog`.
  * Implementar `BackofficeAccessMiddleware` para isolamento de rotas `/api/v1/backoffice/*`.
* **Frontend (Blazor):**
  * Criar shell administrativo (`BackofficeLayout.razor`) com navegação por permissões.
  * Tela inicial com KPIs de tenants, assinaturas, inadimplência e saúde da plataforma.
* **TDD & Validação:**
  * Testes de autorização negando acesso por padrão (`default deny`) quando permissão inexistente.

#### Sub-fase 1.2: Modelo de Permissões Granulares (RBAC + Policies) [CONCLUÍDA]
* **Backend:**
  * Implementar catálogo de permissões por recurso/ação, por exemplo:
    * `tenants.read`, `tenants.write`, `tenants.suspend`
    * `plans.read`, `plans.write`, `plans.publish`
    * `subscriptions.read`, `subscriptions.manage`
    * `impersonation.start`, `impersonation.stop`
    * `audit.read`, `users_admin.manage`
  * Policies com escopo (`Global`, `Tenant`, `Unidade`) e suporte a `PermissionRequirement`.
  * Papéis padrão: `PlatformOwner`, `SupportN1`, `SupportN2`, `FinanceOps`, `ReadOnlyAuditor`.
* **Frontend:**
  * Guardas de rota e rendering condicional por permissão (menus, botões e ações críticas).
* **TDD & Validação:**
  * Matriz de testes role x permission x scope garantindo bloqueio de ações fora do escopo.

#### Sub-fase 1.3: Gestão de Usuários Administrativos e Sessões [CONCLUÍDA]
* **Backend:**
  * CRUD de usuários administrativos, vínculo de múltiplos papéis e rotação de credenciais.
  * MFA obrigatório para perfis com permissões sensíveis (`impersonation.*`, `plans.publish`, `tenants.suspend`).
  * Sessões com expiração curta, refresh seguro e revogação imediata.
* **Frontend:**
  * Tela de usuários administrativos com filtros por status, função e último acesso.
  * UX de ativação MFA e revogação de sessões ativas.
* **TDD & Validação:**
  * Testes de integração cobrindo login administrativo, MFA, expiração e revogação.

---

### Phase 2: Gestão de Tenants, Clientes e Ciclo de Vida

#### Sub-fase 2.1: Cadastro e Visão 360 do Tenant [CONCLUÍDA]
* **Backend:**
  * `CreateTenantAdminCommand`, `UpdateTenantAdminCommand`, `GetTenantBackofficeDetailQuery`.
  * Campos operacionais: dados fiscais, status contratual, limites ativos, owner técnico/comercial.
* **Frontend:**
  * Tela `TenantsManagement.razor` com busca, filtros avançados e visão consolidada por tenant.
* **TDD & Validação:**
  * Testes para consistência de dados cadastrais e regras de unicidade (CNPJ/identificador externo).

#### Sub-fase 2.2: Estados do Tenant e Governança de Ciclo de Vida [CONCLUÍDA]
* **Backend:**
  * Máquina de estados: `Trial`, `Active`, `PastDue`, `Suspended`, `Cancelled`, `Archived`.
  * Transições com política e motivo obrigatório (`SuspendTenantCommand`, `ReactivateTenantCommand`).
  * Regras de segurança para impedir suspensão indevida de tenants protegidos.
* **Frontend:**
  * Ações de ciclo de vida com confirmação forte e formulário de justificativa.
* **TDD & Validação:**
  * Testes de transição de estados válidos/invalidos e obrigatoriedade de justificativa.

#### Sub-fase 2.3: Gestão de Clientes e Segmentação Operacional [CONCLUÍDA]
* **Backend:**
  * Taxonomias para segmentação (porte, região, perfil produtivo, risco de churn).
  * Etiquetas operacionais (`Tags`) para campanhas de suporte, sucesso do cliente e retenção.
* **Frontend:**
  * Listas segmentadas com filtros salvos e exportação de recortes operacionais.
* **TDD & Validação:**
  * Testes de performance de consulta e paginação estável em bases grandes.

---

### Phase 3: Catálogo de Planos, Assinaturas e Feature Gating

#### Sub-fase 3.1: Catálogo de Planos Versionado [CONCLUÍDA]
* **Backend:**
  * Entidades: `PlanCatalog`, `PlanVersion`, `PlanFeature`, `PlanLimit`.
  * Versionamento sem impacto retroativo nas assinaturas vigentes.
  * Publicação controlada por permissão (`plans.publish`) e workflow de aprovação.
* **Frontend:**
  * Tela de planos com comparação de recursos, limites e janela de vigência por versão.
* **TDD & Validação:**
  * Testes garantindo imutabilidade de versões publicadas e migração controlada.

#### Sub-fase 3.2: Assinaturas, Upgrade/Downgrade e Regras de Capacidade [CONCLUÍDA]
* **Backend:**
  * `ChangeTenantPlanCommand` com regras de elegibilidade e impacto em módulos habilitados.
  * Jobs para enforcement de limites por plano (ex.: capacidade de cabeças, usuários, unidades).
  * Regras de grace period para downgrades com excesso de uso.
* **Frontend:**
  * Fluxo assistido de mudança de plano com pré-visualização de impacto.
* **TDD & Validação:**
  * Testes de integração para upgrade, downgrade, bloqueios por limite e grace period.

#### Sub-fase 3.3: Faturamento Operacional e Eventos de Cobrança [CONCLUÍDA]
* **Backend:**
  * Registro de eventos de assinatura e histórico financeiro (`BillingInvoice`, `BillingEvent`).
  * Conciliação de status de cobrança e sincronização com módulo de tenancy/licensing (`Active` ↔ `PastDue` ↔ `Suspended`).
  * Endpoints de overview financeiro (MRR, inadimplência), baixa de pagamentos e anistia assistida com auditoria em `AuditLog`.
* **Frontend:**
  * Painel financeiro `BillingManagement.razor` com aging de inadimplência (0-30d, 31-60d, 61-90d, >90d).
  * Modais interativos `RecordPaymentModal.razor`, `RegularizeBillingModal.razor` e `TenantBillingHistoryModal.razor`.
* **TDD & Validação:**
  * Testes unitários e de integração validando ciclo completo de cobrança, conciliação e acesso a features (`BillingInvoiceDomainTests`, `BillingFeaturesTests`, `BillingLifecycleIntegrationTests`).

---

### Phase 4: Impersonação Segura, Suporte e Operações Assistidas

#### Sub-fase 4.1: Acesso por Impersonação com Dupla Salvaguarda [CONCLUÍDA]
* **Backend:**
  * Entidade de domínio rica `ImpersonationSession` com status (`Active`, `Ended`, `Expired`, `Revoked`) e TTL delimitado (5 a 60 min, default 15 min).
  * `StartImpersonationSessionCommand` e `StopImpersonationSessionCommand` implementados com **Result Pattern** e proteção via permissões granulares (`impersonation.start`, `impersonation.stop`).
  * Salvaguardas duplas: validação de justificativa detalhada (mín. 10 caracteres), vínculo obrigatório de ticket de suporte (ex: `SUP-1042`), e rejeição automática para tenants suspensos, cancelados, arquivados ou protegidos (`IsProtected`).
  * Emissão de token JWT efêmero com claims explícitas de auditoria (`is_impersonation`, `impersonated_by_admin_id`, `impersonated_by_admin_email`, `impersonation_session_id`, `impersonation_ticket`) e registro imutável em `AuditLog` (`Impersonation.Started` e `Impersonation.Stopped`).
* **Frontend:**
  * Componente `StartImpersonationModal.razor` com formulário de dupla salvaguarda, avisos de compliance e validação em tempo real.
  * Componente persistente `ImpersonationBanner.razor` exibido no topo de todos os layouts com badge pulsante âmbar, informações do tenant/chamado, contador regressivo ao vivo (mm:ss) e botão para encerramento instantâneo com restauração do token de admin.
  * Botão de acesso por impersonação integrado ao painel 360 de `TenantsManagement.razor`.
* **TDD & Validação:**
  * Testes de domínio `ImpersonationSessionDomainTests`, testes de features `StartImpersonationSessionCommandTests`, `StopImpersonationSessionCommandTests` e testes de segurança de claims `ImpersonationSecurityTests` aprovados com 100% de sucesso.

#### Sub-fase 4.2: Workbench de Suporte N1/N2 [CONCLUÍDA]
* **Backend:**
  * Permissões granulares de governança adicionadas: `support.diagnose` (N1, N2, PlatformOwner) e `support.remediate` (N2, PlatformOwner) com segregação estrita contra operações financeiras e suspensões destrutivas.
  * API de diagnóstico assistido `GetTenantDiagnosticsQuery` consolidando saúde de sincronização PWA/campo, cotas de rebanho vs plano, matriz de módulos ativos, status de filas e jobs em background, alertas/falhas recentes e sessão de suporte ativa.
  * Catálogo de playbooks operacionais padronizados `GetSupportPlaybooksQuery` (`PB-SYNC-01` a `PB-LOCK-05`) com roteiros de verificação passo a passo.
  * Catálogo de ações remediativas seguras via `ExecuteTenantRemediationCommand` (`RequestClientCacheReset`, `EvictTenantCache`, `ReconcileEntitlements`, `RetryFailedQueueItems`, `ResetTransientLocks`) com dupla salvaguarda (ticket de suporte obrigatório e justificativa com mín. 10 caracteres) e registro imutável em `AuditLog` (`Support.RemediationExecuted`).
  * Endpoints REST mapeados e protegidos: `GET /api/v1/backoffice/support/tenants/{id:guid}/diagnostics`, `GET /api/v1/backoffice/support/playbooks` e `POST /api/v1/backoffice/support/tenants/{id:guid}/remediation`.
* **Frontend:**
  * Console operacional completo `SupportWorkbench.razor` com busca e seleção de tenants, cockpit com 4 cards de KPIs de diagnóstico, grid de módulos habilitados, accordion interativo com checklist de playbooks e disparo de ações sugeridas.
  * Componente modal `ExecuteRemediationModal.razor` com salvaguardas, validação em tempo real e feedback de execução.
  * Integração na navegação lateral `BackofficeNavMenu.razor` e atalho direto no painel 360 de `TenantsManagement.razor`.
* **TDD & Validação:**
  * Testes unitários de matriz RBAC (`BackofficeRolePermissionMatrixTests` e `BackofficePermissionServiceTests`) garantindo que N1 não executa remediação, N2 possui acesso remediativo e FinanceOps/Auditor são segregados.
  * Testes funcionais em `SupportFeaturesTests` cobrindo diagnósticos, playbooks, validações de ticket/justificativa e auditoria forense com 100% de sucesso.

#### Sub-fase 4.3: Gestão de Solicitações Administrativas (4-eyes principle) [CONCLUÍDA]
* **Backend:**
  * Entidade de domínio rica `AdminApprovalRequest` com ciclo de vida completo (`Pending`, `Approved`, `Rejected`, `Executed`, `Cancelled`, `Expired`), payload de execução serializado (`PayloadJson`) e visual diff (`DiffJson`).
  * Salvaguarda estrita do **Princípio 4-Eyes**: bloqueio de domínio e aplicação impedindo categoricamente que o administrador solicitante autoaprove ou autorejeite sua requisição (`ApprovalErrors.CannotSelfApprove`).
  * Mecanismo de expiração temporal automática (TTL configurável de 1h a 168h, padrão 48h).
  * Permissões granulares de governança: `approvals.request` e `approvals.review` integradas à matriz RBAC (`BackofficePermissions` e `BackofficeRoles`).
  * Handlers CQRS com **Result Pattern** e execução atômica no dispatch pós-aprovação (`CreateApprovalRequestCommand`, `ApproveApprovalRequestCommand`, `RejectApprovalRequestCommand`, `CancelApprovalRequestCommand` e `GetApprovalRequestsQueries`).
  * Endpoints REST mapeados e protegidos em `Program.cs` sob o grupo `/api/v1/backoffice/approvals` com registro imutável em `AuditLog`.
  * Migração EF Core `AddAdminApprovalRequests` com índices otimizados para status, expiração e solicitante.
* **Frontend:**
  * Console completo de governança `ApprovalsManagement.razor` com 4 cards de KPIs, navegação por abas (*Pendentes de Análise*, *Minhas Solicitações*, *Histórico Concluído*), filtros e tabela interativa de solicitações.
  * Componente modal detalhado `ApprovalDetailModal.razor` com painel de **Diff de Impacto**, evidências, banner de alerta contextual para o solicitante e painel de deliberação para o revisor (aprovação com notas ou rejeição com motivo).
  * Modal `CreateApprovalRequestModal.razor` para submissão ad-hoc de ações de alta criticidade com validação em tempo real.
  * Item de menu dedicado integrado em `BackofficeNavMenu.razor` com controle de exibição via `PermissionGuard`.
  * Integração completa no cliente HTTP `BackofficeApiClient.cs`.
* **TDD & Validação:**
  * Testes unitários de domínio `AdminApprovalRequestDomainTests` validando não-autoaprovação, ciclo de vida e expiração.
  * Testes de features `ApprovalFeaturesTests` cobrindo submissão, aprovação com execução atômica (publicação de plano e suspensão massiva), rejeição, cancelamento e auditoria forense.
  * Testes de matriz RBAC `BackofficeRolePermissionMatrixTests` e `BackofficePermissionServiceTests` com 100% de sucesso.

---

### Phase 5: Compliance, Auditoria, Risco e Observabilidade

#### Sub-fase 5.1: Auditoria Forense e Retenção de Logs [CONCLUÍDA]
* **Backend:**
  * Entidade de domínio `AuditLog` com modelo forense estruturado: quem (`AdminUserId`, `AdminUserEmail`, `ActorRole`), quando (`TimestampUtc`), onde (`IpAddress`, `UserAgent`), alvo (`Resource`, `TargetTenantId`, `TargetTenantName`), categorização (`AuditCategory`), severidade (`AuditSeverity`) e mutação antes/depois (`OldValuesJson`, `NewValuesJson`).
  * Assinatura criptográfica SHA-256 canônica (`RecordHash`) e encadeamento sequencial tamper-evident (`PreviousRecordHash`) para detecção automática de adulteração ou deleção indevida.
  * Verificação de integridade canônica via `VerifyIntegrity()`, queries de varredura `VerifyAuditTrailIntegrityQuery` e métricas em tempo real `GetAuditStatsQuery`.
  * Política de retenção e ciclo de vida por criticidade com `ApplyAuditRetentionPolicyCommand`: expurgo físico seguro de logs de severidade `Low` (>90d), arquivamento lógico a frio de logs `Medium` (>1 ano) e `High` (>3 anos), e proteção perpétua para eventos `Critical` (nunca expurgados automaticamente).
  * Suporte a simulação (`DryRun`), exportação estruturada em CSV/JSON (`ExportAuditTrailQuery`) e registro imutável da própria execução de retenção.
  * Endpoints REST mapeados no grupo `/api/v1/backoffice/audit` protegidos por claims RBAC (`audit.read` e `users_admin.manage`).
  * Migração EF Core `AddForensicAuditAndRetentionPolicy` com índices compostos de alta performance.
* **Frontend:**
  * Console interativo `AuditExplorer.razor` em `/backoffice/audit` com 4 KPI cards operacionais e indicador de integridade criptográfica.
  * Barra de filtros multifatorial: busca textual livre, filtro por Ator, Tenant, Severidade, Categoria, Período de Datas e toggle de registros arquivados.
  * Modal forense de detalhe `AuditLogDetailModal.razor` com painel de **Diff Antes vs Depois**, metadados de rede e selo de verificação de hash.
  * Modal `AuditRetentionModal.razor` para configuração de SLAs por criticidade, simulação `DryRun` e execução de arquivamento/expurgo.
  * Integração completa com `BackofficeApiClient.cs`.
* **TDD & Validação:**
  * Testes unitários de domínio `AuditLogDomainTests` validando hashing SHA-256, detecção de adulteração em tempo real e retrocompatibilidade.
  * Testes de features CQRS `AuditFeaturesTests` cobrindo filtros de busca, paginação, verificação de cadeia íntegra vs corrompida, exportação CSV e aplicação de políticas de retenção (DryRun e execução física).
  * 100% de sucesso na suíte global de testes (470 testes aprovados, incluindo testes de arquitetura com Testcontainers).
  * Formalização arquitetural via ADR `0009-forensic-audit-trail-and-retention-policy.md`.

#### Sub-fase 5.2: Observabilidade de Backoffice e Alertas [PLANEJADA]
* **Backend & DevOps:**
  * Métricas e traces para fluxos de admin: latência de consultas, falhas de policy, picos de impersonação.
  * Alertas para comportamento anômalo (tentativas negadas em sequência, ações críticas fora de janela).
* **Frontend:**
  * Painel de saúde operacional com indicadores de risco e eventos ativos.
* **TDD & Validação:**
  * Testes de contrato para eventos/telemetria e validação de regras de alerta.

#### Sub-fase 5.3: Compliance LGPD e Governança de Acesso [PLANEJADA]
* **Backend:**
  * Mascaramento de dados sensíveis no backoffice por permissão contextual.
  * Exportação de trilha de acesso para auditorias internas/externas.
* **Frontend:**
  * Visualização de dados com redaction progressivo para perfis sem necessidade operacional.
* **TDD & Validação:**
  * Testes de autorização e de exposição mínima de dados.

---

### Phase 6: Hardening, Rollout Gradual e Go-Live do Backoffice

#### Sub-fase 6.1: Segurança Aplicacional e Testes de Intrusão Assistidos [PLANEJADA]
* **Backend & Infra:**
  * Hardening de autenticação, proteção contra elevação de privilégio e validação forte de policies.
  * Testes automatizados de autorização negativa para endpoints sensíveis.
* **TDD & Validação:**
  * Suíte de regressão de segurança executada em CI/CD com bloqueio de merge em falha.

#### Sub-fase 6.2: Rollout por Ondas e Feature Flags [PLANEJADA]
* **Operação:**
  * Liberação progressiva do backoffice por grupos de usuários administrativos.
  * Feature flags para módulos críticos (impersonação, publicação de planos, suspensões).
* **Validação:**
  * Critérios de rollback definidos por SLO de erro, latência e incidentes de autorização.

#### Sub-fase 6.3: Playbooks, Treinamento e Hand-off Operacional [PLANEJADA]
* **Documentação & Operação:**
  * Playbooks de suporte, segurança e resposta a incidentes administrativos.
  * Treinamento dos perfis `SupportN1/N2`, `FinanceOps` e `PlatformOwner`.
  * Runbooks de emergência para revogação de acesso e contenção.
* **Validação:**
  * Simulação de incidente (tabletop) com evidência de resposta ponta a ponta.

---

## 4. Matriz Base de Permissões Granulares (Referência Inicial)

```markdown
- Escopos suportados: Global, Tenant, Unidade.
- Modelo: RBAC com policies por permissão + constraints de escopo.
- Princípio: Least Privilege + Default Deny.
- Ações críticas exigem MFA e, quando aplicável, aprovação dupla (4-eyes).
- Toda ação administrativa relevante deve gerar evento de auditoria imutável.
```

---

## 5. Checklist de Sign-Off por Sub-Fase

Para marcar qualquer sub-fase como **CONCLUÍDA**, a checklist abaixo deve ser preenchida:

```markdown
- [ ] Endpoints e serviços backoffice implementados em .NET 10 com Result Pattern (Result<T>).
- [ ] Interface Blazor administrativa implementada com guardas por permissão granular.
- [ ] Testes unitários, integração e autorização aprovados (TDD Red/Green/Refactor).
- [ ] Auditoria de ações administrativas e trilha de impersonação validadas.
- [ ] Documentação viva (/docs) atualizada com regras, fluxos e decisões arquiteturais.
```

---
*Roadmap oficial para implementação do Backoffice Administrativo do CriaCerto.*
