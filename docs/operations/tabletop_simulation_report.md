# Relatório de Simulação de Incidentes Administrativos (Tabletop Drill): CriaCerto Backoffice

## 1. Dados da Sessão de Simulação
* **Data da Realização**: 25/09/2026
* **Ambiente**: Staging / Pre-Production (espelho exato de produção com banco PostgreSQL e telemetria .NET 10)
* **Facilitador**: Engenheiro Líder de Confiabilidade e Segurança (SecOps / Tech Lead)
* **Participantes e Papéis Atribuídos**:
  * `PlatformOwner`: Operador Sênior de Infraestrutura e Governança
  * `SupportN2`: Operador de Suporte Técnico Avançado
  * `FinanceOps`: Analista de Operações Financeiras e Faturamento
  * `ReadOnlyAuditor`: Compliance Officer e DPO

---

## 2. Cenários Simulados e Resultados

### Cenário A: Degradação Severa e Acionamento Emergencial de Kill-Switch
* **Descrição**: Simulação de injeção de latência e falhas na execução do comando de remediação assistida (`ExecuteTenantRemediationCommand`), elevando a taxa de erro para 12% e latência p95 para 3.400ms.
* **Execução**:
  1. *00:00* - Início do disparo de falhas sintéticas no endpoint de remediação.
  2. *00:02* - Telemetria do Backoffice detecta violação de SLO no console `/backoffice/rollout`.
  3. *00:03* - `PlatformOwner` é acionado via canal de alerta de incidentes.
  4. *00:04* - `PlatformOwner` acessa o console, seleciona `backoffice.feature.remediation_execution` e clica em **Ativar Kill-Switch Imediato** fornecendo a justificativa formal.
  5. *00:05* - **Contenção Confirmada**: Todas as tentativas seguintes de disparar o comando foram abortadas pelo MediatR pipeline em menos de 50ms, retornando `Result.Failure(FeatureFlagErrors.KillSwitchActive)` sem lançar exceções.
  6. *00:06* - Evento de auditoria com severidade `Critical` gerado e encadeado com hash SHA-256 no `AuditLog`.
* **Resultado**: **APROVADO**.
  * MTTD: 2 minutos.
  * MTTC: 2 minutos (Tempo total: 4 minutos, bem abaixo do SLO de 15 minutos).

---

### Cenário B: Tentativa de Bypassing de Rotas com Token de Impersonação
* **Descrição**: Simulação de um atacante que capturou um token de suporte assistido (`act_as_tenant`) e tenta enviá-lo para acessar endpoints confidenciais do Backoffice (`/api/v1/backoffice/tenants`).
* **Execução**:
  1. Operador gera sessão legítima de impersonação para atendimento com ticket `SUP-9901`.
  2. Script de teste envia o token de impersonação no cabeçalho `Authorization: Bearer <token>` para o endpoint administrativo.
  3. O `BackofficeAccessMiddleware` intercepta a requisição, detecta a claim de suporte assistido e bloqueia imediatamente com resposta `403 Forbidden` (`Backoffice.ImpersonationRestricted`).
  4. Nenhuma informação administrativa é exposta.
* **Resultado**: **APROVADO**.
  * Contenção determinística por camada de segurança com Zero Trust.

---

### Cenário C: Tentativa de Adulteração de Registro de Auditoria Forense
* **Descrição**: Simulação de modificação forçada em um registro do histórico na tabela `AuditLog` via script de banco, simulando comprometimento interno.
* **Execução**:
  1. Alterou-se o campo `NewValuesJson` do registro histórico #104.
  2. A rotina do `AnomalyDetectionEngine` executou a verificação periódica de integridade calculando o hash canônico SHA-256 e comparando com o `RecordHash` gravado.
  3. Divergência detectada imediatamente: o sistema disparou o alerta `ALR_FORENSIC_TAMPER_DETECTED` com severidade `Critical`.
  4. O operador executou o **RUNBOOK-02**, isolou o registro rompido e emitiu o dossiê forense via `ExportAccessTrailQuery`.
* **Resultado**: **APROVADO**.
  * Rastreabilidade e inviolabilidade comprovadas matematicamente pela cadeia criptográfica encadeada.

---

## 3. Síntese dos Indicadores Aferidos (KPIs da Simulação)

| Métrica Avaliada | Meta (SLA) | Resultado Real na Simulação | Status |
| :--- | :---: | :---: | :---: |
| **Tempo Médio de Detecção (MTTD)** | <= 5 minutos | **2 minutos** | ✅ Aprovado |
| **Tempo Médio de Contenção (MTTC)** | <= 15 minutos | **2 minutos** | ✅ Aprovado |
| **Adesão ao Result Pattern** | 100% (Zero Exceções 500) | **100% (Result.Failure padronizado)** | ✅ Aprovado |
| **Enforcement de Hash Forense SHA-256** | 100% imutável | **100% encadeado e auditado** | ✅ Aprovado |
| **Contenção de Tokens de Impersonação** | 100% bloqueado | **100% bloqueado (403 Forbidden)** | ✅ Aprovado |

---

## 4. Parecer Conclusivo e Sign-Off Operacional
A equipe de engenharia e operações declara que a infraestrutura, os playbooks de suporte e financeiro, e os runbooks de resposta a incidentes do CriaCerto Backoffice estão **plenamente operacionais e homologados** para suportar a operação real do SaaS em produção.
