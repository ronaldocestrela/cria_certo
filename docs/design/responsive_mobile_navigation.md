# Especificação de UI/UX: Menu de Navegação Responsivo com Rolagem Lateral Mobile

## 1. Visão Geral
Em dispositivos móveis (smartphones e tablets com viewport `<= 880px`), a barra lateral desktop tradicional (`.shell-sidebar`) é ocultada para maximizar a área útil da tela em operações de campo (currais, piquetes e silos). A navegação é assumida pelo componente dedicado `MobileNavMenu.razor` posicionado no rodapé da viewport.

## 2. Padrão Arquitetural (agents.md §6.3)
- **Single Source of Truth (`NavigationCatalog`)**: Todos os itens de menu compartilham um catálogo centralizado (`NavigationItem.cs`), eliminando redundância e prevenindo drift de rotas entre desktop e mobile.
- **Isolamento de Componentes & Posicionamento Flutuante Global**: O componente `MobileNavMenu.razor` é posicionado como filho direto no nível de layout (`MainLayout.razor`), completamente desacoplado do container CSS Grid `.shell-page`. Isso garante que sua posição seja sempre ancorada à viewport (`position: fixed !important; bottom: 0 !important; z-index: 1050 !important; isolation: isolate;`), flutuando permanentemente sobre o conteúdo em qualquer tela (inclusive telas extensas como Curral & Pesagem).

## 3. Comportamento de Rolagem Lateral (Overflow Horizontal)
Para permitir o acesso rápido a todos os módulos zootécnicos e de gestão (Dashboard, Plantel, IATF, Gestação, Partos, Pastos, Pesagem, Silos, Trato TMR, Suplementação e Configurações):
- **Container Rolável**:
  ```css
  display: flex;
  flex-direction: row;
  flex-wrap: nowrap;
  overflow-x: auto;
  overflow-y: hidden;
  -webkit-overflow-scrolling: touch;
  scroll-behavior: smooth;
  overscroll-behavior-x: contain;
  ```
- **Itens sem Quebra de Linha**: Cada link possui `flex: 0 0 auto; white-space: nowrap;` garantindo pílulas uniformes que nunca quebram para uma segunda linha.
- **Scrollbar Oculta**: `scrollbar-width: none; &::-webkit-scrollbar { display: none; }` para evitar poluição visual.
- **Máscara Gradiente de Borda**:
  ```css
  mask-image: linear-gradient(to right, transparent, black 12px, black calc(100% - 12px), transparent);
  -webkit-mask-image: linear-gradient(to right, transparent, black 12px, black calc(100% - 12px), transparent);
  ```
  Fornece uma pista visual intuitiva aos usuários de que existem itens adicionais à direita ou à esquerda.

## 4. Preservação do Estilo Visual
- **Efeito Vidro Fosco (*Frosted Glass*)**:
  - Fundo: `rgba(249, 251, 247, 0.92);`
  - Filtro: `backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px);`
  - Borda Superior: `1px solid var(--cc-border);`
- **Pílulas de Navegação**:
  - Borda e raio: `border-radius: 0.9rem; border: 1px solid transparent;`
  - Cor do texto inativo: `var(--cc-ink-muted);`
  - Estado ativo: `background: var(--cc-primary-soft); border-color: var(--cc-primary-soft-border); color: var(--cc-primary);`
- **Safe Area Inset**:
  - `padding-bottom: calc(0.6rem + env(safe-area-inset-bottom, 0px));` para respeitar barras de gestos e entalhes do iOS/Android.
  - `shell-main` possui `padding-bottom: calc(5rem + env(safe-area-inset-bottom, 0px));` para garantir que nenhum conteúdo seja encoberto.

## 5. Padrão de Ícones do Sistema (Material Symbols Outlined)
Em total alinhamento com a identidade visual do Cria Certo:
- **Fonte Oficial**: Google Material Symbols Outlined (`<span class="material-symbols-outlined">...</span>`).
- **Navegação Desktop (`NavMenu.razor`)**: Ícones adicionados ao lado de cada item com dimensão `1.25rem`, tonalidade primária (`var(--cc-primary)`) e efeito de microinteração (escala suave em hover/active).
- **Navegação Mobile (`MobileNavMenu.razor`)**: Exibição exclusiva dos ícones em botões de toque circulares/arredondados (`2.75rem x 2.75rem`), otimizados para toque no campo (`gap: 0.5rem`), com atributos de acessibilidade `title` e `aria-label` para identificação assistiva e tooltip.
