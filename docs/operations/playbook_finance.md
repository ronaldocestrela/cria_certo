# Playbook Operacional de Faturamento, Planos e Operações Financeiras: CriaCerto Backoffice

## 1. Visão Geral e Propósito
Este playbook estabelece os procedimentos operacionais para a equipe de **`FinanceOps` (Operações Financeiras)** do SaaS CriaCerto, cobrindo:
1. Gestão e versionamento do catálogo de planos (`Starter`, `Pro`, `Enterprise`).
2. Regras de feature gating e limites de capacidade de rebanho.
3. Fluxo de publicação sob o **Princípio dos Quatro Olhos (4-Eyes Workflow)**.
4. Régua de cobrança, gestão de inadimplência (`PastDue`) e suspensão controlada de inquilinos.

---

## 2. Matriz de Permissões Financeiras

| Operação | Permissão Requerida | Papel FinanceOps | Requer Aprovação Dupla (4-Eyes)? |
| :--- | :--- | :---: | :---: |
| **Leitura do Catálogo de Planos** | `plans.read` | ✅ | Não |
| **Criação / Rascunho de Planos** | `plans.write` | ✅ | Não |
| **Publicação de Versão de Plano** | `plans.publish` | ✅ | **SIM** (Aprovação por `PlatformOwner`) |
| **Consulta de Assinaturas** | `subscriptions.read` | ✅ | Não |
| **Upgrade / Downgrade de Assinatura** | `subscriptions.manage`| ✅ | Não (para planos já publicados) |
| **Suspensão por Inadimplência** | `tenants.suspend` | ✅ | Não (Suspensão em Massa exige 4-Eyes) |

---

## 3. Gestão e Versionamento do Catálogo de Planos

### 3.1. Princípios de Imutabilidade
* **Sem impacto retroativo**: Modificações em valores ou limites de planos nunca alteram contratos vigentes. Uma nova versão do plano (ex: `v2.0`) é criada como rascunho enquanto a versão anterior (`v1.0`) permanece ativa para os produtores contratantes.
* **Granularidade zootécnica de limites**:
  * *Starter*: Manejo básico de cria, limites de até 500 cabeças ativas, sem módulo de confinamento/IATF avançada.
  * *Pro*: Manejo completo de cria e recria, até 3.000 cabeças, índices reprodutivos (IEP, GPD, Taxa de Desmame).
  * *Enterprise*: Cabeças ilimitadas, confinamento com formulação de trato/TMR, predição de arroba (@) e acesso multi-fazendas/unidades.

### 3.2. Fluxo de Publicação com Aprovação Dupla (4-Eyes Workflow)

```
[FinanceOps cria/edita versão em rascunho]
                  │
                  ▼
[FinanceOps clica em "Submeter para Publicação"]
                  │
                  ▼
[Sistema gera 'AdminApprovalRequest' com Payload e Visual Diff]
                  │
                  ├─ Solicitante bloqueado de aprovar (CannotSelfApprove)
                  ├─ TTL de 48 horas para aprovação
                  │
                  ▼
[Notificação enviada ao 'PlatformOwner']
                  │
                  ▼
[PlatformOwner revisa o diff em /backoffice/approvals e Aprova]
                  │
                  ▼
[Backend executa 'PublishPlanVersionCommand' e grava AuditLog SHA-256]
```

#### Passo a Passo no Console:
1. Acesse `/backoffice/plans`.
2. Selecione o plano desejado e clique em **Nova Versão**.
3. Configure os parâmetros comerciais (Preço Mensal, Preço Anual, Desconto, Capacidade Máxima de Cabeças e Módulos Ativos).
4. Clique em **Submeter para Publicação**:
   - Forneça uma justificativa com o objetivo de negócio (ex: *"Reajuste anual de IPCA e inclusão do módulo de simulação de carcaça no Pro"* - mínimo 10 caracteres).
5. O status da versão mudará para `PendingApproval`.
6. Um operador com perfil `PlatformOwner` (diferente do solicitante) deve acessar a central `/backoffice/approvals`, revisar o diff visual e aprovar.

---

## 4. Gestão de Inadimplência e Ciclo de Vida do Tenant

### 4.1. Régua de Cobrança e Estados do Produtor
1. **Dia do Vencimento (D+0)**: Cobrança falhou na adquirente/gateway. O status da assinatura passa a `PastDue`. O produtor mantém acesso completo, recebendo alerta suave no dashboard.
2. **D+5 a D+15**: Disparo de régua de e-mails/WhatsApp de cobrança pela equipe de atendimento e financeiro.
3. **D+30 (Suspensão Administrativa)**:
   - Se a pendência não for regularizada em 30 dias corridos, o tenant deve ser colocado em estado `Suspended`.

### 4.2. Procedimento de Suspensão de Tenant
1. Acesse `/backoffice/tenants` e localize o produtor.
2. Certifique-se de que a flag **Tenant Protegido (`IsProtected`)** está desmarcada.
   > **Atenção**: Inquilinos marcados como protegidos (fazendas de validação institucional, parceiros universitários ou contas de teste) **não podem ser suspensos**. Tentativas retornarão `Result.Failure(TenantErrors.ProtectedTenant)`.
3. Clique em **Ações** ➔ **Suspender Inquilino**.
4. No formulário de confirmação, preencha obrigatoriamente a justificativa financeira (ex: *"Inadimplência referente às faturas 2026-08 e 2026-09 sem resposta após 3 tentativas"*).
5. Ao confirmar, o comando `SuspendTenantAdminCommand` é despachado:
   - O acesso dos peões, veterinários e produtores da fazenda ao sistema web e app é imediatamente bloqueado.
   - O evento é registrado em `AuditLog` com hash encadeado SHA-256 e severidade `High`.

### 4.3. Reativação de Acesso
Após confirmação da liquidação do boleto ou transação no gateway:
1. Acesse o tenant em `/backoffice/tenants`.
2. Clique em **Reativar Acesso**.
3. Informe a justificativa com o comprovante de pagamento.
4. O status retorna a `Active` imediatamente.
