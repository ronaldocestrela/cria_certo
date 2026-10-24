# Paleta de Cores e Guia de Estilo: CriaCerto

Este documento documenta o **Design System e a Paleta de Cores** da plataforma **CriaCerto**, baseada nas especificações do **Stitch Design System (Material Design 3)** adaptado para agrotecnologia e gestão pecuária.

Os estilos centrais estão implementados como variáveis CSS em [`app.css`](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web/wwwroot/app.css) e consumidos pelos componentes Blazor em [`CriaCerto.Web.Client`](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client).

---

## 1. Cores Institucionais e da Marca

Cores principais que identificam a marca CriaCerto e orientam os principais fluxos de ação (CTAs, botões principais e destaques da interface):

| Nome | Hex | RGB | Token CSS | Uso Recomendado |
| :--- | :---: | :---: | :--- | :--- |
| **Primary (Verde Floresta)** | `#00652c` | `rgb(0, 101, 44)` | `--cc-primary` | Cor primária da marca, botões principais, links ativos e ícones em destaque. |
| **Primary Container (Verde Pasto)** | `#15803d` | `rgb(21, 128, 61)` | `--cc-primary-container` | Acentos secundários, estados de hover, cartões com destaque positivo. |
| **Primary Fixed (Verde Claro)** | `#95f8a7` | `rgb(149, 248, 167)` | `primary-fixed` | Fundos de ênfase alta em tema escuro ou contraste com texto escuro (`#00210a`). |
| **Secondary (Ardósia / Slate)** | `#545f73` | `rgb(84, 95, 115)` | `--cc-secondary` | Ações secundárias, botões neutros, ícones complementares. |
| **Secondary Container (Azul Céu Suave)** | `#d8e3fb` | `rgb(216, 227, 251)` | `secondary-container` | Fundos de seleção, cartões informativos e filtros selecionados. |
| **Tertiary (Terra / Cobre)** | `#9f3000` | `rgb(159, 48, 0)` | `tertiary` | Elementos de atenção moderada, gráficos e acentos terrosos. |
| **Tertiary Container (Telha Claro)** | `#ffdbd0` | `rgb(255, 219, 208)` | `tertiary-fixed` | Badges de alerta moderado e cards de transição. |

---

## 2. Superfícies, Estrutura e Fundos (Canvas & Surface)

Projetados para alto conforto visual em campo e em telas de escritório sob luz solar direta ou ambientes fechados:

| Nome | Hex | Token CSS | Aplicação |
| :--- | :---: | :--- | :--- |
| **Canvas / Background** | `#f7f9fb` | `--cc-canvas` / `--cc-surface` | Fundo geral da aplicação (gelo suave para reduzir fadiga visual). |
| **Surface Panels (Branco Puro)** | `#ffffff` | `surface-container-lowest` | Superfície de cartões (*Cards*), modais, menus suspensos e tabelas. |
| **Surface High / Separator** | `#e0e3e5` | `--cc-surface-muted` | Fundos de linhas alternadas, cabeçalhos secundários e divisores. |
| **Sidebar Top** | `#f7f9fb` | `--cc-sidebar-top` | Início do degradê da barra de navegação lateral. |
| **Sidebar Bottom** | `#eceef0` | `--cc-sidebar-bottom` | Final do degradê da barra de navegação lateral. |
| **Border (Sage / Borda Musgo)** | `#becabc` | `--cc-border` / `--cc-outline-variant` | Bordas sutis de tabelas, inputs e separadores de cards. |
| **Outline Médio** | `#6f7a6e` | `--cc-outline` | Linhas de campos em foco e contornos de ênfase. |

---

## 3. Tipografia e Cores de Texto (Ink)

| Nome | Hex | Token CSS | Uso |
| :--- | :---: | :--- | :--- |
| **Ink (Preto Carvão)** | `#191c1e` | `--cc-ink` / `on-surface` | Texto primário, títulos, rótulos de campos e valores numéricos em destaque. |
| **Ink Muted (Musgo Escuro)** | `#3f493f` | `--cc-ink-muted` / `on-surface-variant` | Textos de apoio, legendas, cabeçalhos de tabelas e datas. |
| **Inverse Ink (Branco)** | `#ffffff` | `on-primary` | Texto sobre botões e elementos com fundo primário ou de erro. |

### Fontes Associadas
* **`Work Sans`** (`--cc-font-headline`): Fonte geométrica moderna utilizada para toda a interface (títulos, parágrafos, botões e formulários).
* **`JetBrains Mono`** (`--cc-font-mono`): Tipografia monoespaçada técnica para identificação de animais (Brinco/`EarTag`, SISBOV, RFID), pesos, valores de balança e códigos de lote.

---

## 4. Semântica Zootécnica & Status de Matrizes (Chips & Badges)

Implementado no componente [`StatusChip.razor`](file:///home/rony/LPR/CriaCerto/src/Web/CriaCerto.Web/CriaCerto.Web.Client/Components/Plantel/StatusChip.razor) para leitura visual imediata no manejo:

| Status Zootécnico | Fundo (Hex) | Texto (Hex) | Classe CSS | Significado no Manejo |
| :--- | :---: | :---: | :--- | :--- |
| **Vazia** | `#ffdad6` | `#93000a` | `.chip-empty` | Fêmea em ciclo reprodutivo sem confirmação de prenhez. Requer atenção para nova inseminação/cobertura. |
| **Coberta / Inseminada** | `#d8e3fb` | `#111c2d` | `.chip-bred` | Animal submetido a protocolo de IATF ou monta, aguardando prazo de toque/ultrassom (DG). |
| **Gestante / Prenhe** | `#d3ffd5` | `#005323` | `.chip-pregnant` | Prenhez confirmada positivamente. |
| **Lactante** | `#ffe0c2` | `#832600` | `.chip-lactating` | Matriz em fase de amamentação com bezerro ao pé. |
| **Atenção Crítica / DNP** | `#ba1a1a` | `#ffffff` | `.chip-alert` | Alerta urgente (ex.: DNP - Dias Não Prenhe estourados, carência sanitária ativa). |
| **Quarentena** | `#ffdbd0` | `#832600` | `.chip-warning` | Animal isolado para observação clínica ou quarentena de chegada. |
| **Descarte** | `#e0e3e5` | `#545f73` | `.chip-muted` | Fêmea ou macho destinado ao descarte zootécnico ou refugo. |
| **Ativo** | `#eff7ef` | `#00652c` | `.chip-active` | Animal ativo sem alertas pendentes. |

---

## 5. Estados do Sistema & Alertas (Feedback)

| Estado | Cor de Fundo | Cor do Texto / Borda | Token / Variável |
| :--- | :---: | :---: | :--- |
| **Erro / Crítico** | `#ffdad6` | `#ba1a1a` | `--cc-error` / `--cc-error-container` |
| **Sucesso** | `#d3ffd5` | `#00652c` | `--cc-primary` / `chip-pregnant` |
| **Aviso (Warning)** | `#fff8e1` | `#8a6227` | `.badge-wait` / `.kpi-trend.warning` |
| **Informativo** | `#e9eefb` | `#395796` | `.badge-ready` |

---

## 6. Declaração das Variáveis CSS (`:root`)

```css
:root {
    /* Superfícies e Estrutura */
    --cc-canvas: #f7f9fb;
    --cc-sidebar-top: #f7f9fb;
    --cc-sidebar-bottom: #eceef0;
    --cc-surface: #f7f9fb;
    --cc-surface-muted: #e0e3e5;

    /* Textos e Contraste */
    --cc-ink: #191c1e;
    --cc-ink-muted: #3f493f;

    /* Bordas e Contornos */
    --cc-border: #becabc;
    --cc-outline: #6f7a6e;
    --cc-outline-variant: #becabc;

    /* Cores da Marca */
    --cc-primary: #00652c;
    --cc-primary-container: #15803d;
    --cc-secondary: #545f73;

    /* Alertas e Erros */
    --cc-error: #ba1a1a;
    --cc-error-container: #ffdad6;

    /* Tipografia */
    --cc-font-headline: 'Work Sans', sans-serif;
    --cc-font-mono: 'JetBrains Mono', monospace;

    /* Efeitos */
    --cc-shadow: 0px 4px 20px rgba(0, 0, 0, 0.08);
}
```
