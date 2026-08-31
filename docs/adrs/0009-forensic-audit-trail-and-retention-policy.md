# ADR 0009: Trilha Forense de Auditoria, Integridade Criptográfica (Tamper-Evident) e Retenção por Criticidade

## Status
Aceito (Accepted)

## Contexto
O ecossistema CriaCerto processa operações administrativas críticas (alterações contratuais e de planos, impersonação de sessões de produtores, deliberações 4-Eyes, suspensões massivas de fazendas e remediações técnicas). Para conformidade com regulamentações de segurança corporativa, normas fiscais/LGPD e frameworks SOC 2 / ISO 27001, é imperativo dispor de:
1. **Auditoria Estruturada e Rastreabilidade Completa**: Registro forense de *quem*, *quando*, *onde*, *o quê/alvo* e *antes/depois* (mutações de estado).
2. **Imutabilidade Comprovável (Tamper-Evident)**: Garantia de que qualquer alteração direta indevida ou expurgo clandestino no banco de dados seja imediatamente detectado.
3. **Governança do Ciclo de Vida e Retenção**: Prevenção de crescimento descontrolado da base sem colocar em risco evidências fiscais ou de segurança, com políticas de retenção estratificadas por severidade.

## Decisão
Implementar o subsistema de **Auditoria Forense, Integridade Criptográfica e Retenção de Logs** no módulo `Modules.Backoffice`:

1. **Modelo de Dados Estruturado (`AuditLog`)**:
   - Campos canônicos: `AdminUserId`, `AdminUserEmail`, `ActorRole`, `Action`, `Category`, `Severity` (`Low`, `Medium`, `High`, `Critical`), `Resource`, `TargetTenantId`, `TargetTenantName`, `IpAddress`, `UserAgent`, `OldValuesJson`, `NewValuesJson`, `DetailsJson` e `TimestampUtc`.
   - Sobrecarga retrocompatível com geração automática de hash para callers existentes e método factory estruturado `CreateForensic(...)`.

2. **Assinatura Criptográfica e Encadeamento Sequencial**:
   - Cada registro gera um hash canônico SHA-256 (`RecordHash`) dos atributos do evento.
   - Encadeamento sequencial pelo hash do registro anterior (`PreviousRecordHash`), permitindo validação contínua da cadeia de integridade (`VerifyIntegrity()`) e identificação de registros corrompidos ou excluídos arbitrariamente.

3. **Política de Retenção e Ciclo de Vida por Criticidade**:
   - Prazos mínimos configuráveis:
     - `Critical`: Permanente / 5 anos (1825 dias) — Elevação de privilégio, 4-Eyes, impersonação. **Nunca é expurgado por rotinas automáticas**, apenas arquivado a frio (`IsArchived = true`).
     - `High`: 3 anos (1095 dias) — Publicação de planos, transações financeiras e faturamento.
     - `Medium`: 1 ano (365 dias) — Edições cadastrais de tenants e tags operacionais.
     - `Low`: 90 dias — Consultas diagnósticas e visualizações. Elegíveis para expurgo físico.
   - Execução via `ApplyAuditRetentionPolicyCommand` com suporte a `DryRun` e gravação auditada com severidade crítica da própria operação de retenção.

4. **Console Visual Blazor (`AuditExplorer.razor`)**:
   - Interface completa em `/backoffice/audit` com 4 cards de KPIs operacionais e status da cadeia de integridade.
   - Barra de filtros avançados por ator, tenant, severidade, categoria, texto livre e período.
   - Modal forense `AuditLogDetailModal.razor` com comparador de **Diff Antes vs Depois**, metadados de rede e selo de verificação de hash.
   - Modal `AuditRetentionModal.razor` para simulação e execução de expurgo por administradores credenciados.

## Consequências
- **Positivas**:
  - Detecção imediata de violações ou tentativas de adulteração em registros de auditoria.
  - Auditoria completa antes/depois facilitando investigações forenses e incidentes de suporte.
  - Gestão eficiente de armazenamento com expurgo seguro de telemetria de baixo impacto e arquivamento a frio de logs vitais.
- **Negativas**:
  - Pequeno overhead computacional no cálculo do hash SHA-256 no momento da persistência (insignificante para volume administrativo).
