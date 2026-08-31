# ADR 0010: Observabilidade de Backoffice, Telemetria Estruturada (.NET 10) e Motor de Alertas de Anomalia

## Status
Aceito (Accepted)

## Contexto
O módulo administrativo `Modules.Backoffice` executa operações de alta sensibilidade para a governança SaaS do CriaCerto, incluindo alterações de planos de assinatura, impersonação de sessões de produtores rurais, aprovações de duplo controle (4-Eyes) e expurgo de logs de auditoria. Para garantir confiabilidade operacional, conformidade de segurança e prevenção ativa contra violações:
1. É necessário monitorar continuamente a saúde e a latência de consultas e comandos administrativos.
2. É indispensável detectar comportamentos anômalos em tempo hábil (ex: varreduras de permissão/força bruta, ações críticas fora de janela operacional, surtos de impersonação ou adulteração na trilha forense).
3. O time de engenharia e operações precisa de um console unificado para visualizar métricas, inspecionar incidentes em tempo real e realizar triagens estruturadas (reconhecimento e resolução documentada).

## Decisão
Implementar o subsistema de **Observabilidade de Backoffice e Governança de Alertas de Anomalia**:

1. **Telemetria Estruturada com APIs Nativas do .NET 10 (`System.Diagnostics`)**:
   - `Meter("CriaCerto.Modules.Backoffice", "1.0.0")`: Instrumentação de contadores de ações (`backoffice.admin_actions.total`), falhas de política (`backoffice.policy_failures.total`), medidor de sessões ativas (`backoffice.impersonation_sessions.active`), histograma de latência (`backoffice.operation_latency.duration_ms`) e alertas disparados (`backoffice.alerts.triggered.total`).
   - `ActivitySource("CriaCerto.Modules.Backoffice", "1.0.0")`: Rastreamento distribuído de spans para operações administrativas críticas.
   - Pipeline Behavior MediatR `BackofficeObservabilityBehavior<TRequest, TResponse>`: Coleta automatizada e transparente de latência e criação de spans sem poluir os command/query handlers.

2. **Motor de Detecção de Anomalias (`IAnomalyDetectionEngine` / `AnomalyDetectionEngine`)**:
   - `ALR_POLICY_BRUTE_FORCE`: Disparado quando um mesmo IP ou ator acumula falhas consecutivas de autenticação ou autorização (threshold ≥ 5).
   - `ALR_OFF_HOURS_CRITICAL_ACTION`: Disparado quando ações de severidade `Critical` ou `High` são executadas fora da janela operacional (22h às 06h BRT ou finais de semana).
   - `ALR_IMPERSONATION_BURST`: Disparado quando um operador inicia volume anômalo de sessões de suporte assistido em curto intervalo (threshold ≥ 3).
   - `ALR_FORENSIC_TAMPER_DETECTED`: Disparado imediatamente quando uma inconsistência ou quebra de hash SHA-256 é identificada na trilha de auditoria forense.

3. **Modelo de Domínio e Ciclo de Vida do Alerta (`BackofficeAlert`)**:
   - Estados: `Active` ➔ `Acknowledged` (em triagem) ➔ `Resolved` (resolvido com justificativa técnica obrigatória).
   - Deduplicação inteligente (`Fingerprint`) para prevenir saturação de alertas (alert fatigue), incrementando o contador `OccurrenceCount` e atualizando o snapshot `ContextJson`.
   - Mapeamento no `BackofficeDbContext` com índices compostos em `(Status, Severity)`, `Fingerprint` e `LastTriggeredAtUtc`.

4. **Console Visual Blazor WASM (`OperationalHealthDashboard.razor`)**:
   - Rota `/backoffice/observability` com indicadores em tempo real (status de integridade forense, alertas críticos, falhas de política, sessões de impersonação ativas).
   - Feed interativo de incidentes com filtros multifatoriais, paginação e modais de triagem detalhada (`AlertDetailModal.razor`) e simulação operacional (`SimulateAlertModal.razor`).

5. **Governança Granular RBAC**:
   - Novas permissões `observability.read` e `observability.manage`, protegendo endpoints e componentes visuais de acordo com o princípio do menor privilégio.

## Consequências
- **Positivas**:
  - Visibilidade em tempo real do estado operacional e de segurança da plataforma.
  - Mitigação de fadiga de alertas graças ao agrupamento determinístico por `Fingerprint`.
  - Conformidade com padrões de auditoria contínua e resposta estruturada a incidentes.
  - Zero dependência de bibliotecas pesadas externas para telemetria (uso estrito da BCL nativa do .NET 10).
- **Negativas**:
  - Necessidade de calibragem periódica dos limiares de anomalia (thresholds) conforme a base de operadores e a volumetria do SaaS evoluem.
