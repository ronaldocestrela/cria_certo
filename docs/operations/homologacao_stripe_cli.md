# Procedimento Operacional: Homologação e Testes de Integração com Stripe CLI

## 1. Visão Geral e Propósito
Este documento descreve o procedimento operacional padrão (SOP) para execução de **testes de integração ponta a ponta e homologação de webhooks** do Stripe no SaaS CriaCerto, cobrindo:
1. Configuração do ambiente local com o **Stripe CLI**.
2. Encaminhamento de eventos em tempo real para o endpoint `POST /api/v1/payments/stripe-webhook`.
3. Disparo dos 4 eventos centrais do ciclo de vida de assinaturas.
4. Matriz de verificação da atomicidade transacional e integridade relacional no banco de dados (`TenancyDbContext`).

---

## 2. Pré-requisitos de Ambiente

* **Stripe CLI**: Versão `>= 1.14` instalada (verifique com `stripe version`).
* **Conta de Sandbox Stripe**: Autenticada via comando:
  ```bash
  stripe login
  ```
* **Backend CriaCerto**: Executando localmente na porta padrão (HTTP `5000` ou HTTPS `5001`):
  ```bash
  dotnet run --project src/Host/CriaCerto.Api
  ```
* **Utilitário de Automação**: Script executável em [`scripts/stripe-test-webhooks.sh`](file:///home/rony/LPR/CriaCerto/scripts/stripe-test-webhooks.sh).

---

## 3. Passo a Passo de Execução

### Passo 1: Inicializar o Listener e Obter o Segredo de Assinatura
Em um terminal dedicado, execute:
```bash
./scripts/stripe-test-webhooks.sh listen
```
*Ou diretamente via Stripe CLI:*
```bash
stripe listen --forward-to http://localhost:5000/api/v1/payments/stripe-webhook
```

O terminal exibirá uma saída similar a:
```text
> Ready! Your webhook signing secret is whsec_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx (^C to quit)
```

> [!IMPORTANT]
> Copie o valor retornado (`whsec_...`) e configure a variável de ambiente:
> - No arquivo `.env`: `STRIPE_WEBHOOK_SECRET=whsec_xxxxxxxx`
> - Ou em `src/Host/CriaCerto.Api/appsettings.Development.json`:
>   ```json
>   "Stripe": {
>     "WebhookSecret": "whsec_xxxxxxxx"
>   }
>   ```
> Se o segredo não coincidir, o `StripePaymentService` rejeitará os eventos com erro `Stripe.InvalidSignature` (HTTP 400).

---

### Passo 2: Disparo dos Eventos do Ciclo de Cobrança

Em um segundo terminal, utilize o script automatizado para simular o ciclo completo:
```bash
./scripts/stripe-test-webhooks.sh trigger-all
```

Ou execute individualmente conforme o cenário desejado:

#### 1. Ativação pós-pagamento (`checkout.session.completed`)
```bash
stripe trigger checkout.session.completed
```
* **Efeito no CriaCerto**:
  - Quando `PaymentStatus == "paid"`: o Tenant passa de `Trial` para `Active`.
  - Módulos comerciais do plano (`Starter`, `Pro`, `Enterprise`) são desbloqueados.
  - A capacidade de cabeças de gado (`Capacity`) é redimensionada conforme o plano contratado.
  - Gravação compulsória em `TenantSubscriptionHistories` com `SubscriptionActionType.NewSubscription`.

#### 2. Renovação de ciclo recorrente (`invoice.paid`)
```bash
stripe trigger invoice.paid
```
* **Efeito no CriaCerto**:
  - O Tenant permanece `Active`.
  - A propriedade `CurrentPeriodEndUtc` é estendida com o timestamp de fechamento do novo período.
  - Gravação compulsória em `TenantSubscriptionHistories` com `SubscriptionActionType.Renewal`.

#### 3. Falha no débito / Inadimplência (`invoice.payment_failed`)
```bash
stripe trigger invoice.payment_failed
```
* **Efeito no CriaCerto**:
  - O Tenant é marcado imediatamente como `PastDue`.
  - `StatusReason` documenta a recusa do gateway.
  - Gravação em `TenantSubscriptionHistories` com `SubscriptionActionType.PaymentFailed`.
  - Permite período de carência (*grace period*) antes do corte de rotas operacionais.

#### 4. Cancelamento voluntário ou forçado (`customer.subscription.deleted`)
```bash
stripe trigger customer.subscription.deleted
```
* **Efeito no CriaCerto**:
  - Tenant comum: passa para o status `Cancelled` e o agendamento `CancelAtPeriodEnd` é desativado.
  - Tenant protegido (`tenant.IsProtected == true`): a transição é **interceptada**, mantendo `Status = "Active"` e registrando a tentativa em log de auditoria e no histórico.

---

## 4. Matriz de Verificação Transacional no Banco de Dados

Após o disparo de cada evento, execute a validação nas tabelas do `TenancyDbContext`:

| Tabela | Coluna / Campo | Valor Esperado |
| :--- | :--- | :--- |
| **`Tenants`** | `Status` | `Active` (checkout/renovação), `PastDue` (falha) ou `Cancelled` (cancelamento) |
| **`Tenants`** | `SubscribedPlan` | Plano canônico normalizado (`Starter`, `Pro`, `Enterprise`) |
| **`Tenants`** | `Capacity` | Limite de cabeças correspondente ao plano (ex: 500, 2500, 10000) |
| **`Tenants`** | `CurrentPeriodEndUtc` | Data futura informada pelo Stripe no objeto `lines.period.end` |
| **`StripeWebhookEvents`** | `EventId` | ID único do evento (`evt_...`) gravado para garantia de **idempotência** |
| **`TenantSubscriptionHistories`** | `ActionType` | `NewSubscription`, `Renewal`, `PaymentFailed`, `Cancelled` ou `PlanChanged` |
| **`TenantSubscriptionHistories`** | `ActorId` | `Guid.Empty` (identificador reservado do sistema/Stripe webhook) |

---

## 5. Garantias Arquiteturais e Salvaguardas

1. **Idempotência Estrita:**
   O `StripePaymentService` consulta previamente a tabela `StripeWebhookEvents`. Se o Stripe reenviar o mesmo `EventId` por timeout ou retentativa da rede, a API retorna confirmação `200 OK` instantaneamente sem gerar registros duplicados no histórico.
2. **Defesa em Profundidade contra IDs Nulos:**
   Eventos que cheguem sem `CustomerId` ou `SubscriptionId` válidos são descartados defensivamente sem disparar queries no banco, prevenindo mutações acidentais em fazendas sem assinatura.
3. **Resiliência de Tenants Protegidos (`IsProtected`):**
   Contas de homologação institucional ou fazendas estratégicas com `IsProtected = true` nunca sofrem cancelamento ou suspensão automática por webhooks.

---

## 6. Diagnóstico e Resolução de Problemas (Troubleshooting)

* **HTTP 400 - `Stripe.InvalidSignature`:**
  - *Causa:* O `STRIPE_WEBHOOK_SECRET` configurado no CriaCerto difere do segredo emitido pela sessão atual do `stripe listen`.
  - *Solução:* Copie o `whsec_...` exibido no topo do terminal do `stripe listen` e reinicie a API com a nova chave.
* **HTTP 400 - `Cabeçalho Stripe-Signature ausente ou inválido`:**
  - *Causa:* Requisições manuais via Postman ou curl sem o header `Stripe-Signature`.
  - *Solução:* Utilize o Stripe CLI (`stripe trigger`) ou a suíte automatizada em C# que calcula o HMAC-SHA256 (`StripeWebhookIntegrationTests`).
* **Tenant não encontrado:**
  - *Causa:* O evento gerado pela CLI contém um `customer` sintético do Stripe (ex: `cus_test...`) que não está associado a nenhuma fazenda no banco local.
  - *Solução:* Utilize os testes de integração automatizados em [StripeWebhookIntegrationTests.cs](file:///home/rony/LPR/CriaCerto/tests/Integration/CriaCerto.Architecture.IntegrationTests/StripeWebhookIntegrationTests.cs) ou injete o `TenantId` no metadata do evento.
