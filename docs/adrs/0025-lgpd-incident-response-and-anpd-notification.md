# ADR 0025: Gestão e Notificação de Incidentes de Segurança com Dados Pessoais (RUNBOOK-07 & Art. 48 LGPD)

## Status
Aceito (Accepted)

## Contexto
O ecossistema **CriaCerto Bovino SaaS** manipula dados pessoais de titulares distribuídos em duas frentes:
1. **Relação Direta/B2C**: Dados cadastrais, fiscais e credenciais de produtores rurais, gestores e parceiros comerciais (CriaCerto atua como **Controladora**).
2. **Relação B2B**: Dados de colaboradores da fazenda, vaqueiros e operadores de curral cadastrados para atribuição de manejos zootécnicos e sanitários (CriaCerto atua como **Operadora** nos termos do [DPA - ADR 0022](file:///home/rony/LPR/CriaCerto/docs/adrs/0022-lgpd-b2b-data-processing-agreement-dpa.md)).

O **Art. 48 da Lei Geral de Proteção de Dados (LGPD - Lei nº 13.709/2018)** e a **Resolução CD/ANPD nº 15/2024** (novo Regulamento de Comunicação de Incidente de Segurança da ANPD) impõem requisitos mandatórios:
- Dever legal de comunicação à ANPD e aos titulares quando o incidente acarretar **risco ou dano relevante**.
- Prazo estrito de **3 (três) dias úteis**, contados a partir da ciência inequívoca do incidente (T0), para a notificação à autoridade e aos titulares afetados.
- Prazo de até **20 (vinte) dias úteis** para apresentação do relatório conclusivo complementar à ANPD.
- Dever contratual do Operador em notificar o Controlador com celeridade (SLA de até **24 horas** no CriaCerto) para municiar o produtor rural com os dados necessários à sua própria comunicação regulatória.

Sem um runbook formal e automação de apoio à governança de incidentes de privacidade, a organização ficava sujeita a:
- Intempestividade na comunicação à autoridade nacional, com risco de sanções administrativas (Art. 52 da LGPD).
- Falta de critérios objetivos para discernir incidentes de baixo risco (dados já protegidos por cifra simétrica AES-256-GCM sem chave comprometida) de incidentes que impõem comunicação compulsória.
- Ausência de modelo padronizado e auditável para o relatório preliminar exigido pela ANPD.

## Decisão

Instituiu-se formalmente o framework de governança e resposta técnica a incidentes com dados pessoais:

### 1. Procedimento Operacional Padrão RUNBOOK-07
Adicionou-se a [`docs/operations/runbook_incident_response.md`](file:///home/rony/LPR/CriaCerto/docs/operations/runbook_incident_response.md) o **RUNBOOK-07: Gestão e Notificação de Incidente de Segurança com Dados Pessoais (LGPD Art. 48 & Resolução CD/ANPD nº 15/2024)**, estruturado em 7 fases:
- **Fase 1**: Identificação, Triage e Ativação da Sala de Crise (`#incident-privacy-war-room`, DPO, SecOps e Legal).
- **Fase 2**: Contenção Técnica e Preservação de Evidências (Kill-Switch emergencial [RUNBOOK-01], revogação de sessões [RUNBOOK-05], conferência de hash SHA-256 via `VerifyAuditTrailIntegrityQuery` e extração de dossiê forense via `ExportAccessTrailQuery`).
- **Fase 3**: Avaliação de Dano Relevante pelo DPO e Segurança via motor determinístico.
- **Fase 4**: Protocolo da Notificação Preliminar à ANPD no prazo improrrogável de até 3 dias úteis.
- **Fase 5**: Comunicação Transparente aos Titulares de Dados em até 3 dias úteis por e-mail transacional autenticado via Resend (`IEmailSender`) e nota em `/dpo`.
- **Fase 6**: Notificação B2B em até 24 horas ao Produtor Rural / Fazenda (Controlador sob DPA).
- **Fase 7**: Pós-Incidente, Remediação, Post-Mortem Blameless e Relatório Conclusivo à ANPD em até 20 dias úteis.

### 2. Motor Técnico de Avaliação e Templates Canônicos (`PrivacyIncidentEvaluator`)
Implementou-se no módulo Backoffice (`CriaCerto.Modules.Backoffice.Application.Features.Compliance.IncidentResponse`):
- `PrivacyIncidentSeverity`: Classificação em `Low`, `Medium`, `High` e `Critical`.
- `PrivacyIncidentEvaluator`:
  - Avaliação de risco e dano relevante com base na tipologia dos dados (PII em texto claro vs cifrado com AES-256-GCM, credenciais e dados financeiros).
  - Algoritmo de cálculo de dias úteis (`AddBusinessDays`), assegurando precisão legal contra finais de semana.
  - Geração canônica do payload do Relatório Preliminar de Notificação de Incidente à ANPD (`AnpdPreliminaryIncidentReportDto`), aderente à Resolução CD/ANPD nº 15/2024.

### 3. Living Documentation e Validação Contínua (Tabletop Drill no CI/CD)
- Cobertura por testes unitários dedicados em `PrivacyIncidentEvaluatorTests.cs` (avaliação de criticidade, cálculo de prazos, contenção sem vazamento de chaves e estrutura canônica do relatório).
- Simulação executável de ponta a ponta em `IncidentResponseSimulationTests.cs` (`Runbook07_SecurityIncidentAssessmentAndAnpdNotification_ExecutesEndToEndSimulationWithForensicAudit`), demonstrando em código a geração do relatório de notificação e o encadeamento do registro forense de auditoria com severidade `Critical` no `AuditLog`.

## Consequências

### Positivas
- **Segurança Jurídica e Conformidade**: Pleno atendimento ao Art. 48 da LGPD e à Resolução CD/ANPD nº 15/2024, mitigando riscos de sanções regulatórias.
- **Celeridade Operacional**: Eliminação de dúvidas durante crises graves; roteiro claro com templates preenchíveis para despacho imediato à ANPD e aos titulares.
- **Harmonização B2B**: Proteção da relação com os produtores rurais através do SLA de 24 horas acordado no DPA.
- **Living Documentation**: 100% de alinhamento com `agents.md`, com testes de simulação executáveis no pipeline de integração contínua.

### Negativas / Mitigações
- Exige realização de simulações periódicas (*tabletop drills*) com o time de engenharia e o DPO para reciclagem das equipes.
