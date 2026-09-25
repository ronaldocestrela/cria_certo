# Matriz de Capacitação, Treinamento e Hand-off Operacional: CriaCerto Backoffice

## 1. Visão Geral
Este documento define o framework de prontidão de equipe, transferência operacional (*Hand-off*) e capacitação contínua para os operadores do **Backoffice Administrativo do CriaCerto**, assegurando que os times compreendam a arquitetura de segurança, as ferramentas de diagnóstico zootécnico e os protocolos de emergência.

---

## 2. Matriz RACI de Operações Administrativas

* **R (Responsible)**: Quem executa a ação.
* **A (Accountable)**: Quem responde pelo sucesso e conformidade da ação.
* **C (Consulted)**: Quem deve ser consultado antes ou durante a ação.
* **I (Informed)**: Quem deve ser informado após a conclusão.

| Atividade Operacional | SupportN1 | SupportN2 | FinanceOps | ReadOnlyAuditor | PlatformOwner |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Triagem de Inquilino / Produtor** | **R/A** | C | I | I | I |
| **Abertura de Sessão de Impersonação** | I | **R** | I | I | **A** |
| **Execução de Remediação de Inquilino** | I | **R** | I | I | **A** |
| **Desmascaramento Just-In-Time LGPD** | I | **R** | I | **C** | **A** |
| **Criação / Rascunho de Planos** | I | I | **R/A** | I | C |
| **Submissão de Publicação (4-Eyes)** | I | I | **R** | I | **A** |
| **Aprovação de Publicação de Planos** | I | I | C | I | **R/A** |
| **Suspensão por Inadimplência** | I | I | **R/A** | I | I |
| **Configuração de Rollout e Anéis** | I | I | I | I | **R/A** |
| **Acionamento de Kill-Switch** | I | C | I | I | **R/A** |
| **Tratamento de Alertas de Anomalia** | I | C | I | C | **R/A** |
| **Emissão de Dossiês de Auditoria LGPD**| I | I | I | **R** | **A** |

---

## 3. Trilhas de Treinamento por Perfil

### Trilha 1: Fundamentos de Suporte e Diagnóstico (Perfis: SupportN1, SupportN2)
* **Carga Horária**: 4 horas.
* **Módulos**:
  1. *Arquitetura do Produtor Rural*: Ciclo de cria, recria, engorda, balança e indicadores zootécnicos (IEP, GPD, UA/ha).
  2. *Navegação no Console*: Filtros de tenant por CNPJ/Inscrição Estadual, estados de ciclo de vida (`Trial`, `Active`, `PastDue`, `Suspended`).
  3. *Mapeamento de Descompasso Off-line PWA*: Como ler o status do `IndexedDB` e orientar o operador no curral.
  4. *Avaliação Prática*: Identificar três fazendas com discrepância de animais e diagnosticar causa raiz via logs.

### Trilha 2: Suporte Avançado, Salvaguardas e Remediação (Perfil: SupportN2)
* **Carga Horária**: 6 horas.
* **Módulos**:
  1. *Impersonação Segura*: Preenchimento do modal, exigência de ticket `SUP-XXXX`, restrições de tempo (TTL) e proibição de acesso a endpoints administrativos.
  2. *Ferramentas de Remediação*: Execução de sincronização forçada e recálculo de índices produtivos.
  3. *Privacidade e LGPD*: Regras de desmascaramento temporário de dados pessoais com justificativa auditada.
  4. *Avaliação Prática*: Simular atendimento assistido em tenant de teste e executar remediação sem gerar exceções.

### Trilha 3: Gestão de Planos, 4-Eyes e Financeiro (Perfil: FinanceOps)
* **Carga Horária**: 4 horas.
* **Módulos**:
  1. *Versionamento do Catálogo*: Imutabilidade de versões ativas e parametrização de limites por porte de fazenda.
  2. *Princípio 4-Eyes*: Regras de separação de funções, bloqueio de auto-aprovação e expiração por TTL de 48h.
  3. *Governança de Cobrança*: Régua de inadimplência, salvaguarda de tenants protegidos e suspensão justificada.
  4. *Avaliação Prática*: Cadastrar uma nova versão do plano `Pro v2.1`, submeter para aprovação e simular suspensão de tenant em atraso.

### Trilha 4: Governança de Plataforma, SecOps e Rollout (Perfil: PlatformOwner)
* **Carga Horária**: 8 horas.
* **Módulos**:
  1. *Rollout por Anéis*: Gestão de anéis (`Ring0_Canary`, `Ring1_EarlyAdopters`, `Ring2_GA`) e determinismo SHA-256 percentual.
  2. *Kill-Switch de Emergência*: Reconhecimento de anomalias de SLO e corte em < 1s.
  3. *Observabilidade & Anomalias*: Trilha de telemetria, histogramas de latência e resolução de alertas.
  4. *Forense & Cadeia SHA-256*: Verificação de integridade da tabela `AuditLog` e detecção de tampering.
  5. *Avaliação Prática*: Simulação prática de emergência (Tabletop Drill).

---

## 4. Checklist de Hand-off Operacional (Go-Live Readiness)

Para formalizar o hand-off de desenvolvimento para operação de produção, todos os itens abaixo devem estar 100% validados:

- [x] Contas administrativas provisionadas com MFA TOTP obrigatório para perfis com permissões sensíveis.
- [x] Seed de usuário bootstrap de emergência protegido e senha segregada no cofre de segredos da infraestrutura.
- [x] Playbook de Suporte (`playbook_support.md`) lido e aprovado pelo líder de atendimento ao cliente.
- [x] Playbook Financeiro (`playbook_finance.md`) revisado e aprovado pela gerência financeira e comercial.
- [x] Runbooks de Emergência (`runbook_incident_response.md`) validados em exercício de simulação de mesa (*Tabletop*).
- [x] Canal de incidentes (#incident-p1) configurado com integração de alertas automáticos.
- [x] Todos os 5 recursos críticos com flags de Kill-Switch testadas e operacionais no console `/backoffice/rollout`.
- [x] Matriz de autorização negativa e testes de segurança aprovados em CI/CD.

---

## 5. Termo de Compromisso e Concessão de Privilégios

Todo operador designado para papéis administrativos no CriaCerto assina digitalmente o termo com as seguintes cláusulas:
1. **Confidencialidade Estrita**: Proibição de compartilhamento de credenciais ou chaves MFA com terceiros.
2. **Justificativa Fidedigna**: Obrigação de fornecer justificativas reais, objetivas e rastreáveis para toda e qualquer ação de impersonação, suspensão de tenant, aprovação dupla ou desmascaramento LGPD.
3. **Ciência de Auditoria Forense**: Conhecimento inequívoco de que 100% das requisições e ações são registradas em trilha criptograficamente encadeada (SHA-256), sem possibilidade de exclusão ou alteração retroativa.
