# Decisão de estilo

| | |
|---|---|
| **Data** | 2026-11-07 |
| **Escolha do usuário** | **C. Console de operação** |
| **Base de layout** | Bloco `dashboard-01` do shadcn/ui (`npx shadcn@latest add dashboard-01`), indicado pelo usuário: sidebar à esquerda, cards de indicadores, gráfico de área com seleção de período e tabela de dados |
| **Canvas** | <https://claude.ai/artifact/ATChUi3B73tsmuW2zNjcUu> |
| **Referências** | [`design-references.md`](design-references.md) |

## Critérios

**Limiares:**
- Contraste de texto de 4,5:1.
- Alvos de pelo menos 44 px.
- Construível com shadcn/ui.

As três direções passaram nesses limiares no render em 1280×800, medido em 2026-11-07.

**Trade-offs:**
- Legibilidade na luz da guarita.
- O que cabe sem rolar a tela.
- Toque com luvas.
- Custo de manter dois temas.
- Adequação ao dispositivo.

## Por que C

O usuário definiu a navegação do app: uma sidebar com Dashboard e histórico, Controle de acesso, Clientes, Fluxo de caixa e Empresa. Com isso, o produto passa a ser majoritariamente administrativo e de desktop. A C tem a densidade padrão, as bordas e as tabelas que essas telas pedem, e é a mais próxima do visual padrão do `dashboard-01`.

## O que a C perde

- **Na cancela:** botões de 44 px, que é o mínimo, contra 56 px na A e 64 px na B. É a direção mais fraca para toque com luvas.
- **Com pouca luz:** não tem tema escuro.
- **Em telas largas:** no render, metade da área ficou vazia no desktop. Isso deixa de valer com dashboards e tabelas preenchendo a tela.

## Direções rejeitadas

| Direção | Perdeu em | O que a traria de volta |
|---|---|---|
| A. Balcão claro | Coluna única: em 1280×800, a ação principal ficou abaixo da dobra. Densidade baixa para dashboards e listas. | Uso principal em tablet, sem telas administrativas. |
| B. Guarita noturna | App inteiro escuro para telas administrativas, ou o custo de manter dois temas. | Confirmação de que a guarita opera à noite ou com pouca luz. Nesse caso, o tema escuro entra como modo da tela de operação, mantendo os tokens da C. |
