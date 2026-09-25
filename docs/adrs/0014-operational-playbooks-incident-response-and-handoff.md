# ADR 0014: Playbooks Operacionais, Resposta a Incidentes e Hand-off de Produção do Backoffice

## Status
Aceito (Accepted)

## Contexto
Com a conclusão dos desenvolvimentos funcionais, componentes de interface Blazor, hardening de segurança (Sub-fase 6.1) e esteira de feature flags/rollout por ondas (Sub-fase 6.2), o Backoffice Administrativo do CriaCerto atingiu maturidade técnica para suportar operações de missão crítica no ecossistema pecuário.

Entretanto, a prontidão para produção (*Go-Live Readiness*) exigia a formalização e transição operacional (*Hand-off*):
1. **Padronização de Atendimento e Triagem**: Procedimentos claros para `SupportN1` e `SupportN2` manipularem chamados de produtores, descompassos de sincronização off-line do PWA e remediações assistidas.
2. **Separação de Funções e Controles Financeiros**: Processo operacional para `FinanceOps` publicar versões de planos via aprovação dupla (4-Eyes Workflow) e gerenciar réguas de suspensão por inadimplência.
3. **Contenção Emergencial e Resposta a Incidentes**: Runbooks formais com SLAs claros de detecção (MTTD) e contenção (MTTC) para situações de degradação de serviço, violação de integridade forense SHA-256 e desvios de tokens de impersonação.
4. **Living Documentation & Capacitação**: Eliminação de pontos únicos de conhecimento técnico através de documentação viva e testes de prontidão operacional (*Tabletop Drills*).

## Decisão
Estabelecer o framework operacional padronizado de produção do Backoffice:

1. **Biblioteca Viva de Operações (`/docs/operations/`)**:
   - `playbook_support.md`: Procedimentos padrão de triagem N1, sessões de suporte assistido N2 sob ticket mandatório, remediação assistida e desmascaramento pontual LGPD.
   - `playbook_finance.md`: Versionamento imutável de planos, parametrização zootécnica de limites de rebanho e governança do workflow 4-Eyes.
   - `runbook_incident_response.md`: Runbooks numerados (RUNBOOK-01 a RUNBOOK-05) cobrindo acionamento de Kill-Switch, alerta forense de rompimento de hash (`ALR_FORENSIC_TAMPER_DETECTED`), ataques de força bruta, quebra de contenção de impersonação e revogação emergencial de credenciais.
   - `training_matrix_and_handoff.md`: Matriz RACI operacional, roteiros de treinamento por perfil e termo de responsabilidade.
   - `tabletop_simulation_report.md`: Registro formal da simulação de incidentes de mesa comprovando contenção ponta a ponta.

2. **Invariantes Operacionais Invioláveis**:
   - **Result Pattern Estrito**: Nenhuma falha operacional ou violação de regra em comandos de suporte, faturamento ou contenção deve disparar exceções não tratadas; o sistema deve responder uniformemente com `Result.Failure(Error)`.
   - **Justificativa Mandatória**: Toda e qualquer ação de impersonação, suspensão de produtor, aprovação dupla, desmascaramento LGPD ou Kill-Switch exige justificativa operacional com comprimento mínimo de 10 caracteres.
   - **Auditabilidade Criptográfica Compulsória**: 100% das ações administrativas são registradas em `AuditLog` com hash canônico SHA-256 encadeado e severidade adequada.

3. **Validação Executável em Código**:
   - Criação de testes unitários/integração simulando cenários operacionais de emergência (`IncidentResponseSimulationTests.cs`), garantindo que o comportamento documentado nos runbooks seja testável e executável de forma contínua no CI/CD.

## Consequências
- **Positivas**:
  - Transição segura e documentada da engenharia de desenvolvimento para as operações de suporte e sustentação (*Production Hand-off*).
  - Redução drástica do MTTC (tempo de contenção) de incidentes críticos para menos de 5 minutos via Kill-Switch e isolamento determinístico.
  - Segurança jurídica e conformidade regulatória (LGPD e auditorias fiscais) com rastreabilidade SHA-256 e segregação de funções.
  - 100% de alinhamento com a diretriz de *Living Documentation* de `agents.md`.
- **Negativas**:
  - Requer reciclagem semestral obrigatória das equipes e execução contínua de simulações periódicas de emergência.
