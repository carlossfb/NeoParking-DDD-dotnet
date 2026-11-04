# Spec: Evolução do produto

| | |
|---|---|
| **Status** | Aprovada, salvo as perguntas ainda abertas na seção 7 |
| **Data** | 2026-11-03 |
| **Escopo** | Capacidades de negócio descritas no README que ainda não existem ou estão incompletas. |
| **Relacionada** | [`technical-debt-remediation.md`](technical-debt-remediation.md) |

**Como ler as origens:**

- `README:n` e `ADR:n` citam linhas de `README.md` e `docs/architecture-decisions.md`.
- **Premissa:** inferência desta spec, sem confirmação do negócio. Pode ser revertida.
- **Decisão do usuário:** regra definida pelo dono do produto, registrada na seção 1.1. Prevalece sobre README e ADRs onde houver diferença.
- **Aprovada por delegação:** recomendação desta spec aceita pelo usuário de forma geral ("de resto segue suas recomendações"), sem discussão item a item. Pode ser revista com menos custo que uma decisão explícita.
- **Pergunta aberta:** o negócio ainda precisa responder. Está consolidada na seção 7.

## 1. Visão e objetivos

O NeoParking é um estacionamento convencional que quer modernizar a operação sem perder o atendimento humano, seu diferencial (`README:36`). Os problemas declarados são a gestão manual de tickets e a falta de visibilidade das vagas (`README:36`). A receita vem de avulsos na "free zone" e de mensalistas (`README:38`). O core é o Access, a cobrança por hora é o padrão do setor, e o mensalista paga uma mensalidade com franquia de uso (seção 1.1).

**Objetivos:**

1. Dar visibilidade de vagas e ocupação (`README:36,84-85`).
2. Eliminar o controle manual do ticket, da entrada ao pagamento (`README:36,82,88`).
3. Sustentar mensalistas com entrada automatizada e planos mutáveis (`README:86-87,94`).
4. Manter o operador humano no fluxo: toda automação tem saída manual (`README:36`).

**Princípio de desenho:** o cálculo por hora não muda quando um plano muda (`README:46`). A assinatura não altera a fórmula: ela só define quanto da visita a franquia cobre.

**Metas e métricas:**

| Métrica | Meta | Origem |
|---|---|---|
| Volume suportado | 1000+ transações por dia | `README:52` |
| Disponibilidade | 99,5%, com degradação controlada | `README:53` |
| Consistência entre contextos | Eventual | `README:54` |
| Atraso entre evento na cancela e ocupação visível | p95 até 15 s | Proposta; hoje o outbox faz polling a cada 10 s (`OutboxProcessor.cs:17`) |
| Divergência entre contagem do sistema e contagem física | Até 2 vagas por turno | Proposta |
| Tickets fechados com valor devido e sem pagamento registrado | Até 1% | Proposta |
| Saídas de mensalistas sem intervenção do operador | 90% ou mais | Proposta |

O README não traz linha de base. As metas propostas devem ser recalibradas com a primeira medição real.

### 1.1 Decisões do usuário (2026-11-03)

| # | Decisão | Consequência |
|---|---|---|
| N1 | **Access é o core domain**: a parte de estacionamento é o centro do negócio. | Confirma o `ADR:10`. |
| N2 | **O mensalista paga um valor fixo por assinatura**, sem modelo de desconto. N5 detalha o que esse valor cobre. | Resolve a antiga P1. A C4 não tem benefício percentual. |
| N3 | **Quem não é mensalista continua usando o estacionamento** e paga por uso. | A assinatura é uma opção, não uma condição de acesso. |
| N4 | **A cobrança por hora é o padrão genérico do setor:** taxa inicial mais valor acrescido por hora. | Billing é genérico, o que confirma o `ADR:12`. |
| N5 | **O plano é uma mensalidade com franquia de uso** (exemplo do usuário: 3h, 2 visitas, inspirado em estacionamentos de academia). | Substitui a regra anterior de que toda visita com assinatura ativa fecha sem Fee: agora isso só vale dentro da franquia. Continua sem desconto percentual. Os parâmetros do plano são dados editáveis, porque planos são voláteis (`README:46`). |
| N6 | **A free zone são as vagas não reservadas a mensalistas**, e deve haver sobra para eles. | Substitui a premissa de área compartilhada em P2. Afeta C1. |
| N7 | **O motorista paga antes de sair.** Pagamento feito muito antes da saída perde a validade e exige pagar a diferença. | Resolve P4. Inverte o fluxo de C3 (seção 4). |
| N8 | **Franquia de exemplo: 2 visitas por dia, com 3h por visita.** São parâmetros editáveis do plano. | Fecha P11 e P12. |
| N9 | **A placa do ticket precisa pertencer ao cliente informado.** | Impede consumir a franquia de outra pessoa ou sair sem pagar. Afeta C4 e C5. |

**Cálculo básico já existe:** a `PricingPolicy` tem taxa mínima e valor por hora (`PricingPolicy.cs:21-22`), e o `FeeCalculator` soma os dois (`FeeCalculator.cs:48`). Esse formato não precisa de nova capacidade.

**Receita e core são eixos diferentes:** o `README:46` trata a hora como receita estável, o que não a torna core domain. Por isso não há contradição com N1 e N4.

**Correções a fazer na documentação (Onda 5, TD-504):**

- `README:48`: remover "discounts" como forma de derivar os outros modelos da hora.
- `ADR:23`: remover "subscriber discounts" do racional de Billing.
- `README:94`: incluir a franquia, porque "recurring fee rather than per visit" não cobre o uso acima dela.
- `README:38`: definir "free zone" conforme N6.

### 1.2 Recomendações aprovadas

O usuário aprovou R1 e R2 explicitamente ("aprovo cota e 15 min"), e as demais por delegação ("de resto segue suas recomendações"). Como R3 foi tratada na mesma resposta, conta como decisão do usuário; R4, R5 e R6 são aprovações por delegação.

| # | Recomendação | Motivo |
|---|---|---|
| R1 | **Reserva por cota de capacidade, sem vaga numerada.** Capacidade total = cota de mensalistas + free zone, ambas configuráveis. O avulso é barrado quando a free zone enche. O mensalista usa a cota e, se ela esgotar, pode usar a free zone se houver vaga. A cota é dimensionada pelo uso simultâneo esperado, não pelo total de assinantes. | Responde à dúvida do usuário sobre reservar ou não: garante a sobra de N6 sem sensor por vaga e sem vaga numerada, e a cota parada fica limitada ao tamanho configurado. |
| R2 | **Janela de saída após o pagamento**, com padrão de 15 minutos, configurável. Modelada como validade do pagamento, sem invalidar o ticket: o ticket continua aberto e, passado o prazo, a saída exige pagar a diferença entre a taxa recalculada e o valor pago, sem nova taxa inicial. | Atende N7 sem criar um segundo ticket e sem perder o histórico da sessão. |
| R3 | **Mensalista que excede a franquia paga o excedente pela tarifa por hora**, no mesmo fluxo de pagamento antes da saída. | Reutiliza a tarifa e o fluxo existentes, sem cálculo próprio de plano. |
| R4 | **Liberação da saída por consulta síncrona, com porta no Access** (seção 5). | Evita que o polling do outbox segure a cancela. Exige um ADR, por abrir exceção à regra de comunicação só por eventos. |
| R5 | **A contagem de franquia fica em Billing** (seção 5). | É ele que transforma tempo em valor, e a cotação precisa da franquia antes da saída. |
| R6 | **A decisão de barrar a entrada fica no Access** (seção 5). | É o contexto que registra a entrada e conta os tickets abertos na própria transação. |

## 2. Mapa de lacunas

| Capacidade do README | Estado | Evidência no código | Contexto dono |
|---|---|---|---|
| Registrar entrada e saída (`README:79,81`) | Existe | `Access/Domain/Ticket.cs:60,76` | Access |
| Controlar permanência para tarifa (`README:83`) | Existe | `Billing/Domain/FeeCalculator.cs:23` | Billing |
| Fee com carência (`README:96`) | Existe | `FeeCalculator.cs:34` | Billing |
| Tarifa versionada e Fee congelada | Existe, além do README | `PricingPolicy.cs:18`, `Billing/Domain/Ticket.cs:125` | Billing |
| Cliente e veículo (`README:93`) | Existe | `Access/Domain/Client.cs`, `Vehicle.cs` | Access |
| Operadores e papéis (`README:92,111-113`) | Existe | `Management/Domain/OperatorUser.cs`; autorização corrigida na Onda 1 | Management |
| Ticket vinculado a Client | Parcial | `ClientId` é informado à mão e não é validado (`Access/Application/TicketService.cs:42`) | Access |
| Ticket físico com QR ou digital (`README:95`) | Ausente | Só uma menção em comentário (`Access/Domain/Ticket.cs:21`) | Access |
| Validar ticket ou identificação na saída (`README:82`) | Ausente | A saída usa só o Id (`Endpoints/TicketEndpoints.cs:21`) | Access |
| Vagas disponíveis e ocupação (`README:84-85`) | Ausente | Nenhum código; a `ListAsync` de tickets só filtra | Occupancy |
| Processar pagamento (`README:88`) | Ausente | Fee é calculada, mas nada registra pagamento | Billing |
| Subscriber e planos (`README:86,94`) | Ausente | Nenhum código | Subscription |
| Atualizar permissões pela assinatura (`README:87`) | Ausente | Nenhum código | Access, a partir de eventos |
| RFID e entrada automatizada (`README:80,97`) | Ausente | Só uma menção em comentário | Access |
| Regras especiais e multas (`README:96`) | Ausente | Só carência existe | Billing |
| Área do ticket (`README:95`) | Ausente | `Ticket` não tem área | Access e Occupancy |

**Lacunas que afetam várias capacidades:**

- Billing descarta o `ClientId` do `TicketOpened`: `Ticket.OpenFromAccess` nem recebe esse dado (`Billing/Domain/Ticket.cs:98`). C4 precisa dele.
- A Fee só existe depois do processamento assíncrono do `TicketClosed` (`Billing/Application/TicketService.cs:54,65`), mas N7 exige o valor antes da saída. C3 precisa resolver isso.
- O horário de saída vem do cliente da API (`RegisterExitRequest`, `Access/Application/TicketContracts.cs:9`; `Access/Domain/Ticket.cs:81`). Com validade de pagamento, informar um `ExitAt` antigo burlaria a janela. C3 precisa usar o relógio do servidor.
- O `TicketClosed` hoje tem um único consumidor. Com vários, uma falha pode travar os demais (ver EVO-0 na seção 3).

## 3. Sequência recomendada

| Ordem | Capacidade | Por que nesta posição | Depende de |
|---|---|---|---|
| 1 | **C2:** Ticket com código e validação na saída | É do core (N1), resolve uma dor declarada, tem menos dependências e dá ao pagamento uma forma de achar o ticket | Onda 1 |
| Pré | **EVO-0:** entrega por consumidor no outbox | Occupancy será o segundo consumidor de eventos de ticket | Onda 3 |
| 2 | **C1:** Ocupação e disponibilidade | Outra dor declarada, mas de subdomínio de suporte (`ADR:11`); não envolve dinheiro nem hardware. A cota é configurada aqui, mas só faz efeito em C4, quando existir mensalista | EVO-0 |
| 3 | **C3:** Pagamento de ticket antes da saída | Fecha a receita por uso (`README:46`), sem depender de gateway; ativa o bloqueio de saída por pagamento | C2, Onda 3 |
| 4 | **C4:** Subscriber, planos, franquia e cobrança recorrente | Segunda fonte de receita; reutiliza o `Payment` e a cotação de C3 para mensalidade e excedente | C3 |
| 5 | **C5:** Credenciais RFID/QR e permissão de acesso | A permissão depende do estado da assinatura (`README:87`); exige decidir hardware | C4 |
| 6 | **C6:** Regras especiais e multas | O README só diz que *podem* existir (`README:96`); não há regra definida | C3 e uma regra concreta |
| 7 | **C7:** Área do ticket | Sem modelo de áreas ou vagas no README; só faz sentido se o negócio pedir | Pergunta P17 |

**O que mudou com N1:** C2 passou à frente de C1 por ser core e ter menos dependências. C2 já vinha antes das capacidades de Billing.

**Por que C5 não avança,** mesmo sendo do core: a permissão depende do estado de uma assinatura que ainda não existe (`README:87`), e ela exige escolher hardware. Por isso continua depois de C4.

**C6 e C7 ficam por último** porque não têm regra definida. Construí-las agora seria inventar regra de negócio. Com R1, a free zone é cota, não zona física.

**N5 a N7 não mudam a ordem,** e as dependências ficam assim:

- **C2 → C3:** C2 entrega a validação sem bloqueio por pagamento. O bloqueio só entra com C3, e até lá o fluxo atual da Fee continua igual.
- **C3 → C4:** o excedente da franquia usa a cotação e o pagamento de C3.
- **C1 → C4:** C1 entrega a capacidade total e a configuração da cota. A separação entre mensalista e avulso e o bloqueio por free zone só valem depois de C4; antes disso, todo ticket conta como avulso.
- **C4 → C5:** a saída automática de avulsos em C5 usa a mesma liberação de C3.

### EVO-0: Entrega isolada por consumidor

- **Problema:** o `OutboxProcessor` marca a mensagem como publicada só se `Publish` terminar sem erro (`OutboxProcessor.cs:66-68`). **Premissa a verificar na versão instalada:** o publicador padrão do MediatR para no primeiro handler que falha. Assim, um `TicketClosed` sem tarifa vigente (Billing falha) impediria Occupancy de liberar a vaga, dependendo da ordem dos handlers.
- **Requisito:** a falha de um consumidor não pode impedir a execução dos outros. Todo consumidor deve ser idempotente, porque um retry reentrega a mensagem a todos.
- **Aceite:** com um handler que sempre falha e outro saudável para o mesmo evento, o saudável processa o evento uma vez em efeito final, em qualquer ordem de registro. O retry não duplica esse efeito.
- **Relação com a dívida técnica:** deve ser feito junto do TD-305 (dead-letter). Uma mensagem em dead-letter por causa de um consumidor não pode representar falha dos demais.

## 4. Specs por capacidade

Regras comuns a todas as capacidades:

- Endpoints exigem autenticação conforme a matriz D2. Os papéis abaixo são **Premissa** até aprovação, e cada capacidade estende a matriz.
- O autor da ação sempre vem do claim do token, nunca da rota ou do body (Onda 1).
- Comunicação entre contextos só por eventos de integração via outbox (`README:55,203`). A única exceção é a consulta de liberação da saída (R4), que exige um ADR.
- Consumidores idempotentes, com teste de reentrega.

---

### C1: Ocupação e disponibilidade

**Problema:** falta visibilidade sobre a disponibilidade de vagas (`README:36`). O operador precisa ver vagas livres e acompanhar a ocupação (`README:84-85`).

**Escopo:** capacidade e cota de mensalistas configuráveis (R1), contagem derivada dos eventos de ticket, ajuste manual auditado, consulta atual e histórico. A contagem separada por tipo só faz efeito a partir de C4.

**Fora de escopo:** sensores IoT, vaga numerada, reserva de vaga por pessoa, painel externo e push em tempo real. Polling REST atende até haver pedido específico.

**Histórias:**

- Como operador, vejo quantas vagas estão livres agora, para orientar quem chega.
- Como Owner, defino a capacidade do estacionamento e a cota de mensalistas.
- Como operador, corrijo a contagem quando ela diverge do pátio, informando o motivo.
- Como Owner ou Admin, consulto a ocupação de um período para tomar decisões.

**Regras de negócio:**

| Regra | Origem |
|---|---|
| Vagas livres = capacidade − sessões abertas + ajuste vigente, sem ficar abaixo de zero na exibição | Premissa |
| Ocupação é eventualmente consistente | `README:54` |
| Free zone são as vagas não reservadas a mensalistas, com sobra para eles | Decisão do usuário (N6) |
| Capacidade total = cota de mensalistas + free zone, sem vaga numerada | Decisão do usuário (R1) |
| Vagas livres da free zone = free zone − avulsos − mensalistas além da cota, sem ficar abaixo de zero | Decisão do usuário (R1) |
| Avulso é barrado com a free zone cheia; mensalista usa a cota e, se esgotada, a free zone | Decisão do usuário (R1) |
| Occupancy informa e não bloqueia; a entrada é decidida no Access (R6), e o operador pode liberar com motivo e autor | Aprovada por delegação; substitui a premissa de que lotação era só informação. A saída manual vem de `README:36` |
| Ajuste manual exige motivo e autor | Premissa, supervisão humana em `README:38` |
| Ocupação acima da capacidade é exibida como excedente, não como erro | Premissa |

**Modelo:**

- `ParkingLot`: capacidade e cota de mensalistas. Instância única (sem multi-unidade).
- `ParkingSession`: `TicketId` como chave, `Kind` (`WalkIn`, `Subscriber`), `EntryAt`, `ExitAt?`. É a fonte da contagem e do histórico, e a chave torna a reentrega idempotente.
- `OccupancyAdjustment`: delta, motivo, autor, instante.

**Contexto dono:** Occupancy (seção 5). O `ADR:16` permite Transaction Script, e isso basta aqui.

**Eventos:** consome `TicketOpened` e `TicketClosed`. Não publica nenhum até haver consumidor.

**Endpoints:**

| Endpoint | Autorização |
|---|---|
| `GET /occupancy/availability` | Qualquer operador |
| `GET /occupancy/history?from&to` | Owner/Admin |
| `PUT /occupancy/capacity` (capacidade e cota) | Owner |
| `POST /occupancy/adjustments` | Qualquer operador |

**Critérios de aceite:**

1. Com capacidade 100 e 3 tickets abertos, `availability` retorna 97 livres em até 15 s.
2. Reentregar `TicketOpened` ou `TicketClosed` não altera a contagem.
3. `TicketClosed` processado antes de `TicketOpened` para o mesmo ticket termina com a sessão fechada, sem ocupar vaga.
4. Uma falha de Billing no mesmo evento não impede a liberação da vaga (EVO-0).
5. Ajuste sem motivo é rejeitado; ajuste válido altera a disponibilidade e fica registrado com o autor do token.
6. Com 101 sessões em capacidade 100, a resposta mostra 0 livres e excedente 1, sem erro. Uma liberação manual pode gerar esse caso.
7. Sem capacidade definida, `availability` informa esse estado em vez de um número inventado.
8. A API rejeita uma cota maior que a capacidade.
9. Depois de C4, com capacidade 100 e cota 20: 75 avulsos e 25 mensalistas deixam a free zone com 0 livres. O próximo avulso é barrado, com liberação manual possível; 80 avulsos e 5 mensalistas ainda deixam 15 vagas na cota.

**Na implantação:** tickets já abertos precisam ser carregados uma vez, caso contrário as primeiras saídas fechariam sessões inexistentes.

**Decisões aplicadas:** R1 e R6. **Aprovado por delegação (P3):** a ocupação vem dos eventos de ticket, com ajuste manual; sensores só se a divergência medida justificar.

---

### C2: Ticket com código e validação na saída

**Problema:** a gestão manual de tickets é uma dor declarada (`README:36`). O ticket pode ser físico com QR ou digital (`README:95`) e deve ser validado na saída (`README:82`). Hoje a saída exige o Id interno.

**Escopo:** código único de ticket, consulta por código, saída por código e saída por placa como alternativa.

**Fora de escopo:** impressora e renderização do QR (responsabilidade do cliente ou de um adaptador), envio do ticket digital ao motorista e qualquer regra de multa por perda.

**Histórias:**

- Como operador, gero na entrada um ticket com um código para imprimir ou mostrar.
- Como operador, leio o código na saída e encerro o ticket certo.
- Como operador, encerro pela placa quando o ticket se perdeu.

**Regras de negócio:**

| Regra | Origem |
|---|---|
| Ticket representa a sessão e pode ser físico ou digital | `README:95` |
| Identificação é validada na saída | `README:82` |
| Físico e digital são a mesma sessão, com o mesmo código | Premissa |
| O código é opaco e não sequencial | Premissa, para impedir adivinhação |
| A saída por placa exige exatamente um ticket aberto e registra o motivo e o operador | Premissa; `GetOpenByPlateAsync` já existe |

**Modelo:** `TicketCode` (value object) em `Ticket`; `ExitMethod` (`Code`, `Plate`) registrado na saída.

**Contexto dono:** Access.

**Eventos:** sem novos. `TicketClosed` ganha `ExitMethod` só quando houver consumidor.

**Endpoints:**

| Endpoint | Autorização |
|---|---|
| `POST /access/tickets` (resposta inclui `code`) | Qualquer operador |
| `GET /access/tickets/by-code/{code}` | Qualquer operador |
| `POST /access/tickets/by-code/{code}/exit` | Qualquer operador |
| `POST /access/tickets/by-plate/{plate}/exit` | Qualquer operador; motivo obrigatório |

**Critérios de aceite:**

1. Dois tickets nunca têm o mesmo código, garantido por índice único.
2. Código inexistente retorna 404; ticket já encerrado retorna 422 sem emitir novo evento.
3. A saída por placa sem motivo é rejeitada, e o motivo fica registrado com o autor.
4. Tickets abertos antes da migração recebem código sem alterar `EntryAt`.
5. A saída por qualquer método publica um único `TicketClosed`, e o fluxo da Fee continua igual. Nesta capacidade, a saída não é bloqueada por pagamento; o bloqueio entra com C3 e vale para todos os métodos.

**Aprovado por delegação (P6):** ticket perdido sai por placa, com motivo, sem multa até haver regra aprovada.

---

### C3: Pagamento de ticket antes da saída

**Problema:** o sistema calcula a Fee, mas não registra se ela foi paga (`README:88`). A receita por uso (`README:46`) não fecha.

**Escopo:** cotação antes da saída, registro de pagamento pelo operador, validade do pagamento, pagamento da diferença, bloqueio da saída sem pagamento válido, liberação manual, status de pagamento e relatório de pendências.

**Fora de escopo:** gateway real, estorno, pagamento parcial além da diferença, nota fiscal e autoatendimento. O gateway entra como adaptador quando houver uma escolha concreta (seção 5).

**Histórias:**

- Como operador, vejo quanto o motorista deve antes de cobrar.
- Como operador, registro o pagamento e o meio usado.
- Como motorista, depois de pagar, tenho um prazo para sair sem nova cobrança.
- Como operador, cobro só a diferença de quem pagou e passou do prazo.
- Como operador, libero uma saída manualmente, informando o motivo.
- Como Owner ou Admin, vejo os tickets encerrados sem pagamento.

**Regras de negócio:**

| Regra | Origem |
|---|---|
| Pagamento de ticket avulso existe | `README:88` |
| Fee congelada não é recalculada | Código: `Billing/Domain/Ticket.cs:19-22` |
| O motorista paga antes de sair | Decisão do usuário (N7) |
| Pagamento feito muito antes da saída perde a validade e exige pagar a diferença | Decisão do usuário (N7) |
| A validade é uma janela configurável após o pagamento, com 15 minutos como padrão; o ticket continua aberto | Decisão do usuário (R2) |
| A diferença = taxa recalculada − total pago, sem nova taxa inicial | Decisão do usuário (R2) |
| Cada pagamento quita a cotação vigente; sem pagamento parcial além da diferença | Premissa |
| Saída sem pagamento válido é bloqueada, exceto por liberação manual com motivo e autor | Premissa, alinhada a `README:36`; a liberação fica como pendência em `unpaid` |
| O horário da saída vem do relógio do servidor | Premissa; hoje é informado na requisição (`TicketContracts.cs:9`), o que burlaria a janela |
| Ticket com valor zero (carência) não gera pendência | Premissa, derivada de `FeeCalculator.cs:34` |
| Um ticket pode ter vários pagamentos: o inicial e as diferenças | Consequência de N7; substitui a premissa de um único pagamento |

**Modelo:**

- `Payment` (agregado): `Id`, `TicketId`, `Amount`, `Method`, `ExternalReference?`, `RegisteredBy`, `PaidAt`, `ValidUntil`.
- `TicketQuote` (consulta, sem persistência): usa o `FeeCalculator` existente (`FeeCalculator.cs:23`) com o instante atual e informa total, já pago e saldo a pagar.
- `TicketPaymentStatus` (derivado): `NotDue`, `Pending`, `PaidValid`, `PaidExpired`, `Settled`.

**Fluxo novo:** hoje o Access fecha o ticket e só depois o Billing calcula a Fee, ao consumir `TicketClosed` (`Billing/Application/TicketService.cs:54-65`). Com N7, a ordem passa a ser:

1. O operador pede a cotação ao Billing, com o ticket ainda aberto.
2. O operador registra o pagamento, que ganha um `ValidUntil`.
3. Na saída, o Access consulta a liberação (seção 5). Se o prazo venceu, volta ao passo 1 pela diferença.
4. O Access fecha o ticket e publica `TicketClosed`.
5. O Billing congela a Fee.

**Reconciliação entre valor pago e Fee final (Premissa):** a Fee continua congelada só na saída, e o cálculo usa um instante de cobrança em vez de sempre usar `ExitAt`:

- **Saída dentro da janela:** o instante é o do último pagamento válido. Assim, a Fee é igual ao total pago, mesmo que a janela cruze uma nova hora. É o que dá sentido à janela.
- **Saída sem pagamento válido** (liberação manual): o instante é `ExitAt`, e o saldo fica como pendência em `unpaid`.
- **Invariante:** para ticket `Settled`, a soma dos pagamentos é igual à Fee congelada. Qualquer divergência, inclusive por tarifa alterada (P15), fica registrada para o operador em vez de ser ajustada em silêncio.

**Ticket recém-aberto:** o Billing só conhece o ticket depois de processar `TicketOpened`, com até cerca de 10 s de atraso (`OutboxProcessor.cs:17`). Nesse intervalo, a cotação informa que o ticket ainda não está disponível.

**Contexto dono:** Billing (`ADR:12`).

**Eventos:** nenhum novo, porque R4 usa consulta síncrona (seção 5).

**Endpoints:**

| Endpoint | Autorização |
|---|---|
| `GET /billing/tickets/{id}/quote` | Qualquer operador |
| `POST /access/tickets/{id}/manual-release` (motivo obrigatório) | Qualquer operador |
| `POST /billing/tickets/{id}/payments` (com chave de idempotência) | Qualquer operador |
| `GET /billing/tickets/{id}/payments` | Qualquer operador |
| `GET /billing/tickets/unpaid?from&to` | Owner/Admin |

**Critérios de aceite:**

1. `quote` de ticket aberto usa a mesma fórmula do fechamento, não grava nada e informa total, já pago e saldo.
2. Pagamento com valor diferente do saldo da cotação retorna 422.
3. Dois pedidos com a mesma chave criam um único pagamento. Com pagamento válido, ou sem saldo, um novo pagamento com outra chave retorna 409.
4. Ticket aberto aceita pagamento. Ticket fechado com pendência aceita a quitação do saldo; ticket quitado, não.
5. Ticket dentro da carência fica como `NotDue` e fora de `unpaid`.
6. `RegisteredBy` vem do token, nunca do body.
7. Mudar a tarifa após o congelamento não altera o valor devido.
8. Saída sem pagamento válido é bloqueada e informa o saldo; com ele quitado, é liberada.
9. Saída dentro da janela, mesmo cruzando uma nova hora, não gera cobrança adicional, e a Fee congelada é igual ao total pago.
10. Com a janela vencida, a saída exige a diferença entre a taxa recalculada e o total pago, sem somar uma nova taxa inicial, e o pagamento da diferença abre nova janela.
11. Um `ExitAt` informado pelo cliente não altera a validação da janela.
12. Liberação manual sem motivo é rejeitada; com motivo, fica registrada com o autor do token, e o saldo aparece em `unpaid`.
13. Reentregar `TicketClosed` não muda a Fee nem os pagamentos.

**Aprovado por delegação:** P5 (registro manual com lista configurável de meios; provedor só depois) e P8 (exigir tarifa antes de operar, como passo de implantação, mantendo a sentinela só como proteção). **Pergunta aberta:** P15 (tarifa da diferença).

---

### C4: Subscriber, planos, franquia e cobrança recorrente

**Problema:** mensalistas são uma das duas fontes de receita (`README:38`), mas não existem no sistema. O ciclo de renovar, cancelar e atualizar também não existe (`README:86`).

**Escopo:** catálogo versionado de planos com valor fixo e franquia, assinatura de um Client, consumo da franquia, excedente pago antes da saída e cobrança recorrente mensal.

**Fora de escopo:** diária e pacotes além do mensal até haver regra (`README:46`), proporcionalidade, cobrança automática em cartão e credencial de acesso (C5).

**Histórias:**

- Como Owner, crio ou altero um plano sem afetar quem pagou no passado.
- Como operador, assino, renovo ou cancelo o plano de um Client.
- Como mensalista, não pago pela visita enquanto ela couber na franquia do meu plano.
- Como mensalista, pago só o excedente quando passo da franquia, antes de sair.
- Como operador, vejo o saldo da franquia de um mensalista.
- Como cliente sem assinatura, continuo usando o estacionamento e pago por uso.
- Como Owner ou Admin, vejo as assinaturas com cobrança em atraso.

**Regras de negócio:**

| Regra | Origem |
|---|---|
| O mensalista paga valor fixo, sem desconto | Decisão do usuário (N2), `README:94` |
| O plano dá direito a uma franquia de uso, por exemplo 3h e 2 visitas | Decisão do usuário (N5) |
| Visita dentro da franquia não gera Fee | Decisão do usuário (N5); substitui a regra de isenção para toda visita com assinatura ativa |
| Parâmetros de plano e de franquia são dados editáveis | Decisão do usuário (N5), `README:46` |
| O excedente é pago pela tarifa por hora, no fluxo de pagamento antes da saída | Decisão do usuário (R3) |
| A contagem da franquia fica em Billing | Aprovada por delegação (R5) |
| Exemplo de franquia: 2 visitas por dia, 3h por visita | Decisão do usuário (N8) |
| Saldo não usado não acumula | Registrado conforme a decisão de franquia diária (N8), aprovado por delegação |
| O tempo acima de 3h em uma visita é excedente | Aprovada por delegação (P14) |
| Visita além da 2ª no dia é cobrada como avulsa desde a entrada, com a fórmula completa | Aprovada por delegação (P14) |
| O "dia" da franquia é o dia local do estacionamento na `EntryAt`; uma visita que cruza a meia-noite conta no dia da entrada | Premissa |
| Mensalista vencido ou suspenso é cobrado como avulso, com tolerância definida no plano, e volta a ficar ativo ao pagar | Aprovada por delegação (P7) |
| Assinatura por Client, credencial por veículo e um veículo no pátio por assinatura | Aprovada por delegação (P9) || Quem não tem assinatura ativa usa normalmente e paga por uso | Decisão do usuário (N3) |
| Planos mudam com frequência; o cálculo por hora permanece estável | `README:46` |
| Plano versionado, nos moldes da `PricingPolicy` | Premissa |
| A assinatura é verificada na cotação e, de novo, no fechamento, usando o mesmo instante de cobrança de C3 | Premissa, alinhada a `Billing/Domain/Ticket.cs:125` |
| **A placa do ticket precisa pertencer ao `ClientId` informado.** Access resolve o Client pela placa cadastrada, e o operador não escolhe livremente | Decisão do usuário (N9). Hoje a entrada aceita qualquer `request.ClientId` sem conferir o vínculo (`Access/Application/TicketService.cs:42`). Sem isso, um carro avulso informado com o `ClientId` de um mensalista consome a franquia desse cliente e pode sair sem pagar |

**Modelo:**

- Subscription: `SubscriptionPlan` (revisão com `Name`, `RecurringPrice`, `VisitsPerDay`, `MinutesPerVisit`, `EffectiveFrom/To`), `Subscription` (`ClientId`, `PlanRevisionId`, `Status`, `CurrentPeriod`).
- Billing:
  - `ClientSubscriptionStatus` (projeção local): `ClientId`, `SubscriptionId`, `ActiveFrom`, `ActiveTo?`, parâmetros de franquia da revisão vigente.
  - `AllowanceUsage`: `SubscriptionId`, período, `TicketId`, consumo. O par assinatura e ticket é único, o que torna o consumo idempotente.
  - `Ticket.ClientId`: passa a ser guardado. O `TicketOpenedIntegrationEvent` já traz esse dado, que `Ticket.OpenFromAccess` hoje descarta (`Billing/Domain/Ticket.cs:98`).
  - `Ticket.CoveredBySubscriptionId?`: preenchido quando a franquia cobre a visita, no todo ou em parte. Se cobrir tudo, o ticket fecha sem `Fee`; se cobrir parte, a `Fee` congela só o excedente.
  - `SubscriptionCharge`: mensalidade a receber, paga com o mesmo `Payment` de C3.

**Como Billing fica sabendo, sem referência entre módulos:** Billing usa o `ClientId` guardado e consulta a própria projeção, alimentada por eventos de Subscription, tanto na cotação quanto ao processar `TicketClosed`. Com assinatura ativa, aplica a franquia. Sem `ClientId` ou sem assinatura ativa, segue o cálculo atual.

**Franquia e cálculo:** um componente próprio de Billing decide o caso da visita, e o `FeeCalculator` continua sem conhecer assinatura:

- **Até a 2ª visita do dia, até 3h:** sem Fee.
- **Até a 2ª visita do dia, acima de 3h:** cobra só o tempo acima de 3h pelo valor por hora, sem taxa inicial e com o mesmo arredondamento da tarifa (Premissa). Isso exige expor no cálculo o preço só por hora, sem mudar a fórmula atual.
- **3ª visita do dia em diante:** cobrada como avulsa desde a entrada, com a fórmula atual completa.

**Efeito na ocupação (Premissa):** a visita além da franquia é avulsa na cobrança, mas continua classificada como mensalista em `ParkingSession.Kind`, porque o Access não conhece a contagem de franquia (R5).

**Quando consome:** a cotação só lê o saldo. O consumo é gravado ao processar `TicketClosed`, na mesma transação que fecha o ticket em Billing. Duas sessões simultâneas do mesmo mensalista podem ver o mesmo saldo; P9 limita isso.

**Limite da consistência eventual (`README:54`):** um evento de ativação atrasado pode cobrar uma visita que estaria dentro da franquia. **Premissa:** com o outbox em polling de 10 s, isso é tratado como ajuste operacional, não como bloqueio do fechamento.

Com isso, um plano novo é só dado, e `FeeCalculator` e `PricingPolicy` não mudam por causa dele.

**Contexto dono:** Subscription, para plano e assinatura; Billing, para valores (seção 5).

**Eventos:**

| Evento | Publicado por | Consumido por | Para quê |
|---|---|---|---|
| `SubscriptionActivated`, `SubscriptionPlanRevisionApplied` | Subscription | Billing, Access | Iniciar a franquia e levar seus parâmetros; classificar o ticket como mensalista e permitir acesso (C5) |
| `SubscriptionSuspended`, `SubscriptionCancelled` | Subscription | Billing, Access | Encerrar a franquia e a permissão |
| `SubscriptionChargeDue` | Subscription | Billing | Criar a cobrança |
| `SubscriptionChargePaid` | Billing | Subscription | Renovar o período |

**Endpoints:**

| Endpoint | Autorização |
|---|---|
| `POST /subscriptions/plans` | Owner |
| `GET /subscriptions/plans` | Qualquer operador |
| `POST /subscriptions` | Qualquer operador |
| `GET /subscriptions/{id}` | Qualquer operador |
| `GET /billing/subscriptions/{id}/allowance` | Qualquer operador |
| `POST /subscriptions/{id}/change-plan`, `/cancel` | Owner/Admin |
| `POST /billing/subscription-charges/{id}/payments` | Qualquer operador |
| `GET /billing/subscription-charges/overdue` | Owner/Admin |

**Critérios de aceite:**

1. Alterar um plano não exige mudança em `FeeCalculator` nem em `PricingPolicy`, e os testes existentes deles continuam passando sem alteração.
2. Visita inteiramente dentro da franquia fecha sem Fee, guarda a assinatura que a cobriu, fica como `NotDue` e fora de `fee-history`, mesmo sem tarifa vigente. A saída não exige pagamento.
3. Ticket sem `ClientId`, ou de um Client sem assinatura, é cobrado pelo cálculo por hora atual.
4. Alterar um plano não muda ticket já fechado nem a revisão de assinaturas no período corrente, salvo mudança explícita.
5. Um Client tem no máximo uma assinatura ativa, garantido por índice.
6. O job de vencimento, executado duas vezes, cria uma única cobrança por período.
7. Pagar a cobrança renova o período uma única vez, mesmo com reentrega do evento.
8. Um teste de arquitetura garante que `FeeCalculator` não depende de tipos de assinatura.
9. Reentregar `TicketClosed` não muda o resultado, mesmo se o estado da assinatura tiver mudado depois do primeiro processamento.
10. `ClientId` inexistente é rejeitado na entrada, e o mesmo vale para uma placa que não pertence ao `ClientId` informado. Um teste prova que o carro de outra pessoa não consome a franquia de um mensalista nem sai sem pagar.
11. Com assinatura vencida ou suspensa na saída, a visita é cobrada como avulsa, respeitada a tolerância do plano.
12. Com a franquia esgotada ou excedida, a cotação mostra o excedente, e a saída exige esse pagamento pelo fluxo de C3.
13. Editar os parâmetros de franquia não exige mudança de código nem altera ticket já fechado.
14. Reentregar `TicketClosed` não consome a franquia duas vezes.
15. Com plano de 2 visitas por dia e 3h por visita: visita de 2h fica sem Fee; visita de 4h cobra só 1h pelo valor por hora, sem taxa inicial; a 3ª visita do dia usa a fórmula completa desde a entrada.
16. Saldo não usado em um dia não aumenta a franquia do dia seguinte.
17. Visita que cruza a meia-noite conta no dia da entrada.

**Decisões aplicadas:** N5, N8, N9, R3, R5, P7, P9, P11 a P14. **Sem perguntas abertas nesta capacidade.** A tolerância de P7 é um parâmetro do plano, ainda sem valor definido.

---

### C5: Credenciais RFID/QR e permissão de acesso

**Problema:** mensalistas devem ter entrada e saída automatizadas, sem intervenção manual (`README:94,97`). A permissão acompanha o estado da assinatura (`README:87`). Hoje o `ClientId` é digitado à mão no ticket e não é conferido com a placa (`Access/Application/TicketService.cs:42`).

**Escopo:** cadastro e bloqueio de credenciais, decisão de permitir ou negar, abertura e encerramento automáticos de ticket, porta para o leitor e a cancela, e encaminhamento ao operador em caso de negativa.

**Fora de escopo:** fornecedor específico de leitor ou firmware. O adaptador real depende do equipamento escolhido e, até lá, existe um simulador para testes.

**Histórias:**

- Como mensalista, apresento a credencial e a entrada é registrada sem operador.
- Como operador, vinculo ou bloqueio uma credencial perdida.
- Como operador, vejo por que uma entrada foi negada e posso liberar manualmente.

**Regras de negócio:**

| Regra | Origem |
|---|---|
| RFID ou QR identifica na entrada | `README:80` |
| RFID é atribuído a veículo ou usuário | `README:97` |
| Permissão muda com o status da assinatura | `README:87` |
| Falha ou negativa cai para atendimento humano, sem travar a operação | Premissa, de `README:36,53` |
| Negativa barra só a entrada automática; o operador pode registrar a pessoa como avulsa | Decisão do usuário (N3) |
| Liberação manual exige motivo e autor | Premissa |
| Credencial bloqueada é sempre negada | Premissa |
| O ticket automático usa o titular da credencial, e o veículo identificado precisa pertencer a ele | Decisão do usuário (N9), pelo mesmo motivo de C4: uma credencial em outro carro não pode consumir a franquia nem liberar a saída sem pagamento |
| A saída automática usa a mesma liberação de C3: excedente ou pagamento vencido encaminham ao operador | Decisão do usuário (N7), R4 |

**Modelo (Access):**

- `AccessCredential`: `Type` (`Rfid`, `QrCode`), valor protegido, titular (`ClientId`, com `VehicleId?`), `Status`.
- `AccessPermission` (projeção a partir de eventos de Subscription): `ClientId`, `IsAllowed`, `Reason`.
- `AccessAttempt`: credencial, ponto de acesso, decisão, motivo, instante.
- Portas: `IGateController` (abrir a cancela). O leitor é um adaptador de entrada que chama o caso de uso.

**Contexto dono:** Access (`ADR:10,15`).

**Eventos:** consome os de `Subscription*`. Reutiliza `TicketOpened` e `TicketClosed` com `ClientId` preenchido.

**Endpoints:**

| Endpoint | Autorização |
|---|---|
| `POST /access/credentials`, `POST /access/credentials/{id}/block` | Qualquer operador |
| `GET /clients/{id}/credentials` | Qualquer operador |
| `POST /access/gate/entry`, `/exit` | Identidade de dispositivo, ver abaixo |
| `POST /access/gate/manual-release` | Qualquer operador; motivo obrigatório |
| `GET /access/attempts?from&to` | Owner/Admin |

**Autorização de dispositivos:** a matriz D2 só prevê pessoas. O `README:109` exige que nada seja acessível sem a Management. **Premissa:** o dispositivo é uma identidade emitida e revogável pela Management, restrita às rotas `/access/gate/*`. A escolha do mecanismo deve considerar a limitação de 60 minutos da D3 e virar ADR.

**Critérios de aceite:**

1. Credencial válida com assinatura ativa abre ticket com `ClientId` e aciona a porta da cancela.
2. Assinatura suspensa ou credencial bloqueada resulta em negativa, com motivo registrado, sem ticket e sem abrir a cancela automaticamente; o operador ainda pode registrar uma entrada avulsa.
3. Depois de `SubscriptionSuspended`, a permissão muda ao processar o evento, e a reentrega não altera o resultado.
4. Credencial já com ticket aberto é rejeitada na entrada com motivo claro.
5. Falha do adaptador da cancela deixa o caso visível ao operador e não cria ticket duplicado em nova tentativa.
5a. Saída automática com saldo a pagar não abre a cancela e encaminha ao operador com o valor.
5b. Credencial usada em veículo de outro Client é negada e registrada.
6. Token de operador não acessa `/access/gate/entry`, e identidade de dispositivo não acessa outras rotas.
7. O valor da credencial não aparece em logs nem em respostas após o cadastro.

**Aprovado por delegação:** P9 (credencial por veículo) e, de P10, atendimento manual em queda e restrição do CPF. **Pergunta aberta:** P10 (equipamento).

---

### C6: Regras especiais e multas

**Problema:** a Fee pode incluir regras especiais, carências e multas (`README:96`). Só a carência existe, e nenhuma outra regra está definida.

**Escopo:** implementar **uma regra concreta por vez**, assim que o negócio definir. O candidato mais provável é ticket perdido (P6).

**Fora de escopo:** motor genérico de regras. Só justifica existir depois de várias regras reais.

**História:** como Owner, defino o valor de uma regra aprovada, e o operador vê o acréscimo separado da tarifa por hora.

**Regras de negócio:** nenhuma aprovada. Esta spec não define valores nem gatilhos; pela P6 aprovada, ticket perdido não tem multa até haver regra.

**Modelo:** `FeeAdjustment` no `Billing`: tipo, valor, motivo, autor, ligado ao ticket. Mantém o valor por hora intacto: `Total = Fee + Ajustes`.

**Contexto dono:** Billing.

**Eventos:** depende da regra. `TicketClosed` com o método de saída (C2) pode ser suficiente.

**Endpoints:** `POST /billing/tickets/{id}/adjustments`, Owner/Admin. Definição das regras: Owner.

**Critérios de aceite:**

1. O ajuste aparece separado do valor por hora, com tipo, motivo e autor.
2. O `FeeCalculator` continua sem mudanças.
3. Um ajuste não é aceito depois de um pagamento concluído sem um fluxo próprio definido.

**Pergunta aberta:** P16 (regras e valores, quando houver).

---

### C7: Área do ticket

**Problema:** o ticket deve incluir a área atribuída (`README:95`), mas o README não define áreas. A free zone agora está definida (N6) e, com R1, é uma cota, não uma área física. Hoje o ticket não tem esse dado.

**Escopo:** só após resposta a P17. Se houver zonas físicas: cadastro de área, área no ticket e ocupação por área. Se for vaga individual: spec própria, porque muda a operação.

**Fora de escopo:** mapa do pátio, reserva e orientação de rota.

**História:** como operador, atribuo a área na entrada e vejo a disponibilidade dela.

**Regras de negócio:** pendentes de P17. Não há regra de atribuição no README.

**Modelo:** `Area` (`Name`, `Capacity`, `Kind?`) em Occupancy; `AreaId?` em `Ticket` e no `TicketOpened`.

**Contexto dono:** Occupancy, para áreas; Access, para a atribuição.

**Eventos:** `TicketOpened` ganha `AreaId?`, de forma compatível.

**Endpoints:** `POST /occupancy/areas` (Owner), `GET /occupancy/areas/availability` (qualquer operador).

**Critérios de aceite:**

1. A soma das capacidades de área é consistente com a capacidade total.
2. Tickets sem área continuam válidos e contam no total.
3. A ocupação por área segue os critérios de idempotência de C1.

**Pergunta aberta:** P17.

## 5. Decisões de contexto

| Assunto | Recomendação | Motivo | Alternativa |
|---|---|---|---|
| **Occupancy** | Novo módulo, com Transaction Script | Previsto no `ADR:11,16,62` e no `README:117`; terá outra fonte, a IoT | Projeção dentro do Access: menos código, mas funde responsabilidades que os ADRs separam |
| **Banco de Occupancy** | MySQL, com novo ADR substituindo o `ADR:43` | 1000+ transações por dia (`README:52`) dão cerca de uma por minuto em média, sem pressão por NoSQL; a Onda 4 remove o Mongo (D1) e o MySQL já está na operação | MongoDB, se IoT trouxer volume alto. **Não assumido**: reavaliar com dados de telemetria real |
| **Subscription** | Novo módulo | Citado como contexto futuro no `README:117`; plano volátil (`README:46`) fica isolado do cálculo por hora | Dentro de Billing: menos eventos, mas mistura catálogo volátil com a base estável |
| **Contagem de franquia** (aprovada por delegação: R5) | Em Billing | É ele quem transforma tempo em valor, tem os tickets e cota antes da saída; recebe os parâmetros por evento | Em Subscription: junta plano e consumo, mas força Billing a consultar outro contexto em toda cotação |
| **Bloqueio de entrada por free zone** (aprovado por delegação: R6) | Access decide, com a própria contagem de tickets abertos por tipo e os limites recebidos por evento de Occupancy | Evita decidir sobre uma projeção com até 15 s de atraso, e a entrada é transação do core (N1). Ajustes manuais de Occupancy precisam entrar nesses limites | Consulta síncrona a Occupancy, com mais acoplamento; ou só alertar o operador, sem barrar, mas sem garantir a sobra de N6 |
| **Payment** | Dentro de Billing | `ADR:12` já coloca pagamentos em Billing; ainda não há razão para um novo contexto | Módulo próprio, se surgirem vários provedores ou regras de conciliação |
| **Gateway de pagamento** | Porta `IPaymentGateway` em Billing, adaptador só quando houver provedor escolhido | Pagamento manual em C3 não precisa dele; `README:55` prevê REST para sistemas externos | Sem alternativa antes da escolha do provedor |
| **Leitor e cancela** | Adaptadores no Access (`IGateController`), com simulador nos testes | Domínio sem hardware; o fornecedor é desconhecido | Serviço de borda próprio, se o equipamento exigir operação offline |
| **Sensor IoT** | Fora por agora. Se vier, adaptador de entrada em Occupancy | `ADR:62` associa Occupancy à IoT, mas o `README` não pede | Ver P3 |
| **Classificação de subdomínios** | Manter Access core e Billing genérico (`ADR:10,12`; N1 e N4). Na Onda 5, revisar só os padrões em `ADR:17-18` | O código usa agregados onde o ADR afirma Transaction Script e Active Record: refletir a implementação ou justificar a diferença | Sem alternativa: decisão do usuário |

### Liberação da saída (aprovada por delegação: R4)

**Risco:** o Access decide a saída, mas o pagamento é do Billing, sem referência entre módulos. O `OutboxProcessor` faz polling a cada 10 s (`OutboxProcessor.cs:17`), então uma liberação por evento pode segurar a cancela por até esse tempo, ou mais se houver retry. Quem acabou de pagar não deve ficar esperando.

| Opção | Como funciona | Prós | Contras |
|---|---|---|---|
| **A: porta síncrona (escolhida)** | Porta `IExitClearance` na Application do Access; o adaptador fica na API, que já é a composição dos módulos, e chama uma consulta só de leitura do Billing | Resposta na hora e sempre atual, sem referência entre módulos; simples em um monólito | Abre exceção a `README:55,203` e exige ADR; a extração futura de módulos transforma a chamada em rede |
| **B: evento com disparo imediato** | `TicketPaid` por outbox, com o processador acordado logo após o commit | Mantém só eventos; reduz o atraso típico | Continua assíncrono, sem garantia em caso de falha ou retry; o Access precisa manter uma projeção, e a validade da janela fica duplicada |
| **C: evento com polling atual** | `TicketPaid` no ciclo de 10 s | Nenhuma mudança na infraestrutura | Cancela presa por até 10 s ou mais; inadequada para saída automática |

**Em qualquer opção:** se a consulta falhar, a saída não abre sozinha e cai para o operador, com liberação manual (`README:36,53`).

**Atualização necessária do context map** (`README:234`):

- **Access → Billing:** `TicketOpened`, `TicketClosed` (publica eventos; Billing se conforma ao contrato). Com R4, também a consulta de liberação da saída, composta pela API.
- **Access → Occupancy:** `TicketOpened`, `TicketClosed`. Com R6, **Occupancy → Access:** limites de capacidade e cota.
- **Subscription → Billing e Access:** `Subscription*`.
- **Subscription ↔ Billing:** `SubscriptionChargeDue` e `SubscriptionChargePaid`.
- **Billing → provedor de pagamento:** camada anticorrupção via `IPaymentGateway`, quando existir.
- **Access → leitor e cancela:** camada anticorrupção via adaptadores.
- **Management:** identidade e autorização para todos (`README:109`), via JWT validado na API, não por chamada entre módulos. Sem seta de evento para `ClientRegistered` depois da Onda 4.
- **Remover ou marcar como futuro** o que não existir na data da atualização. Hoje o mapa cita elementos que o código não tem, conforme a auditoria.

Todos seguem sendo módulos do mesmo monólito (`README:123`), com novo contrato de evento no Shared Kernel, seguindo o padrão atual.

## 6. Dependências da spec de débito técnico

| Onda | Por que é necessária | Bloqueia |
|---|---|---|
| **Onda 0**, CI e base E2E | Critérios de aceite precisam de CI e testes reais; o aceite final da onda depende do primeiro push | Todas |
| **Onda 1**, segurança | Nenhuma capacidade nova deve ser exposta com a autorização atual | C1 a C7 |
| **Onda 2**, arquitetura | Novos módulos precisam entrar nas regras desde o início | Occupancy e Subscription |
| **Onda 3**, consistência | Índices únicos, resposta 409, dead-letter e testes reais de atomicidade antes de cobrar valores; o TD-305 é a base de EVO-0 | EVO-0, C1, C3, C4, C5, C6 |
| **Onda 4**, limpeza | D1 define o banco de Occupancy; serve de referência aos novos módulos, com portas no mesmo lugar | Occupancy e Subscription, de preferência |
| **Onda 5**, documentação | README, ADRs e context map precisam incluir cada capacidade à medida que ela entrar | Sem bloqueio; atualizar a cada entrega |

**C3 também precisa** que a Onda 1 tenha o autor vindo do token, porque a liberação manual e o pagamento são auditados por ele.

**C2 é a exceção:** depende só das Ondas 0 e 1, por isso pode ser a primeira capacidade e começar antes da Onda 3. Ainda assim, o índice do código do ticket deve seguir o padrão de conflito da Onda 3.

## 7. Perguntas

### Abertas

Não têm recomendação ou dependem de informação que só o negócio tem.

| # | Pergunta | Afeta |
|---|---|---|
| P15 | Qual tarifa vale para a diferença se a política mudar entre o pagamento e a saída? | C3 |
| P10 | Qual leitor e cancela serão usados? | C5 |
| P16 | Quais regras especiais e multas existem, e com quais valores? | C6 |
| P17 | O que é a "área" do ticket (`README:95`)? Há zonas físicas? | C7 |

### Fechadas

| # | Pergunta | Resposta | Origem |
|---|---|---|---|
| P1 | Mensalista paga por visita? | Valor fixo, sem desconto | Decisão do usuário (N2) |
| P2 | Como reservar vagas para mensalistas? | Cota de capacidade, sem vaga numerada | Decisão do usuário (N6, R1) |
| P4 | Paga antes ou depois da saída? | Antes, com janela configurável de 15 minutos | Decisão do usuário (N7, R2) |
| P11 | A franquia de exemplo vale por qual período? | 2 visitas por dia | Decisão do usuário (N8) |
| P12 | As 3h são por visita ou total? | Por visita | Decisão do usuário (N8) |
| P13 | Saldo não usado acumula? | Não | Registrado conforme a franquia diária (N8), aprovado por delegação |
| P14 | O excedente conta a partir de quando? | Tempo acima de 3h na visita; além da 2ª visita do dia, avulsa desde a entrada | Aprovada por delegação |
| P3 | A ocupação vem das cancelas ou de sensores? | Eventos de ticket com ajuste manual; sensores só se a divergência justificar | Aprovada por delegação |
| P5 | Quais meios de pagamento? | Registro manual e lista configurável; provedor depois | Aprovada por delegação |
| P6 | Como funciona ticket perdido? | Saída por placa, com motivo, sem multa até haver regra | Aprovada por delegação |
| P7 | Mensalista inadimplente ou vencido? | Cobrar como avulso, com tolerância no plano, e reativar ao pagar | Aprovada por delegação |
| P8 | Ticket antes da primeira tarifa? | Exigir tarifa antes de operar; sentinela só como proteção, e remover se nunca ocorrer em produção | Aprovada por delegação |
| P9 | Assinatura e RFID pertencem a quem? | Assinatura por Client, credencial por veículo, um veículo no pátio por assinatura | Aprovada por delegação |
| P10, parte | Em caso de queda, e o CPF? | Atendimento manual; restringir e mascarar CPF, com análise de LGPD antes de C5 | Aprovada por delegação |
