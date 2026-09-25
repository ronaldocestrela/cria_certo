# Runbooks de Emergência e Resposta a Incidentes: CriaCerto Backoffice

## 1. Classificação de Severidade de Incidentes e SLOs

| Nível de Severidade | Definição de Impacto | MTTD (Detecção) | MTTC (Contenção) | Comunicação |
| :--- | :--- | :---: | :---: | :--- |
| **P1 - Crítico** | Indisponibilidade total, violação de cadeia de hash SHA-256, vazamento de PII em massa ou bypass de autorização. | <= 5 minutos | <= 15 minutos | Imediata (Canal #incident-p1 + SMS/Pager) |
| **P2 - Alto** | Degradação de rotinas críticas (impersonação, remediação, publicação de planos com falhas generalizadas). | <= 15 minutos | <= 45 minutos | Canal interno de engenharia e suporte |
| **P3 - Médio** | Falha pontual em sincronização off-line de um tenant específico ou lentidão isolada de consulta. | <= 1 hora | <= 4 horas | Atualização regular em chamado |
| **P4 - Baixo** | Inconsistência cosmética de interface ou dúvida operacional sem bloqueio de fluxo de trabalho. | Próximo dia útil | 3 dias úteis | Fila padrão de sustentação |

---

## 2. Catálogo de Runbooks Operacionais

---

### RUNBOOK-01: Acionamento Emergencial de Kill-Switch (Circuit Breaker)
* **Objetivo**: Interromper instantaneamente uma funcionalidade administrativa instável sem necessidade de rollback de container, redeploy ou reinicialização de pods.
* **Gatilhos**:
  * Taxa de erro de endpoint crítico > 1.0% ou latência p95 > 2.000ms.
  * Comportamento anômalo relatado em sessões de suporte ou remediação.
* **Responsável**: `PlatformOwner` (ou `SupportN2` de plantão sob autorização expressa).

#### Procedimento de Execução:
1. Acesse o console em `/backoffice/rollout`.
2. No painel de **Catálogo de Feature Flags**, localize a flag crítica comprometida:
   - `backoffice.feature.impersonation` (Suporte Assistido)
   - `backoffice.feature.plan_publishing` (Publicação de Planos)
   - `backoffice.feature.tenant_suspension` (Suspensão de Produtores)
   - `backoffice.feature.remediation_execution` (Remediação de Inquilinos)
   - `backoffice.feature.compliance_unmasking` (Desmascaramento LGPD)
3. Clique em **Configurar Flag** ➔ selecione a aba **Zona de Emergência**.
4. Clique no botão vermelho **Ativar Kill-Switch Imediato**.
5. Preencha o modal com justificativa obrigatória (mínimo 10 caracteres, ex: *"Erro 500 recorrente ao carregar tokens de suporte na versão v1.2.4"*).
6. **Efeito Imediato**:
   - Todas as requisições subsequentes interceptadas por `[RequireFeatureFlag]` no MediatR pipeline são abortadas instantaneamente com `Result.Failure(FeatureFlagErrors.KillSwitchActive)`.
   - Um evento com severidade `Critical` é gravado no `AuditLog` com hash encadeado SHA-256 (`Action = KILL_SWITCH_ACTIVATED`).
7. **Pós-Contenção e Restauração**:
   - Após a resolução do bug em ambiente de testes e deploy da correção:
   - Retorne à tela, clique em **Restaurar Operação (Desativar Kill-Switch)** com justificativa formal registrada em auditoria.

---

### RUNBOOK-02: Resposta a Alerta de Adulteração de Hash Forense (`ALR_FORENSIC_TAMPER_DETECTED`)
* **Objetivo**: Conter e investigar suspeita de alteração manual ou maliciosa direta no banco de dados (`AuditLog`), quebrando a cadeia criptográfica SHA-256.
* **Gatilho**: Disparo automático do alerta `ALR_FORENSIC_TAMPER_DETECTED` pelo `AnomalyDetectionEngine`.
* **Responsável**: `PlatformOwner` e Encarregado de Proteção de Dados (DPO).

#### Procedimento de Execução:
1. **Isolamento e Notificação**:
   - Abra a sala de crise (*War Room*) no Slack/Teams.
   - Acesse `/backoffice/observability` e abra o card do alerta ativo.
2. **Identificação do Registro Rompido**:
   - Identifique no payload do alerta o ID do registro (`AuditLogId`), o `RecordHash` calculado e o `PreviousRecordHash`.
   - Execute a consulta de integridade forense via query administrativa `VerifyAuditChainIntegrityQuery`.
3. **Preservação de Evidência**:
   - Exporte o dossiê forense completo assinado digitalmente via `ExportAccessTrailQuery` cobrindo o período da anomalia.
   - Salve a trilha em storage frio (S3 Object Lock / WORM imutável).
4. **Investigação de Causa Raiz**:
   - Verifique acessos diretos ao PostgreSQL via logs de DBA (pgAdmin / IAM RDS/Azure Database).
   - Se for identificado ataque interno ou injeção SQL, revogue imediatamente as credenciais de banco e acione a consultoria jurídica.

---

### RUNBOOK-03: Contenção de Força Bruta ou Violação de RBAC (`ALR_POLICY_BRUTE_FORCE`)
* **Objetivo**: Mitigar tentativas automatizadas de adivinhação de senhas ou exploração de endpoints administrativos.
* **Gatilhos**:
  * Disparo de alerta `ALR_POLICY_BRUTE_FORCE`.
  * Taxa elevada de respostas HTTP `429 Too Many Requests` geradas pelo `BackofficeAuthRateLimiter` (limite padrão: 15 req/min).
* **Responsável**: `PlatformOwner` / SecOps.

#### Procedimento de Execução:
1. Acesse os logs do proxy reverso / Ingress Controller (Nginx / Cloudflare / AWS WAF).
2. Identifique o IP de origem causador da anomalia e o e-mail alvo.
3. Se o ataque for concentrado em uma conta específica:
   - Execute o bloqueio preventivo temporário do usuário administrativo via `LockAdminAccountCommand`.
   - Revogue todos os tokens de sessão ativos da conta alvo.
4. Aplique regra de bloqueio do IP agressor na borda (Cloudflare / WAF com Drop imediato).
5. Após 30 minutos sem novas ocorrências, realize a redefinição assistida de credenciais do usuário afetado.

---

### RUNBOOK-04: Contenção de Token de Impersonação em Rotas Administrativas
* **Objetivo**: Bloquear e investigar tentativas de uso de token efêmero de impersonação (`act_as_tenant`) para acessar endpoints internos do Backoffice.
* **Gatilho**: Ocorrência de log com código de erro `Backoffice.ImpersonationRestricted` disparado pelo `BackofficeAccessMiddleware`.
* **Responsável**: `SupportN2` e `PlatformOwner`.

#### Procedimento de Execução:
1. O middleware já rejeita a requisição preventivamente com `403 Forbidden`.
2. Localize no `AuditLog` o operador de suporte responsável pela sessão de impersonação.
3. Execute imediatamente o encerramento forçado da sessão via `ForceStopImpersonationSessionCommand`.
4. Contate o operador para verificar se foi um desvio acidental de abas no navegador ou comportamento anômalo da máquina do colaborador.

---

### RUNBOOK-05: Revogação Compulsória de Sessões e Desligamento de Operador
* **Objetivo**: Desligar imediatamente qualquer operador de suporte ou financeiro com permissões sensíveis (revogação de privilégios *Zero Trust*).
* **Gatilho**: Demissão, transferência de área ou comprometimento comprovado de credenciais.
* **Responsável**: `PlatformOwner`.

#### Procedimento de Execução:
1. Acesse `/backoffice/users`.
2. Localize o usuário administrativo e clique em **Revogar Acessos & Desativar**.
3. O sistema despacha `RevokeAdminUserCommand`:
   - A conta é marcada como inativa (`IsActive = false`).
   - Todas as sessões ativas (`AdminSession`) e `RefreshToken` associados são revogados imediatamente no banco de dados.
   - O segredo de MFA TOTP é invalidado.
   - Qualquer requisição subsequente com tokens JWT emitidos é rejeitada pelo pipeline com erro `401 Unauthorized`.
4. Emita o relatório de ações recentes do usuário via `GetAdminUserAuditHistoryQuery` para arquivamento no dossiê de RH.

---

## 3. Protocolo Pós-Incidente (Post-Mortem Blameless)

Após a declaração de encerramento do incidente:
1. **Janela de 24 horas**: Redigir o relatório preliminar contendo:
   - Linha do tempo exata com timestamps UTC e BRT.
   - Causa raiz identificada (*5 Whys*).
   - Eficácia das ferramentas de contenção (Kill-Switch, Rate Limiter, Alertas).
2. **Janela de 72 horas**: Reunião de Post-Mortem com os envolvidos focando em melhoria de processos e automações, sem culpabilização individual (*Blameless*).
3. **Plano de Ação Corretiva**: Itens cadastrados no backlog do projeto com prazos de correção e revisão de testes de regressão de segurança `[Trait("Category", "SecurityRegression")]`.
