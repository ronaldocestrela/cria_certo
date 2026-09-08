# Módulo Sanitário & Período de Carência (`Modules.Sanitary`)

## 1. Visão Geral
O módulo `Modules.Sanitary` gerencia o calendário oficial de vacinações (Febre Aftosa, Brucelose, Raiva, Clostridioses), controle veterinário de aplicação de medicamentos/vermífugos e calcula com precisão matemática a **data final de carência sanitária**.

---

## 2. Bloqueio Rígido de Abate / Carência Sanitária
Para garantir conformidade com as normas do MAPA (Ministério da Agricultura e Pecuária) e órgãos de defesa sanitária animal:
- Sempre que um animal ou lote recebe uma medicação ou vacina com `WithdrawalDays > 0`, o sistema registra a data de liberação `WithdrawalEndDateUtc = ApplicationDateUtc.AddDays(WithdrawalDays)`.
- Se for feita uma consulta de elegibilidade para abate (`ValidateSlaughterEligibilityQuery`), e a data atual for anterior à data de liberação de carência, o backend retorna `Result.Failure(SanitaryErrors.ActiveSlaughterWithdrawalPeriod)`.
- No Frontend Blazor, a interface renderiza a badge `<SlaughterWithdrawalBadge>` em vermelho, indicando que o abate do animal/lote está totalmente bloqueado.

---

## 3. Entidades Principais
- **`VaccinationCampaign`**: Registra campanhas oficiais com período de vigência (`StartDateUtc` a `EndDateUtc`), tipo da campanha e status de atividade.
- **`TreatmentRecord`**: Registra a aplicação individual ou em lote de produto comercial, dosagem, número do lote, veterinário responsável e dias de carência.
- **`WithdrawalPeriodService`**: Serviço de domínio responsável por avaliar a inelegibilidade de abate.

---

## 4. API Endpoints
- `GET /api/sanitary/campaigns` - Lista campanhas sanitárias ativas.
- `POST /api/sanitary/campaigns` - Cria nova campanha oficial de vacinação.
- `POST /api/sanitary/treatments` - Registra aplicação de medicamento/vacina com dias de carência.
- `GET /api/sanitary/treatments` - Lista o histórico de tratamentos registrados com status de carência calculada.
- `GET /api/sanitary/slaughter-validation/{animalId}` - Valida se o animal está liberado para abate ou se possui carência sanitária ativa.

---

## 5. Interface Web & Cliente HTTP (`CriaCerto.Web.Client`)
- **`SanitaryApiClient`**: Cliente HTTP autenticado (via Bearer token) que encapsula as operações do módulo sanitário para consumo nos componentes Blazor.
- **Página `/sanitary/campaigns` (`SanitaryCampaigns.razor`)**:
  - Renderização interativa Blazor WebAssembly (`InteractiveWebAssemblyRenderMode(prerender: false)`).
  - **Métricas Sanitárias**: Exibição em tempo real de campanhas ativas, animais em período de carência (bloqueados para abate) e status de imunização/conformidade.
  - **Modal Nova Campanha Oficial**: Cadastro de campanhas sanitárias obrigatórias com período de vigência, tipo (`CampaignType`) e validações de data.
  - **Modal Aplicar Tratamento / Carência**: Registro de aplicação de medicamentos com suporte a escopo de **Animal Individual** (integrado com `PlantelApiClient`) ou **Lote de Animais** (integrado com `GrowthApiClient`), dosagem, lote do produto, veterinário responsável e dias de carência sanitária.
  - **Histórico & Indicadores Visuais**: Badges em destaque (`BLOQUEADO PARA ABATE` vs `Liberado para Abate`), identificação do animal ou lote tratado e botão de atualização (Refresh) com feedback de carregamento.
  - **Notificações**: Feedback imediato de operações via `IToastService`.

---

## 6. Garantia TDD & Testes
- Testes unitários do domínio e queries sanitárias em `tests/Unit/CriaCerto.Modules.Sanitary.UnitTests`:
  - `SanitaryDomainTests`: Regras de criação de campanhas, tratamentos e cálculo de elegibilidade para abate.
  - `SanitaryQueryTests`: Handlers de consulta (`GetTreatmentsQueryHandler` e `GetActiveCampaignsQueryHandler`).
- Testes unitários do cliente HTTP em `tests/Unit/CriaCerto.Web.Client.UnitTests`:
  - `SanitaryApiClientTests`: Validação de chamadas HTTP, rotas, payloads e respostas do `SanitaryApiClient`.
