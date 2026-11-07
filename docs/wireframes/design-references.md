# Design: brief e referências

| | |
|---|---|
| **Status** | Estágios 1 e 2 da skill `neoparking-design-process`. Aguarda revisão do usuário antes do estágio 3 (direções de estilo). |
| **Data** | 2026-11-07 |
| **Método** | Busca na web e leitura do texto das páginas (WebFetch). **Nenhuma página foi vista como imagem**: o Claude in Chrome não estava conectado e o usuário pediu para seguir só com a web. Por isso, nada aqui descreve cor, tipografia ou layout visual de terceiros. |

## 1. Brief (abreviado)

O brief foi abreviado a pedido do usuário. Os itens marcados como **desconhecido** não foram assumidos.

- **Problema:** a API do NeoParking já cobre entrada, saída por código ou placa, clientes, operadores, tarifas e auditoria de documentos (Ondas 6 e 8), mas não existe nenhuma tela. O operador precisa registrar entrada e saída rápido e sem erro, e o Owner precisa administrar e auditar.
- **Usuários:**
  - Operador da cancela: uso contínuo, em pé ou com pressa, com o motorista esperando.
  - Admin: cadastros de clientes, operadores e tarifas.
  - Owner: o mesmo que o Admin, mais a auditoria de acesso a documentos.
- **Contexto de uso:**
  - Guarita ou balcão. Dispositivo: **desconhecido** (desktop, tablet ou celular).
  - Iluminação: **desconhecida**. Ambiente externo, com sol ou à noite, é plausível, mas não confirmado.
- **Restrições:**
  - React com shadcn/ui (skill `neoparking-wireframes`).
  - Contraste de texto de 4,5:1 e alvos de pelo menos 44 px (limiares do estágio 4).
  - Documento sempre mascarado, e placa e documento fora da URL (TD-802).
  - Papéis D2 (Owner, Admin, Operator).
- **Critérios de sucesso:**
  - Saída por placa ou código em poucos passos, sem tirar a mão do teclado.
  - Erros com motivo claro.
  - Nenhum dado sensível exposto além do que a API já mascara.
  - Metas numéricas: **desconhecidas**, sem dado de uso real.
- **Desconhecidos que afetam o estilo:** dispositivo da guarita, uso com luvas ou toque, iluminação, idioma (só pt-BR?) e identidade visual existente (marca, cores).

## 2. Referências

As 8 referências abaixo foram abertas nesta sessão. Todas foram vistas **só pelo texto da página**, e nenhuma por screenshot.

### Concorrentes diretos

**R1. Parking BOXX: CloudEASE.** <https://parkingboxx.com/parking-management-software/>
- **Visto:** texto da página, uma lista de recursos.
- **O que diz:**
  - "Live terminal views: Screen images from each lane, updated in real time."
  - "Instant alerts for paper low, cash-box full, gate fault, or revenue anomaly."
  - Abertura remota da cancela, registrada com o operador e o horário.
  - Placa não reconhecida segue o fluxo normal de ticket.
- **Princípio:** a unidade da tela é a **pista ou cancela**, com estado ao vivo. Alertas são exceções acionáveis, e não um feed. Toda ação manual deixa rastro de quem fez.
- **Aplica porque:** já temos auditoria de documentos (TD-805). O mesmo padrão de "quem fez e quando" vale para ações manuais na saída.
- **Evitar:** painel de receita misturado com a tela da cancela. O operador não precisa de faturamento ali.

**R2. T2 Systems: Private Operators.** <https://www.t2systems.com/private-operators/>
- **Visto:** texto da página, que é marketing com fotos de banco de imagens.
- **O que diz:**
  - "Manage multiple entry/exit lanes... from a single pane of glass."
  - "Automated overstay alerts."
  - "Clean audit trails and automated reconciliation."
- **Princípio:** uma visão única para a operação, com alertas de permanência e conciliação como fim de turno.
- **Aplica porque:** as metas do produto incluem divergência de contagem por turno (`product-evolution.md`, seção 1).
- **Evitar:** heat maps e yield. Não temos dados para isso, e C1 (ocupação) ainda não existe.

**R3. Jump Park (Brasil).** <https://www.b2bstack.com.br/product/jump-park>
- **Visto:** texto da página de catálogo, em português.
- **O que diz:**
  - App do operador "desenvolvido para smartphone ou tablet (Android)", que "não depende de internet".
  - Na saída, o operador lê o QR do cartão e "já recebe todos os dados de cobrança".
  - Tem impressora bluetooth.
- **Princípio:** a saída é **uma leitura e uma confirmação**: o código traz tudo o que o operador precisa para decidir.
- **Aplica porque:** C2 já entrega saída por código ou placa. O concorrente local confirma que o operador brasileiro usa celular ou tablet, o que pesa na pergunta do dispositivo.
- **Evitar:** assumir que funciona offline. Nossa API não suporta isso.

**R4. ParkPow: Parking Management Dashboard.** <https://parkpow.com/blog/parking-management-dashboard/>
- **Visto:** texto de um post de blog.
- **O que diz:**
  - Filtros por "authorized drivers, residents, vehicle types, entrances and exits".
  - "see all available data on that vehicle with one click".
  - Alerta por e-mail "with a photo and timestamp".
- **Princípio:** a placa é a chave de navegação. Da busca por placa, um clique leva ao histórico do veículo.
- **Aplica porque:** `POST /clients/search` por placa exata (TD-802) e a busca de ticket aberto por placa (TD-803).
- **Evitar:** foto da placa e alertas por e-mail. Não temos câmera nem LPR (C5 está fora do escopo atual).

### Ferramentas operacionais vizinhas

**R5. Square Point of Sale: notas de versão 7.8 (21 de maio de 2026).** <https://squareup.com/gb/en/release-notes/release-notes-may-21st>
- **Visto:** texto da página.
- **O que diz:**
  - "Dark mode available across all Square Point of Sale apps on iOS."
  - O modo escuro "reduces glare so your team can ring up sales", para "low-light environments".
  - Ele segue o ajuste do sistema.
  - No KDS: "During a busy service, kitchen staff need to spot critical modifier details", com cor de texto própria para esses detalhes.
- **Princípio:**
  - Um tema escuro de primeira classe para operação com pouca luz.
  - Cor reservada para o detalhe crítico, e não como decoração.
- **Aplica porque:** a guarita pode funcionar à noite (iluminação desconhecida). Situações como pagamento pendente ou placa sem cliente precisam saltar aos olhos.
- **Evitar:** tratar o modo escuro como estética. O motivo citado é ofuscamento e legibilidade.

**R6. Dynatrace Design System: DataTable, uso.** <https://developer.dynatrace.com/design/components/tables/DataTable/usage/>
- **Visto:** texto da página. As imagens de exemplo não foram vistas.
- **O que diz:**
  - Três densidades: default para "process and scan the information shown", condensed com o aviso de que "cell content might be more challenging for users to scan" e comfortable para "improve readability".
  - Zebra em "large datasets".
  - Ações de linhas selecionadas numa toolbar.
  - Cabeçalhos com "one or two simple words and sentence case".
  - Estado vazio "when there's no data or there's an error".
- **Princípio:** a densidade é uma escolha por tela, não global. Use a densidade default nas listas administrativas (clientes, operadores, tickets) e nunca a condensada na cancela.
- **Aplica porque:** as listas paginadas das Ondas 8 (TD-801, TD-802) terão até 100 itens.
- **Evitar:** colunas demais. Nome e telefone são cifrados e não ordenáveis (TD-710), então a tabela não deve prometer ordenação por eles.

### Fora da categoria

**R7. GOV.UK Design System: Error message.** <https://design-system.service.gov.uk/components/error-message/>
- **Visto:** texto da página.
- **O que diz:**
  - "Show an error message next to the field and in the Error summary component."
  - "Do not clear any form fields."
  - "Be specific": evite "An error occurred" e "This field is required".
  - Não use mensagem de erro para falta de permissão nem para problema do serviço.
- **Princípio:** o erro diz o que fazer, fica junto do campo e nunca apaga o que o operador digitou.
- **Aplica porque:**
  - A API devolve 400, 404 e 409 com motivo (placa já usada, documento inválido, ticket não encontrado).
  - O 403 da matriz D2 é uma situação de permissão, e não um erro de campo.
- **Evitar:** um toast genérico de "Erro" para tudo.

**R8. W3C: WCAG 2.2, Understanding SC 2.5.8 Target Size (Minimum).** <https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html>
- **Visto:** texto da página, atualizado em 2 de novembro de 2026.
- **O que diz:**
  - O nível AA é "at least 24 by 24 CSS pixels", com exceção por espaçamento.
  - "For important links/controls, consider aiming for the stricter 2.5.5 Target Size (Enhanced)", que usa 44 por 44.
- **Princípio:** 44 px é o mínimo nos controles da cancela. Nas tabelas administrativas, 24 px com espaçamento é o piso legal, mas a skill fixa 44 px como limiar, então a regra mais estrita prevalece.
- **Aplica porque:** a cancela pode usar toque ou luvas (desconhecido).
- **Evitar:** linhas condensadas com ações pequenas lado a lado.

### Não abertas

| Link | Resultado |
|---|---|
| <https://www.behance.net/search/projects/parking%20management%20dashboard> | HTTP 403 |
| <https://www.behance.net/search/projects/parking%20app> | HTTP 403 |
| <https://dribbble.com/search/parking-dashboard> | HTTP 202 com corpo vazio |
| <https://mobbin.com/search/apps/web?content_type=screens&q=parking> | HTTP 403 |
| <https://www.sap.com/design-system/btp/dna-design/floorplans/table/usage> | HTTP 403 |
| <https://m2.material.io/design/layout/applying-density.html> | Abriu, mas só com o título "Material Design" |

Nada dessas páginas é citado acima.

## 3. Princípios consolidados

**Seguir:**
1. **A cancela é uma tela de uma tarefa:** um campo (placa ou código), um resultado e uma ação primária grande. (R1, R3)
2. **A placa é a chave de navegação:** da busca, um clique leva ao ticket e ao cliente. (R4)
3. **Cor só para estado crítico:** pagamento pendente, placa sem cliente e erro. Todo o resto fica neutro. (R5)
4. **Tema escuro de primeira classe,** se a guarita operar à noite. (R5)
5. **Densidade por tela:** default nas listas e espaçosa na cancela. (R6)
6. **Erros específicos, junto do campo, sem apagar o que foi digitado.** O 403 é tratado à parte. (R7)
7. **Alvos de 44 px no mínimo.** (R8)
8. **Toda ação manual mostra quem fez e quando.** (R1, R2)

**Evitar:**
- Dashboards de receita, heat maps, foto de placa e modo offline: não temos dados, câmera nem suporte.
- Modo condensado e toasts genéricos.

## 4. Lacunas desta rodada

- **Sem referência visual:** nenhuma página foi vista como imagem, então as direções de estilo do estágio 3 não têm base visual de terceiros. Cada direção vai dizer isso. Com o Claude in Chrome conectado, dá para repetir esta rodada com screenshots e incluir galerias como Behance, Dribbble e Mobbin.
- **Faltam respostas do brief:** dispositivo, iluminação e identidade visual mudam a escolha entre densidade e tema.
