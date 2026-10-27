# Runbooks de Emergência e Resposta a Incidentes: CriaCerto Backoffice

## 1. Classificação de Severidade de Incidentes e SLOs

| Nível de Severidade | Definição de Impacto | MTTD (Detecção) | MTTC (Contenção) | Comunicação |
| :--- | :--- | :---: | :---: | :--- |
| **P1 - Crítico** | Indisponibilidade total, violação de cadeia de hash SHA-256, vazamento de PII em massa ou bypass de autorização. | <= 5 minutos | <= 15 minutos | Imediata (Canal #incident-p1 + SMS/Pager) |
| **P2 - Alto** | Degradação de rotinas críticas (impersonação, remediação, publicação de planos com falhas generalizadas). | <= 15 minutos | <= 45 minutos | Canal interno de engenharia e suporte |
| **P3 - Médio** | Falha pontual em sincronização off-line de um tenant específico ou lentidão isolada de consulta. | <= 1 hora | <= 4 horas | Atualização regular em chamado |
| **P4 - Baixo** | Inconsistência cosmética de interface ou dúvida operacional sem bloqueio de fluxo de trabalho. | Próximo dia útil | 3 dias úteis | Fila padrão de sustentação |
| **Privacidade (Art. 48)** | Incidente com dados pessoais acarretando risco ou dano relevante (LGPD e Res. ANPD 15/2024). | <= 15 minutos | <= 1 hora | ANPD & Titulares: <= 3 dias úteis<br>Controlador B2B: <= 24 horas |

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

### RUNBOOK-06: Investigação de Falhas de Assinatura de Webhook Stripe (`ALR_WEBHOOK_SIGNATURE_INVALID`)
* **Objetivo**: Conter tentativas de requisições forjadas contra o endpoint `/api/v1/payments/stripe-webhook` ou corrigir descompasso de configuração no segredo de assinatura (`STRIPE_WEBHOOK_SECRET`).
* **Gatilho**: Disparo do alerta crítico `ALR_WEBHOOK_SIGNATURE_INVALID` pelo subsistema de observabilidade.
* **Responsável**: Engenharia de Plataforma / SecOps e Suporte N2.

#### Procedimento de Execução:
1. Acesse o console em `/backoffice/observability` e abra o card de detalhes do alerta `ALR_WEBHOOK_SIGNATURE_INVALID`.
2. Inspecione o payload no campo `ContextJson` verificando o motivo da recusa (`reason`), o tamanho do payload (`payloadLength`) e a janela temporal.
3. **Cenário A: Descompasso de Configuração (Ambiente Local / Homologação / Produção)**:
   - Se houver múltiplos eventos legítimos sendo rejeitados simultaneamente, verifique se a variável de ambiente `STRIPE_WEBHOOK_SECRET` no container/AppService corresponde ao segredo configurado no dashboard do Stripe em **Developers ➔ Webhooks**.
   - Em caso de rotação de segredo, atualize o segredo no Azure Key Vault / App Settings e realize o restart do serviço.
4. **Cenário B: Tentativa de Ataque Externo (Replay ou Spoofing)**:
   - Se o segredo estiver correto e o tráfego anômalo vier de IPs não pertencentes à rede oficial de webhooks do Stripe:
   - Inspecione os logs de borda (WAF / Cloudflare) e aplique regra de bloqueio imediato para os IPs agressores.
   - Valide se a lista oficial de IPs de Webhook do Stripe (`api.stripe.com`) está habilitada no Ingress.
5. Marque o incidente como `Acknowledged` durante a triagem e como `Resolved` após validação no painel com notas técnicas.

---

### RUNBOOK-07: Gestão e Notificação de Incidente de Segurança com Dados Pessoais (LGPD Art. 48 & Resolução CD/ANPD nº 15/2024)
* **Objetivo**: Padronizar as ações imediatas de contenção técnica, avaliação de risco e dano relevante, comunicação institucional à Autoridade Nacional de Proteção de Dados (ANPD), notificação aos titulares afetados e aos clientes B2B (Produtores Rurais / Controladores sob DPA).
* **Gatilhos**:
  * Suspeita confirmada de vazamento, exfiltração, acesso indevido ou alteração não autorizada de dados pessoais (PII).
  * Despejo não autorizado de banco de dados, falha de isolamento multi-tenant ou bypass de autorização em endpoints de exportação (`ExportUserDataPortabilityQuery`, `ExportAccessTrailQuery`).
  * Comprometimento de credenciais de usuários, administradores, operadores rurais ou chaves de decifra AES-256-GCM.
* **Responsáveis**: Encarregado pelo Tratamento de Dados Pessoais (DPO), `PlatformOwner` (SecOps / Infraestrutura) e Consultoria Jurídica.

#### Matriz de Risco e Dano Relevante (Resolução CD/ANPD nº 15/2024)

| Nível de Severidade | Cenário Típico de Dados | Ação Obrigatória | Prazo Legal / SLA |
| :--- | :--- | :--- | :--- |
| **P1-PRIV (Crítico)** | Vazamento massivo de PII em claro, credenciais ativas, dados financeiros/bancários ou chaves criptográficas. | Sala de Crise imediata + Notificação ANPD + Notificação aos Titulares + Notificação B2B | **3 dias úteis** (ANPD/Titulares)<br>**24 horas** (B2B DPA) |
| **P2-PRIV (Alto)** | Exposição de PII com risco direto de fraude (ex: CPF + Telefone + Nome de produtores ou operadores de curral). | Contenção imediata + Parecer do DPO + Notificação ANPD + Titulares afetados | **3 dias úteis** (ANPD/Titulares)<br>**24 horas** (B2B DPA) |
| **P3-PRIV (Médio)** | Exposição de metadados operacionais ou dados cadastrais sem credenciais e sem dados financeiros. | Contenção rápida + Avaliação casuística pelo DPO se há dano relevante. | 24 horas para avaliação do DPO |
| **P4-PRIV (Baixo)** | Dados cifrados em repouso com **AES-256-GCM** vazados sem a chave mestra, ou exfiltração bloqueada em tempo real por WAF. | Registro formal interno no Livro de Incidentes e ROPA (Art. 37). Desobrigada notificação externa. | Registro em até 5 dias úteis |

#### Prazos Legais e SLAs Mandatórios:
1. **Momento da Ciência Inequívoca (T0)**: O marco temporal inicial é o momento exato em que a equipe técnica ou o DPO confirma a ocorrência do incidente de segurança com dados pessoais.
2. **Notificação Preliminar à ANPD**: Prazo improrrogável de **3 (três) dias úteis**, contados a partir de T0, via peticionamento eletrônico no Sistema de Notificação de Incidentes da ANPD.
3. **Comunicação aos Titulares de Dados**: Prazo de até **3 (três) dias úteis**, em linguagem clara, direta e acessível, alertando sobre os riscos e medidas recomendadas.
4. **Notificação B2B a Produtores Rurais (DPA - ADR 0022)**: Prazo máximo de **24 horas** a contar de T0 quando o incidente envolver operadores de fazenda, vaqueiros ou membros convidados (`TeamInvite`), permitindo ao produtor rural (Controlador) tomar as providências cabíveis.
5. **Relatório Conclusivo à ANPD**: Prazo de até **20 (vinte) dias úteis**, contados da notificação preliminar, para protocolar a apuração definitiva de causa raiz e impacto final.

#### Procedimento de Execução por Fases:

##### Fase 1: Identificação, Triage e Ativação da Sala de Crise (T+0 a T+2h)
1. O operador que identificar o indício de vazamento aciona imediatamente o canal `#incident-privacy-war-room`.
2. Notificação telefônica/SMS imediata ao DPO (`dpo@criacerto.com.br`) e ao `PlatformOwner`.
3. Congelamento da cena: proibido reiniciar instâncias de banco ou sobrescrever logs voláteis antes da extração de dumps forenses.

##### Fase 2: Contenção Técnica e Preservação de Evidências (T+2h a T+4h)
1. Se o vetor for um endpoint com falha funcional, acione o **Kill-Switch Imediato (RUNBOOK-01)** no console `/backoffice/rollout`.
2. Se houver suspeita de comprometimento de credenciais administrativas, execute o **RUNBOOK-05** revogando compulsoriamente sessões de suporte e tokens.
3. Execute `VerifyAuditTrailIntegrityQuery` para certificar que os registros de auditoria SHA-256 não sofreram adulteração.
4. Exporte o dossiê de trilha de acessos via `ExportAccessTrailQuery` e arquive em storage frio WORM com hash assinado.
5. Se houver suspeita de vazamento de chave simétrica, inicie a rotação de chave do chaveiro `KeyRing` via `POST /api/v1/backoffice/referral/reencrypt-contracts` (ADR 0020).

##### Fase 3: Avaliação de Dano Relevante pelo DPO (T+4h a T+24h)
1. O DPO e a equipe técnica submetem os dados à rotina de avaliação `PrivacyIncidentEvaluator.Evaluate()`.
2. Se confirmada severidade `High` ou `Critical` ou exposição de PII em claro com risco aos titulares, formaliza-se a decisão compulsória de notificação à ANPD e aos titulares.
3. Registra-se o parecer técnico e a decisão no `AuditLog` com severidade `Critical` e categoria `Compliance` (`Action = "SECURITY_INCIDENT_ASSESSED"`).

##### Fase 4: Protocolo da Notificação Preliminar à ANPD (Até 3 dias úteis)
1. O DPO gera o relatório preliminar oficial através de `PrivacyIncidentEvaluator.GenerateAnpdPreliminaryReport()`.
2. Protocolar o formulário eletrônico no portal oficial de peticionamento da ANPD (ou sistema Super/SEI da ANPD).
3. Obter e anexar o número do processo SEI/protocolo da ANPD ao dossiê de compliance.

##### Fase 5: Notificação aos Titulares de Dados Afetados (Até 3 dias úteis)
1. Disparar comunicado individual por e-mail transacional autenticado via Resend (`IEmailSender`) para todos os titulares cujos e-mails foram confirmados.
2. Para incidentes que envolvam público massivo ou contatos desatualizados, publicar Nota de Transparência no canal oficial `/dpo` e cabeçalho institucional em `/privacidade/canal-dpo`.
3. Disponibilizar fila de suporte prioritário com os canais de atendimento do DPO (`dpo@criacerto.com.br`).

##### Fase 6: Comunicação B2B aos Clientes / Fazendas sob o DPA (Até 24 horas)
1. Para tenants afetados onde a CriaCerto opera dados de colaboradores rurais (DPA - ADR 0022), enviar dossiê técnico formal ao administrador da fazenda.
2. Informar as medidas já tomadas para proteger o rebanho e os operadores da fazenda, além das orientações técnicas cabíveis.

##### Fase 7: Pós-Incidente, Remediação e Relatório Conclusivo à ANPD (Até 20 dias úteis)
1. Finalização da investigação de causa raiz e implementação definitiva de correções no código, validadas pela suíte de testes com tag `[Trait("Category", "SecurityRegression")]`.
2. Reunião de Post-Mortem Blameless conforme Seção 3 deste documento.
3. Protocolo do Relatório Conclusivo à ANPD detalhando:
   - Confirmação do número final de titulares afetados.
   - Demonstração da eficácia das medidas de contenção (Kill-Switch, revogações, criptografia).
   - Lições aprendidas e plano de conformidade continuada.

---

#### Template Canônico: Relatório Preliminar de Notificação à ANPD (Art. 48)

```markdown
# COMUNICAÇÃO DE INCIDENTE DE SEGURANÇA À ANPD (RELATÓRIO PRELIMINAR)
**Protocolo Interno**: INC-[YYYYMMDD]-[XXXX]
**Data e Hora da Comunicação**: [DD/MM/AAAA HH:MM:SS] UTC

## 1. Identificação do Agente de Tratamento
* **Razão Social**: CriaCerto Bovino SaaS Ltda.
* **CNPJ**: 12.345.678/0001-90
* **Endereço**: [Endereço da sede]
* **Qualificação**: Controlador (e Operador para dados de operadores rurais B2B)

## 2. Identificação do Encarregado pelo Tratamento de Dados Pessoais (DPO)
* **Nome do Encarregado**: [Nome do DPO]
* **E-mail Institucional**: dpo@criacerto.com.br
* **Canal Oficial de Atendimento**: https://app.criacerto.com.br/dpo

## 3. Cronologia dos Fatos
* **Data e Hora da Ocorrência Estimada**: [DD/MM/AAAA HH:MM:SS] UTC
* **Data e Hora da Ciência Inequívoca pelo Controlador (T0)**: [DD/MM/AAAA HH:MM:SS] UTC
* **Data e Hora da Contenção Técnica**: [DD/MM/AAAA HH:MM:SS] UTC
* **Tempo Decorrido até a Contenção**: [X] horas

## 4. Descrição do Incidente e Categorias de Dados Afetadas
* **Vetor de Exploração / Causa Raiz**: [Descrição técnica detalhada, ex: exploração indevida de token, falha de isolamento, injeção contida]
* **Categorias de Dados Pessoais Comprometidas**: [ex: Nome, CPF, Telefone, E-mail, Registros de Manejo no Rebanho]
* **Havia Dados Sensíveis (Art. 5º, II) ou de Menores?**: [Sim/Não - Especificar]
* **Estimativa de Titulares Afetados**: [Número exato ou faixa estimada]

## 5. Riscos e Impactos aos Direitos dos Titulares
* [Detalhamento de possíveis danos materiais, morais, discriminação, engenharia social ou phishing]

## 6. Medidas Técnicas e Administrativas de Contenção Adotadas
* [Acionamento emergencial de Kill-Switch / Circuit Breaker com corte < 1s]
* [Revogação de sessões ativas e invalidação de credenciais]
* [Verificação de integridade forense SHA-256 no banco de dados]
* [Isolamento de containers e aplicação de regras de borda WAF/Cloudflare]

## 7. Plano de Comunicação aos Titulares Afetados
* [Envio individualizado de e-mails transacionais via Resend]
* [Publicação de comunicado de transparência em /dpo]
* [Instruções para os titulares, como troca de senhas e alertas de phishing]

## 8. Prazos e Cronograma para Apresentação do Relatório Conclusivo
* Previsão de envio do relatório complementar e conclusivo em até 20 dias úteis: [DD/MM/AAAA].
```

---

#### Template: Notificação por E-mail aos Titulares Afetados

```text
Assunto: Comunicado Importante de Segurança da Informação - CriaCerto

Prezado(a) [Nome do Titular],

Em respeito ao nosso compromisso com a transparência e em conformidade com o Art. 48 da Lei Geral de Proteção de Dados (LGPD), informamos que identificamos e contivemos um incidente de segurança em nossa infraestrutura no dia [Data].

O que aconteceu:
Nossa equipe de segurança detectou um acesso não autorizado que pode ter exposto as seguintes informações cadastrais: [Categorias: Nome, CPF, Telefone]. Senhas e dados de cartão de crédito não foram comprometidos em texto claro.

O que já fizemos para protegê-lo(a):
1. Acionamos nossos mecanismos emergenciais de contenção e isolamos o sistema em minutos.
2. Revogamos preventivamente as sessões ativas de acesso.
3. Notificamos formalmente a Autoridade Nacional de Proteção de Dados (ANPD).

Medidas recomendadas a você:
- Redefina sua senha de acesso na plataforma através do link oficial: https://app.criacerto.com.br/forgot-password
- Permaneça atento(a) a e-mails ou mensagens suspeitas solicitando confirmação de dados ou transferências financeiras (phishing). O CriaCerto nunca solicita sua senha por WhatsApp ou telefone.

Nosso Encarregado pelo Tratamento de Dados Pessoais (DPO) está à disposição para esclarecimentos adicionais através do e-mail dpo@criacerto.com.br ou pelo portal https://app.criacerto.com.br/dpo.

Atenciosamente,
Equipe de Segurança e Privacidade CriaCerto
```

---

## 3. Protocolo Pós-Incidente (Post-Mortem Blameless)

Após a declaração de encerramento do incidente:
1. **Janela de 24 horas**: Redigir o relatório preliminar contendo:
   - Linha do tempo exata com timestamps UTC e BRT.
   - Causa raiz identificada (*5 Whys*).
   - Eficácia das ferramentas de contenção (Kill-Switch, Rate Limiter, Alertas).
2. **Janela de 72 horas**: Reunião de Post-Mortem com os envolvidos focando em melhoria de processos e automações, sem culpabilização individual (*Blameless*).
3. **Plano de Ação Corretiva**: Itens cadastrados no backlog do projeto com prazos de correção e revisão de testes de regressão de segurança `[Trait("Category", "SecurityRegression")]`.
