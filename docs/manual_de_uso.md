# Manual do Usuário — Plataforma CriaCerto
> **Guia Oficial de Operação e Uso do Sistema de Gestão Agropecuária**  
> *Versão do Sistema: 2026.1 | Plataforma Web & Mobile PWA Offline-First*

---

## Sumário
1. [Visão Geral da Plataforma](#1-visão-geral-da-plataforma)
2. [Primeiros Passos & Acesso](#2-primeiros-passos--acesso)
   - [2.1 Cadastro de Conta e Confirmação de E-mail](#21-cadastro-de-conta-e-confirmação-de-e-mail)
   - [2.2 Wizard de Configuração Inicial (Onboarding)](#22-wizard-de-configuração-inicial-onboarding)
   - [2.3 Login Seguro e Recuperação de Senha](#23-login-seguro-e-recuperação-de-senha)
3. [Interface e Navegação](#3-interface-e-navegação)
   - [3.1 Painel Lateral e Atalhos Rápidos](#31-painel-lateral-e-atalhos-rápidos)
   - [3.2 Dashboard Executivo & Indicadores Globais](#32-dashboard-executivo--indicadores-globais)
4. [Módulo de Reprodução & Gestão do Plantel (`Breeding`)](#4-módulo-de-reprodução--gestão-do-plantel-breeding)
   - [4.1 Cadastro e Ficha Cadastral do Animal](#41-cadastro-e-ficha-cadastral-do-animal)
   - [4.2 Consulta, Filtros e Linha do Tempo](#42-consulta-filtros-e-linha-do-tempo)
   - [4.3 Protocolos de IATF e Apontamentos em Lote](#43-protocolos-de-iatf-e-apontamentos-em-lote)
   - [4.4 Diagnóstico de Gestação (DG) e Alertas de DNP](#44-diagnóstico-de-gestação-dg-e-alertas-de-dnp)
5. [Módulo de Maternidade & Bezerreiro (`Calving`)](#5-módulo-de-maternidade--bezerreiro-calving)
   - [5.1 Registro de Partos e Vitalidade do Bezerro](#51-registro-de-partos-e-vitalidade-do-bezerro)
   - [5.2 Acompanhamento de Lactação e Desmama](#52-acompanhamento-de-lactação-e-desmama)
6. [Módulo de Recria, Engorda & Pastagens (`Growth`)](#6-módulo-de-recria-engorda--pastagens-growth)
   - [6.1 Gestão de Pastos, Piquetes e Taxa de Lotação (UA/ha)](#61-gestão-de-pastos-piquetes-e-taxa-de-lotação-uaha)
   - [6.2 Criação e Movimentação de Lotes](#62-criação-e-movimentação-de-lotes)
   - [6.3 Apontamento de Pesagens e Cálculo de GPD](#63-apontamento-de-pesagens-e-cálculo-de-gpd)
   - [6.4 Importação de Arquivos de Balanças Eletrônicas](#64-importação-de-arquivos-de-balanças-eletrônicas)
   - [6.5 Detecção de Anomalias de Pesagem](#65-detecção-de-anomalias-de-pesagem)
7. [Módulo Sanitário & Vacinas (`Sanitary`)](#7-módulo-sanitário--vacinas-sanitary)
   - [7.1 Campanhas Coletivas de Vacinação](#71-campanhas-coletivas-de-vacinação)
   - [7.2 Tratamentos Veterinários e Controle de Carência](#72-tratamentos-veterinários-e-controle-de-carência)
   - [7.3 Bloqueio de Abate e Despacho Seguro](#73-bloqueio-de-abate-e-despacho-seguro)
8. [Módulo de Nutrição & Confinamento (`Nutrition`)](#8-módulo-de-nutrição--confinamento-nutrition)
   - [8.1 Gestão de Silos e Controle de Estoque](#81-gestão-de-silos-e-controle-de-estoque)
   - [8.2 Trato TMR no Confinamento e Conversão Alimentar](#82-trato-tmr-no-confinamento-e-conversão-alimentar)
   - [8.3 Suplementação Mineral e Proteica a Pasto](#83-suplementação-mineral-e-proteica-a-pasto)
   - [8.4 Custo por Arroba (@) Produzida](#84-custo-por-arroba--produzida)
9. [Operação Offline no Campo e Sincronização (PWA)](#9-operação-offline-no-campo-e-sincronização-pwa)
   - [9.1 Instalação do Aplicativo (PWA)](#91-instalação-do-aplicativo-pwa)
   - [9.2 Manejo Sem Sinal de Internet no Curral](#92-manejo-sem-sinal-de-internet-no-curral)
   - [9.3 Resolução de Conflitos e Fila de Sincronização](#93-resolução-de-conflitos-e-fila-de-sincronização)
10. [Configurações, Equipe & Governança](#10-configurações-equipe--governança)
    - [10.1 Gestão da Fazenda e Múltiplas Unidades Produtivas](#101-gestão-da-fazenda-e-múltiplas-unidades-produtivas)
    - [10.2 Perfis de Usuário e Gestão de Equipe (RBAC)](#102-perfis-de-usuário-e-gestão-de-equipe-rbac)
    - [10.3 Planos, Assinatura e Faturamento (Stripe)](#103-planos-assinatura-e-faturamento-stripe)
    - [10.4 Privacidade, Dados e LGPD](#104-privacidade-dados-e-lgpd)
    - [10.5 Programa Indique e Ganhe](#105-programa-indique-e-ganhe)
11. [Perguntas Frequentes (FAQ) & Suporte](#11-perguntas-frequentes-faq--suporte)

---

## 1. Visão Geral da Plataforma

O **CriaCerto** é uma plataforma SaaS modular de alta performance projetada para revolucionar o controle zootécnico, operacional e financeiro da pecuária bovina e suinocultura.

### Principais Benefícios:
* **Foco no Manejo Real:** Otimizado para telas móveis e tablets de curral, com botões amplos, contrastes adaptados e fluxo rápido de inserção de dados.
* **Operação Offline-First:** Trabalhe no pasto, retiro ou mangueiro sem conexão à internet. Ao recuperar o sinal, todos os lançamentos são sincronizados automaticamente.
* **Segurança Sanitária Integrada:** Bloqueio preventivo automático para abate de animais em período de carência de medicamentos.
* **Indicadores em Tempo Real:** Cálculo instantâneo de Ganho de Peso Diário (GPD), Taxa de Lotação em Unidades Animais por Hectare (UA/ha), Conversão Alimentar (CA) e Custo da Arroba (@).

---

## 2. Primeiros Passos & Acesso

### 2.1 Cadastro de Conta e Confirmação de E-mail
1. Acesse a tela inicial e clique em **Criar Conta** (ou acerte a rota `/register`).
2. Preencha seus dados:
   * **Nome Completo**
   * **E-mail de Trabalho**
   * **Senha Forte** (mínimo de 8 caracteres, contendo letras maiúsculas, minúsculas, números e caracteres especiais).
3. Leia e aceite os Termos de Serviço e Política de Privacidade.
4. Clique em **Registrar-se**.
5. Um link de confirmação será enviado para a sua caixa de entrada. Clique no botão de confirmação para ativar a sua credencial.

### 2.2 Wizard de Configuração Inicial (Onboarding)
No primeiro acesso após a ativação:
1. Você será direcionado para o assistente de boas-vindas (`/onboarding`).
2. **Identificação da Fazenda:**
   * Nome da Propriedade (ex.: *Fazenda Vale Verde*).
   * Inscrição Estadual e CNPJ/CPF (opcional).
   * Estado (UF) e Município sede.
3. **Primeira Unidade de Produção / Retiro:**
   * Informe o nome da sede principal ou retiro (ex.: *Retiro das Palmeiras*).
   * Capacidade estimada de cabeças e área total em hectares.
4. **Finalização:** A plataforma cria automaticamente o seu ambiente isolado (*Tenant*) com você como **Administrador Geral**.

### 2.3 Login Seguro e Recuperação de Senha
* **Login (`/login`):** Insira seu e-mail e senha cadastrados. A sessão é protegida com tokens JWT criptografados.
* **Esqueci Minha Senha (`/forgot-password`):** Caso não recorde sua senha, informe seu e-mail para receber um link seguro de redefinição com validade temporária.

---

## 3. Interface e Navegação

### 3.1 Painel Lateral e Atalhos Rápidos
O menu lateral esquerdo (ou menu inferior no celular) organiza as rotas da ferramenta em dois blocos:
* **Operações Bovinas:** Dashboard, Plantel, IATF, Diagnóstico de Gestação, Partos, Pastos & Lotação, Pesagem & Curral, Sanitário, Silos & Estoque, Trato TMR e Suplementação.
* **Configurações:** Fazenda & Unidades, Assinatura & Planos, Indique e Ganhe, Meu Perfil e Privacidade LGPD.

### 3.2 Dashboard Executivo & Indicadores Globais
Acesse pelo menu **Dashboard** (`/analytics/executive-dashboard`). Reúne os principais KPIs da fazenda em tempo real:
* **Taxa de Prenhez (%):** Percentual de matrizes confirmadas prenhas sobre o total de fêmeas expostas no ciclo.
* **Taxa de Desmame (%):** Eficiência de desmama em relação ao total de nascimentos.
* **Lotação Média (UA/ha):** Carga animal ponderada por hectare em relação à capacidade forrageira.
* **GPD Médio do Rebanho:** Ganho médio de peso diário em kg/dia nos últimos 30 ou 60 dias.
* **Custo Médio da Arroba Produzida (R$/@):** Visão direta da viabilidade econômica das dietas e suplementações.
* **Alertas Sanitários:** Quantidade de animais atualmente sob período de carência medicamentosa.
* **Exportação de Relatórios:** Botão para download de consolidados em planilha estruturada (CSV/Excel).

---

## 4. Módulo de Reprodução & Gestão do Plantel (`Breeding`)

Centraliza toda a vida reprodutiva e genealógica dos bovinos.

### 4.1 Cadastro e Ficha Cadastral do Animal
Acesse **Plantel Bovino** (`/breeding/registry`) e clique em **Novo Animal**:
* **Identificação Principal:** Número do Brinco (`EarTag` - campo obrigatório e único por fazenda).
* **Identificadores Complementares:** Apelido, RFID/Botom eletrônico, Número SISBOV, Tatuagem e Registro Genealógico/PBB.
* **Classificação:** Sexo (Fêmea ou Macho) e Categoria:
  * Marcando *Matriz* (para fêmeas em idade fértil).
  * Marcando *Touro/Reprodutor* (para machos ativos).
* **Genética e Origem:** Raça predominante, Data de Nascimento, Origem (Nascido na Fazenda ou Comprado).
* **Filiação:** Brinco ou nome do Pai e da Mãe (árvore de pedigree).
* **Entrada no Plantel:** Data de entrada e Peso de Chegada (kg).
* **Escore de Condição Corporal (ECC/BCS):** Avaliação de 1 a 5 (Muito Magro, Magro, Ideal, Gordo, Muito Gordo) com seletor visual interativo.

### 4.2 Consulta, Filtros e Linha do Tempo
* **Filtros Rápidos por Status:** *Todas*, *Vazias*, *Inseminadas*, *Prenhes*, *Lactantes*, *Secas* ou *Descarte*.
* **Busca Dinâmica:** Pesquise imediatamente por número de brinco ou apelido.
* **Ficha do Animal (`/breeding/animal/{id}`):** Ao clicar em um animal, tenha a visão completa:
  * Idade calculada automaticamente em meses e anos.
  * Tempo de permanência no plantel.
  * Histórico de pesagens e curva evolutiva de ganho.
  * Linha do tempo reprodutiva (cobrições, inseminações, toques e partos).

### 4.3 Protocolos de IATF e Apontamentos em Lote
Acesse **Protocolos IATF** (`/breeding/iatf`):
1. **Criar Protocolo:** Dê um nome ao lote de IATF (ex.: *IATF Primíparas Lote 01*) e defina a data de início (D0).
2. **Vincular Matrizes:** Selecione as matrizes aptas do plantel que participarão do protocolo.
3. **Cronograma Automático:** O sistema gera o calendário visual de manejos:
   * **D0:** Inserção do dispositivo de progesterona + aplicação de benzoato de estradiol.
   * **D8:** Retirada do implante + aplicação de prostaglandina + eCG + cipionato.
   * **D10/D11:** Inseminação Artificial em Tempo Fixo propriamente dita com indicação do touro/sêmen utilizado.
4. **Apontamento Rápido em Lote (`/breeding/batch-breeding`):** Confirme a execução de dosagens hormonais e IA para dezenas de matrizes com um único clique.

### 4.4 Diagnóstico de Gestação (DG) e Alertas de DNP
Acesse **Diagnóstico de Gestação** (`/breeding/diagnostico`):
1. Consulte a lista de fêmeas com prazo para realização de toque ou ultrassonografia (geralmente entre 30 e 45 dias pós-IA).
2. No curral, lance o resultado do exame:
   * **Prenhe:** Informe os dias estimados de gestação e confirme o touro pai. O sistema calcula automaticamente a **Data Provável de Parto (DPP)**.
   * **Vazia:** A matriz retorna imediatamente ao status *Vazia* e entra na fila de reprogramação reprodutiva.
3. **Indicador DNP (Dias Não Prenhe):** Alerta visual em destaque para matrizes com elevado tempo em aberto, sinalizando necessidade de descarte zootécnico ou intervenção veterinária.

---

## 5. Módulo de Maternidade & Bezerreiro (`Calving`)

### 5.1 Registro de Partos e Vitalidade do Bezerro
Acesse **Partos & Bezerreiro** (`/calving/records`) para apontar o nascimento:
1. Clique em **Registrar Parto**.
2. Selecione a **Matriz Mãe** (o sistema preenche o histórico reprodutivo e o touro pai correspondente).
3. Informe os dados do nascimento:
   * **Data e Hora do Parto**
   * **Tipo de Parto:** Normal, Distócico (com auxílio) ou Cesariana.
   * **Sexo do Bezerro:** Macho ou Fêmea.
   * **Número do Brinco do Bezerro** (já cadastrado automaticamente no plantel).
   * **Peso ao Nascer (kg)**.
   * **Vigor Neonatal:** Classificação de vitalidade do recém-nascido (Fraco, Regular, Vigoroso).
   * **Ingestão de Colostro:** Sinalização de mamada nas primeiras 6 horas de vida.

### 5.2 Acompanhamento de Lactação e Desmama
* As matrizes com bezerro ao pé recebem automaticamente o status **Lactante**.
* **Apontamento de Desmame:** Quando o lote atinge a idade/peso de desmama (geralmente entre 7 e 8 meses):
  * Informe o peso aferido na desmama.
  * O sistema calcula automaticamente o **Ganho Médio Diário Pré-Desmame (kg/dia)**.
  * A mãe é automaticamente atualizada para o status **Seca** e liberada para o próximo protocolo de reprodução.

---

## 6. Módulo de Recria, Engorda & Pastagens (`Growth`)

### 6.1 Gestão de Pastos, Piquetes e Taxa de Lotação (UA/ha)
Acesse **Pastos & Lotação** (`/growth/pastures`):
* **Cadastro de Piquetes:** Cadastre cada pasto com nome, área total (hectares), variedade de capim (Brachiaria, Mombaça, Panicum, etc.) e condições do bebedouro/cocho.
* **Cálculo de Lotação em Tempo Real:** Conforme os lotes são movimentados, o sistema calcula a **Lotação Atual** dividindo o peso total dos animais (em Unidades Animais de 450 kg) pela área do piquete:
  $$\text{Taxa de Lotação (UA/ha)} = \frac{\text{Peso Total do Lote (kg)} \div 450}{\text{Área do Piquete (ha)}}$$
* **Alertas de Sobrepastejo:** O card do piquete muda de cor se a taxa de lotação ultrapassar a capacidade de suporte recomendada da forrageira.

### 6.2 Criação e Movimentação de Lotes
* **Criação de Lote:** Agrupe animais por categoria (ex.: *Garrotes Recria 2026*, *Novilhas Nelore*, *Lote Engorda Confinamento 02*).
* **Troca de Piquete:** Movimente um lote de um pasto para outro através de um modal simplificado, atualizando instantaneamente os saldos de área e dias de descanso do pasto.

### 6.3 Apontamento de Pesagens e Cálculo de GPD
Acesse **Pesagem & Arrobas** (`/growth/curral-weighing`):
* Tela desenvolvida sob medida para uso em currais com teclado numérico em destaque e digitação ágil.
* **Fluxo de Pesagem:**
  1. Digite o número do brinco do animal ou leia via leitor RFID Bluetooth.
  2. Digite o peso aferido na balança (kg).
  3. O sistema exibe imediatamente na tela:
     * Peso anterior e data da última aferição.
     * Intervalo de dias entre as pesagens.
     * **GPD (Ganho de Peso Diário):**
       $$\text{GPD} = \frac{\text{Peso Atual (kg)} - \text{Peso Anterior (kg)}}{\text{Dias Decorridos}}$$
     * Projeção em **Arrobas Líquidas (@)** considerando o rendimento de carcaça padrão.
  4. Pressione `Enter` ou clique em **Salvar e Próximo** para avançar instantaneamente.

### 6.4 Importação de Arquivos de Balanças Eletrônicas
Se você utiliza balanças eletrônicas automatizadas (Tru-Test, Coimma, etc.):
1. Exporte o arquivo de pesagens do software da balança (formato `.csv` ou `.txt`).
2. Na tela de pesagem, clique no botão **Importar da Balança**.
3. Selecione o arquivo exportado. O sistema processará todos os registros, vinculando cada pesagem ao brinco correspondente.

### 6.5 Detecção de Anomalias de Pesagem
Para evitar que erros de digitação corrompam os dados da fazenda:
* O sistema possui validação estatística inteligente. Se uma pesagem registrar ganho acima de 2,5 kg/dia ou perda abrupta de peso injustificada, um aviso visual alerta o operador para confirmação antes de gravar.

---

## 7. Módulo Sanitário & Vacinas (`Sanitary`)

Garante a rastreabilidade sanitária e a proteção legal da carne e do leite produzidos.

### 7.1 Campanhas Coletivas de Vacinação
Acesse **Sanitário & Vacinas** (`/sanitary/campaigns`):
1. **Nova Campanha:** Selecione o tipo de vacina (Febre Aftosa, Brucelose B19/RB51, Clostridioses, Raiva Bovina, etc.).
2. Defina o público-alvo (todos os animais, apenas fêmeas jovens, bezerros até 8 meses, etc.).
3. Registre a data de aplicação, veterinário responsável, número do lote e laboratório fabricante.
4. Lance a aplicação em lote.

### 7.2 Tratamentos Veterinários e Controle de Carência
No manejo individual de animais doentes:
* Registre o diagnóstico (ex.: *Pododermatite*, *Pneumonia*, *Tristeza Parasitária*).
* Informe o medicamento utilizado, dosagem aplicada e o **Período de Carência (em dias)** informado na bula do fármaco para corte e leite.

### 7.3 Bloqueio de Abate e Despacho Seguro
> [!IMPORTANT]
> **Segurança Sanitária Garantida:** O CriaCerto monitora ativamente as datas de carência de todos os animais medicados.

* Se qualquer operador tentar despachar ou emitir guia de abate para um animal cujo período de carência ainda esteja em vigor, a plataforma **bloqueia o despacho** e exibe um alerta visual informando:
  * Medicamento causador da carência;
  * Data exata em que o animal estará liberado para abate.

---

## 8. Módulo de Nutrição & Confinamento (`Nutrition`)

### 8.1 Gestão de Silos e Controle de Estoque
Acesse **Silos & Estoque** (`/nutrition/silos`):
* **Cadastro de Silos e Galpões:** Cadastre depósitos (ex.: *Silo Trincheira 01*, *Galpão de Farelo de Soja*, *Tanque de Melaço*).
* **Entrada de Insumos (`Restock`):** Aponte carregamentos com data, quantidade recebida (toneladas ou kg), nota fiscal e custo por tonelada.
* **Saldo Atual e Dias de Autonomia:** Acompanhe graficamente o nível dos silos e a previsão de término com base no consumo diário dos lotes.

### 8.2 Trato TMR no Confinamento e Conversão Alimentar
Acesse **Trato TMR Confinamento** (`/nutrition/trough-log`):
* **Receitas e Formulações:** Cadastre as formulações de ração total misturada (TMR) com proporção de volumoso (silagem, feno) e concentrado (milho, farelos, núcleo mineral).
* **Apontamento de Trato:** Registre as batidas de vagão diárias entregues no cocho por lote ou curral.
* **Leitura de Cocho:** Aponte a nota de sobras (cocho limpo, cocho raspado, sobras normais) para ajuste dinâmico do fornecimento.
* **Conversão Alimentar (CA):** Cálculo automático da eficiência biológica do lote:
  $$\text{Conversão Alimentar} = \frac{\text{Kg de Matéria Seca Consumida}}{\text{Kg de Peso Vivo Ganho}}$$

### 8.3 Suplementação Mineral e Proteica a Pasto
Acesse **Suplementação de Pasto** (`/nutrition/pasture-supplementation`):
* Registre o abastecimento de cochos de sal mineral, sal proteinado ou ração de creep-feeding nos pastos.
* Controle o consumo médio por cabeça/dia em gramas.

### 8.4 Custo por Arroba (@) Produzida
* Cruzando os dados de ganho de peso das pesagens com o custo dos insumos consumidos nos silos e cochos, a plataforma apresenta em tempo real o **Custo Operacional Efetivo por Arroba Engordada (R$/@)**, permitindo identificar precocemente lotes deficitários.

---

## 9. Operação Offline no Campo e Sincronização (PWA)

O CriaCerto foi desenvolvido com arquitetura **Offline-First**, utilizando Progressive Web App (PWA) e armazenamento local criptografado (IndexedDB).

### 9.1 Instalação do Aplicativo (PWA)
Você não precisa baixar o app pela loja comercial:
1. Abra o navegador do seu celular ou tablet (Google Chrome ou Safari) e acesse o endereço da sua fazenda no CriaCerto.
2. No menu do navegador, toque em **Instalar Aplicativo** ou **Adicionar à Tela de Início**.
3. O ícone do **CriaCerto** ficará acessível na tela inicial do aparelho, funcionando como um app nativo em tela cheia.

### 9.2 Manejo Sem Sinal de Internet no Curral
* Ao sair para o mangueiro ou pasto sem internet, o aplicativo exibe o chip **Offline**.
* **Todas as operações permanecem 100% disponíveis:**
  * Lançamento de pesagens rápidas;
  * Registro de inseminações e toques de gestação;
  * Apontamento de nascimentos e partos;
  * Fornecimento de tratos nos cochos.
* Os dados são salvos com segurança no armazenamento local do seu dispositivo móvel.

### 9.3 Resolução de Conflitos e Fila de Sincronização
* Assim que o celular ou tablet conectar a uma rede Wi-Fi ou sinal 4G/5G, a fila de eventos entra em processo de sincronização em segundo plano.
* **Resolução Inteligente de Conflitos:** Se dois vaqueiros lançaram eventos diferentes para o mesmo animal simultaneamente, a plataforma aplica regras zootécnicas baseadas no carimbo de tempo (*timestamp*). Caso persista alguma inconsistência, uma tela de resolução de conflitos (`ConflictResolutionModal`) permite ao administrador escolher a versão definitiva com total transparência.

---

## 10. Configurações, Equipe & Governança

### 10.1 Gestão da Fazenda e Múltiplas Unidades Produtivas
Acesse **Fazenda & Unidades** (`/settings/organization`):
* Atualize os dados cadastrais da propriedade rural.
* **Cadastro de Retiros/Unidades:** Cadastre filiais, retiros e fazendas arrendadas, gerenciando todos os núcleos produtivos sob uma mesma conta consolidada.

### 10.2 Perfis de Usuário e Gestão de Equipe (RBAC)
Convide seus funcionários e parceiros técnicos atribuindo papéis com permissões restritas:
* **Administrador / Proprietário:** Acesso irrestrito a configurações, dados financeiros, relatórios e equipe.
* **Gerente / Veterinário:** Acesso total aos manejos reprodutivos, sanitários, prescrições, lotes e pesagens.
* **Operador de Curral / Campeiro:** Acesso focado no lançamento rápido de pesagens, manejos diários e alimentação, sem visualização de dados financeiros e custos da fazenda.
* **Consultor / Auditor:** Permissão somente-leitura para acompanhamento de índices e auditoria zootécnica.

### 10.3 Planos, Assinatura e Faturamento (Stripe)
Acesse **Assinatura & Planos** (`/settings/subscription`):
* Conheça os planos disponíveis (Starter, Profissional, Enterprise).
* Upgrade ou downgrade direto via **Stripe Checkout** com pagamento seguro por cartão de crédito ou boleto bancário.
* Gerenciamento de faturas, notas fiscais e alteração de cartão via **Portal do Cliente Stripe**.

### 10.4 Privacidade, Dados e LGPD
Acesse **Privacidade LGPD** (`/settings/privacy`):
* O CriaCerto opera em estrita conformidade com a Lei Geral de Proteção de Dados (Lei nº 13.709/2018).
* **Portabilidade de Dados:** Baixe um arquivo compactado com todos os seus dados pessoais e registros zootécnicos.
* **Canal do Encarregado (DPO):** Formulário direto para envio de solicitações de retificação, consulta ou revogação de consentimento ao nosso DPO.

### 10.5 Programa Indique e Ganhe
Acesse **Indique e Ganhe** (`/settings/referrals`):
* Compartilhe seu link exclusivo de indicação com produtores vizinhos e cooperativas.
* Acompanhe os cadastros ativados e receba descontos cumulativos ou mensalidades gratuitas na sua assinatura conforme as indicações ativarem planos pagos.

---

## 11. Perguntas Frequentes (FAQ) & Suporte

**1. O sistema funciona se a internet cair durante a pesagem no curral?**  
Sim! A plataforma foi concebida especificamente para operar em áreas rurais remotas. Continue pesando e registrando normalmente; quando você retornar à sede com Wi-Fi, o app enviará tudo para a nuvem automaticamente.

**2. Posso usar leitor de brinco eletrônico (RFID)?**  
Sim. O campo de identificação do animal aceita leitura de bastões ou leitores de brinco eletrônico conectados via Bluetooth ou teclado virtual.

**3. O que acontece se o período de teste (trial) expirar?**  
Ao término dos 14 dias de teste gratuito, o sistema direcionará para a tela de regularização de assinatura (`/trial-expired`). Todos os seus dados, cadastros e históricos permanecem intactos e protegidos; basta selecionar um plano para restabelecer o acesso imediato.

**4. Como entrar em contato com o suporte técnico?**  
* **E-mail:** `suporte@criacerto.agr.br`
* **WhatsApp de Suporte:** Disponível no menu de ajuda dentro da plataforma para planos Profissional e Enterprise.
* **Horário de Atendimento:** Segunda a Sexta das 07h às 18h e Sábados das 07h às 12h (horário de Brasília).

---
*CriaCerto — Gestão Zootécnica de Alta Performance.*  
*Copyright © 2026 CriaCerto. Todos os direitos reservados.*
