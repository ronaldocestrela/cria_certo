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

#### Sub-fase 2.1: Cadastro e Visão 360 do Tenant [PLANEJADA]
* **Backend:**
  * `CreateTenantAdminCommand`, `UpdateTenantAdminCommand`, `GetTenantBackofficeDetailQuery`.
  * Campos operacionais: dados fiscais, status contratual, limites ativos, owner técnico/comercial.
* **Frontend:**
  * Tela `TenantsManagement.razor` com busca, filtros avançados e visão consolidada por tenant.
* **TDD & Validação:**
  * Testes para consistência de dados cadastrais e regras de unicidade (CNPJ/identificador externo).

#### Sub-fase 2.2: Estados do Tenant e Governança de Ciclo de Vida [PLANEJADA]
* **Backend:**
  * Máquina de estados: `Trial`, `Active`, `PastDue`, `Suspended`, `Cancelled`, `Archived`.
  * Transições com política e motivo obrigatório (`SuspendTenantCommand`, `ReactivateTenantCommand`).
  * Regras de segurança para impedir suspensão indevida de tenants protegidos.
* **Frontend:**
  * Ações de ciclo de vida com confirmação forte e formulário de justificativa.
* **TDD & Validação:**
  * Testes de transição de estados válidos/invalidos e obrigatoriedade de justificativa.

#### Sub-fase 2.3: Gestão de Clientes e Segmentação Operacional [PLANEJADA]
* **Backend:**
  * Taxonomias para segmentação (porte, região, perfil produtivo, risco de churn).
  * Etiquetas operacionais (`Tags`) para campanhas de suporte, sucesso do cliente e retenção.
* **Frontend:**
  * Listas segmentadas com filtros salvos e exportação de recortes operacionais.
* **TDD & Validação:**
  * Testes de performance de consulta e paginação estável em bases grandes.

---

### Phase 3: Catálogo de Planos, Assinaturas e Feature Gating

#### Sub-fase 3.1: Catálogo de Planos Versionado [PLANEJADA]
* **Backend:**
  * Entidades: `PlanCatalog`, `PlanVersion`, `PlanFeature`, `PlanLimit`.
  * Versionamento sem impacto retroativo nas assinaturas vigentes.
  * Publicação controlada por permissão (`plans.publish`) e workflow de aprovação.
* **Frontend:**
  * Tela de planos com comparação de recursos, limites e janela de vigência por versão.
* **TDD & Validação:**
  * Testes garantindo imutabilidade de versões publicadas e migração controlada.

#### Sub-fase 3.2: Assinaturas, Upgrade/Downgrade e Regras de Capacidade [PLANEJADA]
* **Backend:**
  * `ChangeTenantPlanCommand` com regras de elegibilidade e impacto em módulos habilitados.
  * Jobs para enforcement de limites por plano (ex.: capacidade de cabeças, usuários, unidades).
  * Regras de grace period para downgrades com excesso de uso.
* **Frontend:**
  * Fluxo assistido de mudança de plano com pré-visualização de impacto.
* **TDD & Validação:**
  * Testes de integração para upgrade, downgrade, bloqueios por limite e grace period.

#### Sub-fase 3.3: Faturamento Operacional e Eventos de Cobrança [PLANEJADA]
* **Backend:**
  * Registro de eventos de assinatura (ativação, renovação, atraso, cancelamento).
  * Conciliação de status de cobrança e sincronização com módulo de tenancy/licensing.
* **Frontend:**
  * Painel financeiro com aging de inadimplência e ações de regularização.
* **TDD & Validação:**
  * Testes de consistência entre estado da assinatura e acesso a features.

---

### Phase 4: Impersonação Segura, Suporte e Operações Assistidas

#### Sub-fase 4.1: Acesso por Impersonação com Dupla Salvaguarda [PLANEJADA]
* **Backend:**
  * `StartImpersonationSessionCommand` e `StopImpersonationSessionCommand`.
  * Exigir justificativa, ticket de suporte vinculado e permissão específica (`impersonation.start`).
  * Sessões de impersonação com TTL curto, escopo mínimo e trilha completa em `AuditLog`.
* **Frontend:**
  * Banner persistente de sessão impersonada + contador regressivo + botão de encerramento.
  * Modal com justificativa obrigatória e referência de chamado.
* **TDD & Validação:**
  * Testes de segurança para impedir impersonação em tenants bloqueados ou sensíveis.

#### Sub-fase 4.2: Workbench de Suporte N1/N2 [PLANEJADA]
* **Backend:**
  * APIs de diagnóstico assistido (status de sync, filas, falhas recorrentes, módulos ativos).
  * Catálogo de ações remediativas seguras por permissão.
* **Frontend:**
  * Console de suporte com playbooks operacionais e ações contextualizadas.
* **TDD & Validação:**
  * Testes garantindo segregação entre suporte operacional e ações financeiras/sensíveis.

#### Sub-fase 4.3: Gestão de Solicitações Administrativas (4-eyes principle) [PLANEJADA]
* **Backend:**
  * Fluxo de aprovação dupla para ações críticas (suspensão massiva, publicação de plano, acesso ampliado).
  * Entidade `AdminApprovalRequest` com trilha de decisão e expiração.
* **Frontend:**
  * Caixa de aprovações pendentes com diff de impacto e evidências.
* **TDD & Validação:**
  * Testes de workflow garantindo que o solicitante não possa autoaprovar.

---

### Phase 5: Compliance, Auditoria, Risco e Observabilidade

#### Sub-fase 5.1: Auditoria Forense e Retenção de Logs [PLANEJADA]
* **Backend:**
  * Auditoria estruturada para toda ação administrativa (quem, quando, onde, antes/depois).
  * Assinatura de integridade e política de retenção por criticidade.
* **Frontend:**
  * Explorer de auditoria com filtros por ator, tenant, recurso e intervalo temporal.
* **TDD & Validação:**
  * Testes de imutabilidade lógica e rastreabilidade de eventos críticos.

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
