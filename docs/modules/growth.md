# Module Specification: Growth, Pasture Management & Stocking Rate (`Modules.Growth`)

## 1. Overview
The `Modules.Growth` module manages animal lots, pasture paddocks (piquetes), animal movements between paddocks, zootecnic stocking rate indicators (Taxa de Lotação em Unidades Animais por Hectare - UA/ha), and curral weighings with ADG/GPD (Ganho de Peso Diário) and Arroba (@) conversions.

---

## 2. Domain Models & Rules

### 2.1 Pasture Paddock (`PasturePaddock`)
* **Identification:** `Name`, `Code` (Identificador do Piquete).
* **Metrics:** `AreaHectares` (Área em Hectares), `MaxCapacityUA` (Capacidade Máxima Suportável em UA).
* **Status:** `Active` (Em pastejo), `Resting` (Pousio/Descanso), `Maintenance` (Reforma de pastagem).

### 2.2 Animal Lot (`Lot`)
* **Category:** `Bezerros`, `Recria`, `Garrotes`, `Engorda`, `Matrizes`, `Reprodutores`.
* **Zootecnic Load (UA):**
  * Standard Unit: $1\text{ UA} = 450\text{ kg}$ of live weight.
  * $$\text{Total Weight (kg)} = \text{HeadCount} \times \text{AverageWeightKg}$$
  * $$\text{Total UA} = \frac{\text{Total Weight (kg)}}{450.0}$$
* **Status:** `Active`, `Closed`.

### 2.3 Stocking Rate & Overgrazing Alert
* **Stocking Rate Equation:**
  $$\text{Taxa de Lotação (UA/ha)} = \frac{\sum \text{UA dos lotes alocados no piquete}}{\text{Área do Piquete (ha)}}$$
* **Overgrazing Warning:** Triggered when total allocated UA exceeds `MaxCapacityUA` or when lots are placed in a paddock with status `Resting` or `Maintenance`.

### 2.4 Curral Weighing & Zootecnic Growth Tracking (`Weighing`)
* **Entity:** `Weighing` (`AnimalTagId`, `WeightKg`, `CarcassYieldPercentage`, `WeighingDate`).
* **Arrobas (@) Calculation:**
  $$\text{Arrobas Total (@)} = \frac{\text{Peso Vivo (kg)} \times (\text{Rendimento Carcaça \%} / 100)}{15.0\text{ kg/@}}$$
  *(Standard default Rendimento de Carcaça = 50.0\%, customizable between 40.0\% and 65.0\%).*
* **Average Daily Gain (ADG / GPD kg/dia):**
  $$\text{GPD (kg/dia)} = \frac{\text{Peso Atual (kg)} - \text{Peso Anterior (kg)}}{\text{Dias Decorridos}}$$
* **Monthly Arroba Gain (GPD em @/mês):**
  $$\text{GPD (@/mês)} = \frac{\text{GPD (kg/dia)} \times 30.0\text{ dias} \times (\text{Rendimento Carcaça \%} / 100)}{15.0\text{ kg/@}}$$
* **Weight Loss Warning (`IsWeightLossWarning`):** Triggered automatically when current weight is less than the previous recorded weight ($\text{GPD} < 0$).
* **Consecutive Weight Loss Anomaly (`WeightLossAnomalyService`):** Flags critical anomaly when an animal records 2 consecutive weighings with negative GPD ($\text{GPD} < 0$).

### 2.5 Scale File Import & Formats (`IWeighingScaleFileParser`)
Supported Electronic Scale Export File Formats:
* **Tru-Test:** CSV/TXT with headers (`VID`/`Tag`, `Weight`, `Date`).
* **Coimma:** TXT/CSV with semicolon delimiters (`Brinco`, `Peso`, `Data`).
* **Toledo:** CSV with headers (`TAG`, `PESO`, `DATA_PESAGEM`).
* **Generic CSV / Auto-Detect:** Flexible CSV header mapping (`Brinco`, `Peso`, `Data`, `Rendimento`).

---

## 3. API Endpoints
* `GET /api/growth/paddocks?tenantId={id}` - List paddocks with current stocking rate metrics.
* `POST /api/growth/paddocks` - Register a new pasture paddock.
* `GET /api/growth/lots?tenantId={id}` - List animal lots.
* `POST /api/growth/lots` - Create a new animal lot.
* `POST /api/growth/lots/move` - Move a lot to a destination paddock (records history).
* `POST /api/growth/lots/{id}/close` - Close an active lot.
* `POST /api/growth/weighings` - Record individual curral weighing.
* `POST /api/growth/weighings/batch` - Record batch weighings for curral session.
* `POST /api/growth/weighings/import` - Multipart upload of Tru-Test, Coimma, Toledo, or CSV scale export files.
* `GET /api/growth/weighings/anomalies` - List animals with consecutive weight loss anomaly alerts.
* `GET /api/growth/weighings/history/{animalTagId}` - Get animal weighing history & GPD progression.
* `GET /api/growth/weighings/lot-summary/{lotId}` - Get summary metrics for a lot's latest weighings.
* `GET /api/growth/weighings/recent` - Get recent weighings list.

---

## 4. Field Offline Capability & Curral Weighing UI
* **Curral Weighing Fast Input (`/growth/curral-weighing`):** Permite entrada ágil de pesagem individual na balança. O operador seleciona o animal diretamente de um dropdown populado a partir do plantel de bovinos cadastrados (`Breeding / Plantel`), visualizando brinco, raça, categoria e apelido.
* **Offline & Contingência:** Caso a conexão esteja indisponível ou ocorra entrada de animal não cadastrado previamente, a interface oferece alternância instantânea para modo de digitação livre de brinco/RFID.
* **Cache & Sincronização:** Movimentações de lotes em pastagem e pesagens de curral realizadas em locais remotos são armazenadas em cache local no `IndexedDB` do cliente PWA quando não há conectividade com a rede, sendo sincronizadas em segundo plano assim que a conexão é restabelecida.

