# ADR 0013: Governança de Rollout por Ondas, Feature Flags e Circuit Breaker de Emergência (Kill-Switch)

## Status
Aceito (Accepted)

## Contexto
O Backoffice Administrativo do SaaS CriaCerto orquestra rotinas críticas com alto potencial de impacto técnico, financeiro e jurídico para produtores rurais e para a operação central da plataforma, nomeadamente:
1. **Impersonação de Suporte Assistido (`backoffice.feature.impersonation`)**: Permite que operadores atuem no contexto de fazendas/produtores.
2. **Publicação de Versões de Planos (`backoffice.feature.plan_publishing`)**: Altera tabelas de preços, limites operacionais e módulos disponíveis.
3. **Suspensão e Cancelamento de Inquilinos (`backoffice.feature.tenant_suspension`)**: Interrompe acessos de produtores em atraso ou violação de termos.
4. **Execução de Remediações de Suporte (`backoffice.feature.remediation_execution`)**: Executa correções de dados e sincronizações assistidas.
5. **Desmascaramento Just-In-Time de Dados LGPD (`backoffice.feature.compliance_unmasking`)**: Revela PII sob justificativa operacional.

Era mandatória a implementação de uma esteira controlada de liberação gradual (*Ring Deployment* / *Wave Rollout*), controle dinâmico sem necessidade de novos deploys de software, monitoramento de conformidade com SLOs de estabilidade e capacidade de corte imediato (*Kill-Switch* / *Circuit Breaker*) em caso de anomalias operacionais ou incidentes de segurança.

## Decisão
Implementar a governança de Rollout por Ondas e Feature Flags integrada ao ecossistema do Backoffice:

1. **Modelo de Anéis de Liberação (Rollout Rings)**:
   - **`Ring 0 (Canary / Core Ops)`**: Acesso restrito a `PlatformOwner` (validação preliminar em ambiente de produção).
   - **`Ring 1 (Early Adopters)`**: `PlatformOwner`, `SupportN2` e `FinanceOps` (liberação controlada de funcionalidades com maior maturidade).
   - **`Ring 2 (General Availability - GA)`**: Disponibilidade geral para todos os operadores administrativos autorizados pelo RBAC.

2. **Algoritmo Determinístico de Avaliação (`IFeatureFlagEvaluator`)**:
   - Para percentuais parciais de rollout (0% a 100%), o sistema calcula o bucket de inclusão de forma determinística via hash criptográfico SHA-256 da composição `Key:AdminUserEmail % 100`. Isso elimina qualquer oscilação ou inconsistência de interface (*flicker*) durante a navegação do operador.
   - Suporte a lista branca explícita (*whitelisting*) de e-mails para validação pontual de testes.

3. **Corte Emergencial Imediato (Kill-Switch & Circuit Breaker)**:
   - Acionamento imediato com justificativa operacional obrigatória (mínimo 10 caracteres).
   - O Kill-Switch tem precedência absoluta sobre anéis, porcentagens ou whitelists, forçando desativação instantânea em memória e em banco de dados.
   - Registro compulsório de evento de auditoria forense criptográfica (`AuditLog`) com severidade `Critical` e encadeamento SHA-256 (`Category = Rollout`, `Action = "KILL_SWITCH_ACTIVATED"`).
   - Restauração formal também auditada (`Action = "KILL_SWITCH_DEACTIVATED"`).

4. **Intercepção Declarativa no MediatR Pipeline (`FeatureFlagEvaluationBehavior`)**:
   - Criação do atributo declarativo `[RequireFeatureFlag("chave.da.flag")]`.
   - Pipeline behavior que intercepta comandos e consultas marcados antes de alcançarem o handler de domínio. Se a flag estiver inativa, em anel superior ao do operador ou sob Kill-Switch, a requisição é abortada imediatamente retornando `Result.Failure` tipado (`FeatureFlagErrors.FeatureFlagDisabled`, `FeatureFlagErrors.WaveNotReached` ou `FeatureFlagErrors.KillSwitchActive`).
   - Comandos críticos decorados: `StartImpersonationSessionCommand`, `PublishPlanVersionCommand`, `SuspendTenantAdminCommand`, `ExecuteTenantRemediationCommand` e `RevealSensitiveDataCommand`.

5. **Guardas de Interface e Console de Operação**:
   - Componente atômico Blazor `FeatureFlagGuard.razor` com escuta reativa a eventos `OnFlagsChanged`.
   - Página administrativa `/backoffice/rollout` com KPI cards, tabela de conformidade de SLOs em tempo real (taxa de erro e latência p95) e modal de configuração rápida de anéis, percentuais e Kill-Switch.
   - Menu lateral `BackofficeNavMenu.razor` atualizado com claim `rollout.read`.

## Consequências
- **Positivas**:
  - Implantação e desacoplamento seguro entre deploy de código e liberação de funcionalidade (*dark launching*).
  - Mitigação de raio de explosão (*blast radius reduction*) em atualizações críticas.
  - Corte emergencial determinístico em menos de 1 segundo sem necessidade de rollback de container ou reinicialização de pods.
  - Conformidade estrita com o princípio da Não-Exceção para fluxo de controle (Result Pattern).
  - 100% de cobertura por testes automatizados (unitários, integração e cliente Blazor).
- **Negativas**:
  - Requer governança contínua para evitar acúmulo de flags legadas (*flag debt*) após maturidade e conclusão do ciclo de rollout para GA.
