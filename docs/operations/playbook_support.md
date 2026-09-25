# Playbook Operacional de Suporte Técnico e Atendimento: CriaCerto Backoffice

## 1. Visão Geral e Propósito
Este playbook estabelece os procedimentos operacionais padrão (SOP) para as equipes de atendimento e engenharia de suporte do SaaS CriaCerto:
- **`SupportN1` (Atendimento Nível 1 - Triagem e Diagnóstico)**: Acesso em modo leitura e consultas não invasivas.
- **`SupportN2` (Atendimento Nível 2 - Suporte Avançado e Remediação)**: Operações assistidas, sessões de impersonação com dupla salvaguarda, remediação de dados e desmascaramento pontual LGPD.

Toda a interação é governada pelo modelo de privilégio mínimo (Least Privilege), Default Deny e auditoria imutável via hash encadeado SHA-256.

---

## 2. Matriz de Perfis e Permissões de Suporte

| Capacidade Operacional | Permissão Requerida | Perfil N1 | Perfil N2 | Observação |
| :--- | :--- | :---: | :---: | :--- |
| **Consulta de Produtores / Fazendas** | `tenants.read` | ✅ | ✅ | Busca por CNPJ, Razão Social, UF e Segmento. |
| **Visualização de Assinaturas** | `subscriptions.read` | ✅ | ✅ | Consulta de plano vigente, limites e ciclo. |
| **Consulta da Trilha de Auditoria** | `audit.read` | ✅ | ✅ | Histórico de ações de suporte e alterações. |
| **Início de Sessão de Impersonação** | `impersonation.start` | ❌ | ✅ | Requer ticket de chamado (`SUP-XXXX`) e justificativa. |
| **Encerramento de Impersonação** | `impersonation.stop` | ❌ | ✅ | Encerramento manual ou expiração por TTL (5-60 min). |
| **Remediação de Inquilino** | `tenants.write` | ❌ | ✅ | Requer feature flag `backoffice.feature.remediation_execution`. |
| **Desmascaramento Just-In-Time LGPD**| `compliance.unmask` | ❌ | ✅ | Requer feature flag `backoffice.feature.compliance_unmasking`. |

---

## 3. Procedimentos Operacionais: Suporte N1 (Triagem & Diagnóstico)

### 3.1. Identificação do Produtor e Diagnóstico 360
1. Acesse o console administrativo em `/backoffice/tenants`.
2. No campo de busca, filtre pelo CNPJ/CPF da fazenda, nome da propriedade ou e-mail do titular.
3. Inspecione o card de estado do tenant:
   - **`Active`**: Operação regular.
   - **`Trial`**: Período de avaliação ativo (verificar expiração).
   - **`PastDue`**: Assinatura em atraso (orientar contato com o financeiro).
   - **`Suspended` / `Cancelled` / `Archived`**: Acesso de produtores bloqueado. N1 não realiza reativação; direcionar para fluxo financeiro/ouvidoria.
4. Verifique a capacidade zootécnica contratada vs. cabeças de gado ativas (ex: limite de matrizes/confinamento do plano *Starter* ou *Pro*).

### 3.2. Diagnóstico de Falha de Sincronização Off-line PWA (Curral/Manejo)
Quando o produtor relatar que pesagens, partos ou vacinações realizadas no curral não aparecem no painel web:
1. Verifique na aba *Auditoria do Tenant* o último sync registrado.
2. Peça ao produtor para abrir a central off-line no app PWA do celular/tablet.
3. Se houver registros na fila do `IndexedDB` com erro de conflito ou schema:
   - Instrua o produtor a manter a conexão Wi-Fi/4G estável.
   - Colete o identificador do dispositivo e crie um ticket no Jira/Zendesk sob a taxonomia `SUP-OFFLINE-SYNC`.
   - Escale o chamado imediatamente para o **Suporte N2**.

---

## 4. Procedimentos Operacionais: Suporte N2 (Operação Avançada)

### 4.1. Sessão de Impersonação com Dupla Salvaguarda
A impersonação permite que o operador N2 acesse a interface do produtor para reproduzir chamados técnicos complexos.

#### Salvaguardas Mandatórias:
1. **Ticket de Suporte Válido**: Código alfanumérico do chamado (ex: `SUP-4821`).
2. **Justificativa Operacional**: Mínimo de 10 caracteres claros descrevendo o motivo.
3. **Restrições de Elegibilidade**:
   - Tenants com status `Suspended`, `Cancelled` ou `Archived` são **rejeitados automaticamente** pela API.
   - Tenants com flag de proteção institucional (`IsProtected = true`) **rejeitam qualquer tentativa de impersonação**.
4. **TTL Estrito**: Duração máxima configurada entre 5 e 60 minutos (padrão recomendado: 15 minutos).

#### Fluxo Passo a Passo:
```
[N2 abre chamado SUP-XXXX] 
       │
       ▼
[Acessa /backoffice/support] ➔ Clica em "Iniciar Suporte Assistido"
       │
       ▼
[Preenche Modal: ID do Tenant, Ticket SUP-XXXX, Justificativa e TTL em minutos]
       │
       ▼
[Backend valida Feature Flag 'backoffice.feature.impersonation']
       │
       ├─ Se Kill-Switch Ativo ➔ Erro 400 'FeatureFlagErrors.KillSwitchActive'
       │
       ▼
[Gera token de impersonação efêmero com claim 'act_as_tenant']
       │
       ▼
[Navegação assistida com banner visual persistente 'MODO SUPORTE ATIVO']
       │
       ▼
[Encerramento formal pelo botão 'Encerrar Sessão' ou expiração de TTL]
```

> **Aviso Crítico de Segurança**: O token de impersonação é impedido de acessar endpoints do Backoffice (`/api/v1/backoffice/*`). Qualquer tentativa disparará erro `403 Forbidden` (`Backoffice.ImpersonationRestricted`) e encerrará a sessão imediatamente.

### 4.2. Remediação de Inconsistências de Dados (`ExecuteTenantRemediationCommand`)
Utilizado para reparar desvios de saldo de lote, recalcular indicadores de GPD (Ganho de Peso Diário) ou ressincronizar filas pendentes:
1. Acesse `/backoffice/support` na seção **Ferramentas de Remediação**.
2. Selecione o tipo de remediação necessária:
   - `ResyncOfflineQueue`: Reprocessa mensagens retidas no pipeline off-line.
   - `RecalculateStockingRate`: Recalcula a Taxa de Lotação (UA/ha) dos piquetes.
   - `FixWeaningIndicators`: Corrige pesagens ajustadas de desmame a 205 dias.
3. Forneça o ticket de suporte e a justificativa técnica.
4. Clique em **Executar Remediação**.
5. O sistema grava um registro de auditoria forense criptográfica em `AuditLog` com severidade `High`.

### 4.3. Desmascaramento Just-In-Time LGPD (`compliance.unmask`)
Por padrão, todos os dados pessoais (CPF, CNPJ, telefone, e-mail pessoal) são mascarados na interface do Backoffice (`***.456.789-**`):
1. Caso seja estritamente necessário validar a titularidade fiscal de um produtor:
2. Clique no ícone de "Cadeado" ao lado do campo mascarado.
3. No modal de confirmação, informe:
   - Ticket de atendimento.
   - Justificativa legal/operacional formal (mínimo 10 caracteres).
4. Ao confirmar, o backend processa [RevealSensitiveDataCommand](file:///home/rony/LPR/CriaCerto/agents.md#L162), desmascara temporariamente o dado na tela e insere um registro imutável com severidade `High` na cadeia SHA-256 de auditoria.

---

## 5. Matriz de Escalação e Prazos (SLAs)

| Tipo de Chamado | Nível Inicial | Tempo de Resposta (SLA) | Escalação |
| :--- | :---: | :---: | :--- |
| Dúvidas de navegação e relatórios | N1 | 15 minutos | N/A |
| Bloqueio por atraso financeiro | N1 | 10 minutos | `FinanceOps` via fila interna |
| Erro de sincronização off-line do PWA | N1 | 30 minutos | `SupportN2` |
| Falha ao registrar nascimento ou IATF | N2 | 20 minutos | Engenharia de Software |
| Suspeita de vazamento de dados ou anomalia | N2 | Imediato (5 min) | `PlatformOwner` / SecOps via [RUNBOOK-05] |
