# ADR 0027: Assinatura Self-Service Modular (À La Carte) e Precificação Escalonada por Faixas de Cabeças

## Status
Aceito e Implementado (Accepted & Implemented)

## Contexto
O modelo comercial original do CriaCerto baseava-se em pacotes monolíticos fixos (*Starter*, *Pro* e *Enterprise*). Essa estrutura apresentava severas limitações para a heterogeneidade da pecuária de corte e leite:
1. **Incompatibilidade Produtiva:** Fazendas focadas em recria e engorda a pasto não utilizam protocolos reprodutivos de IATF (módulo *Breeding*), enquanto cabanhas de genética não necessitam de confinamento ou trato TMR intensivo (*Nutrition*). Planos fixos forçavam o produtor a pagar por módulos não utilizados.
2. **Descolamento da Carga Animal:** A capacidade de cabeças era atrelada rigidamente ao pacote, impedindo que pequenas propriedades acessassem ferramentas de inteligência zootécnica e que grandes confinamentos contratassem apenas sanidade e pesagens.
3. **Atrito de Expansão (Upsell Friction):** O upgrade exigia migração completa de pacote e recadastramento financeiro, gerando atrito no momento em que o operador necessitava de uma nova funcionalidade no curral.

O sistema necessitava de uma reformulação comercial e arquitetural para permitir a contratação **À La Carte** por módulo e por **Faixa de Capacidade de Cabeças**, preservando rigorosamente os princípios do Monólito Modular (.NET 10), o Result Pattern e a integridade de dados.

## Decisão
Implementar a arquitetura de **Assinatura Self-Service Modular & Precificação por Faixas de Cabeças**, estruturada nos seguintes pilares:

### 1. Catálogo Modular Versionado no Backoffice (`Modules.Backoffice`)
- **`HeadTierCatalog` (`backoffice.HeadTierCatalogs`):** Tabela de faixas zootécnicas (`TIER_500` até 500 cabeças, `TIER_1500` até 1.500, `TIER_3000` até 3.000 e `TIER_5000_PLUS` até 10.000+ cabeças).
- **`ModuleCatalog` (`backoffice.ModuleCatalogs`):** Catálogo dos 7 módulos comerciais (`Cows`, `Breeding`, `Calving`, `Growth`, `Sanitary`, `Nutrition`, `Analytics`).
- **`ModuleTierPrice` (`backoffice.ModuleTierPrices`):** Matriz imutável de 28 preços (7 módulos $\times$ 4 faixas) com suporte a ciclos Mensal e Anual, aprovação dupla (4-Eyes) e feature flag `backoffice.feature.plan_publishing`.
- **Inviolabilidade do Módulo Base:** O módulo de Plantel Base (`Cows`) possui `IsCore = true` travado por domínio, sendo obrigatório em todas as assinaturas e blindado contra desativação (`PlanErrors.CannotDeactivateCoreModule`).

### 2. Governança de Capacidade Desacoplada (`ITenantActiveHerdCounter`)
- Para validar a capacidade de cabeças por faixa sem efetuar acoplamento de banco de dados entre `Modules.Tenancy` e os módulos de manejo (`Modules.Breeding`, `Modules.Growth`), definiu-se a abstração `ITenantActiveHerdCounter`.
- O Host da API registra o `CompositeActiveHerdCounter`, consultando as contagens de animais em tempo de execução via barramento em memória.
- Requisições que excedam o teto contratado falham compulsoriamente com `Result.Failure(License.HeadCapacityExceeded)`.

### 3. Faturamento Stripe Multi-Item com Rateio Proporcional (*Proration*)
- Substituição do modelo de plano único no Stripe pela criação e manutenção de múltiplos `SubscriptionItems` na mesma assinatura (`IStripePaymentService.CreateModularCheckoutSessionAsync` e `UpdateModularSubscriptionAsync`).
- Modificações de módulos e faixas são processadas com `ProrationBehavior = "create_prorations"`, garantindo crédito ou cobrança proporcional imediata sem duplicidade de mensalidade.
- Webhooks do Stripe (`checkout.session.completed`, `customer.subscription.updated`, `invoice.paid`) utilizam estratégia de resolução híbrida: priorizam metadados canônicos da sessão (`head_tier`, `max_heads`, `subscribed_modules`) com fallback resiliente para mapeamento reverso dos `PriceIds` cadastrados no catálogo modular.

### 4. Tolerância de Downgrade (*14-day Grace Period*)
- Ao solicitar redução de faixa para um teto menor do que o rebanho ativo cadastrado, o sistema suporta a transição para `SubscriptionActionType.DowngradeGracePeriodStarted` com status `GracePeriodActive` por 14 dias (`GracePeriodEndUtc = UtcNow.AddDays(14)`).
- Permite que a fazenda ajuste o rebanho ativo (descarte, vendas ou transferências) sem bloqueio emergencial abrupto de suas operações no campo.

### 5. Emissão Dinâmica de JWT Claims e Gating Declarativo
- O `JwtService` emite claims dedicadas: `head_tier`, `head_capacity`, claims individuais `subscribed_module` e a claim condensada CSV `subscribed_modules`.
- O pipeline MediatR (`ModuleAccessBehavior`) e o `ModuleLicenseChecker` interceptam comandos decorados com `[RequiresModule("...")]`, garantindo que apenas módulos ativos no token tenham acesso liberado, retornando `Result.Failure(License.ModuleNotSubscribed)` em caso de recusa.

### 6. Upsell Contextual de 1-Clique na Interface (`ModuleGuard.razor`)
- O componente `ModuleGuard.razor` integra o `ModuleGuardUpsellCalculator`: ao tentar acessar uma tela bloqueada, calcula instantaneamente o acréscimo mensal correspondente à faixa ativa da fazenda.
- Administradores com cartão Stripe ativo podem confirmar a inclusão do módulo com 1 clique: o sistema executa o `UpdateModularSubscriptionCommand`, renova o JWT em memória via `AuthenticationStateProvider` e libera o recurso sem recarregar a página.

### 7. Desacoplamento do Programa de Indicação e Migração Canônica
- As políticas de comissão e desconto do Programa de Indicação (`PlanReferralDiscountPolicy`) foram desacopladas dos planos legados, vinculando-se à `HeadTierCode` ou diretriz global `GLOBAL_DEFAULT` sobre o total da fatura modular (`MonthlyTotalAmount`), com teto estrito de 100%.
- A migração de clientes legados foi formalizada através do `LegacyPlanMigrationResolver` e executada via migration EF Core e script SQL idempotente `20261105_migrate_existing_tenants_to_modular.sql`.

## Consequências

### Positivas
- **Alinhamento Pecuário e Conversão:** Cada produtor monta o pacote exato para o seu perfil zootécnico (cria, recria, engorda, confinamento ou ciclo completo).
- **Redução de Churn:** Elimina o sentimento de pagamento por módulos ociosos e reduz barreiras de entrada para pequenas fazendas através da faixa até 500 cabeças.
- **Aceleração de Upsell:** O desbloqueio de 1-clique diretamente no curral converte necessidades imediatas de manejo em expansão de receita com fricção nula.
- **Isolamento Modular Rígido:** A arquitetura mantém independência completa entre os módulos, usando abstrações in-memory (`ITenantActiveHerdCounter`) e desacoplamento de persistência.
- **Conformidade com Result Pattern:** Eliminação de exceções para controle de licenciamento, retornando respostas padronizadas e previsíveis.

### Mitigações e Desafios
- **Complexidade de Sincronização Stripe:** A gestão de múltiplos `SubscriptionItems` requer que a suíte de testes de integração simule adições, remoções e webhooks concorrentes com precisão (coberta integralmente por 12 testes ponta a ponta em `StripeWebhookIntegrationTests`).
- **Resolução de Preços em Webhooks:** Mitigada com a abordagem em duas camadas (metadados canônicos na assinatura + mapeamento reverso por catálogo de preços publicados).

