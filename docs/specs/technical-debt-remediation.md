# Spec: Correção do débito técnico

| | |
|---|---|
| **Status** | Aprovada |
| **Data** | 2026-11-03 |
| **Escopo** | Só débito técnico. A evolução do produto será discutida depois. A Onda 6 (privacidade) foi aprovada em 2026-11-04. |

## 1. Contexto e objetivos

A direção das dependências do NeoParking está boa: nenhum módulo referencia outro, e Domain e Application não usam EF, Mongo, ASP.NET nem BCrypt. Os problemas estão em outro lugar: a autorização pode ser burlada, não há CI, invariantes dependem de verificar antes de gravar, e o README descreve um sistema diferente do código.

**Objetivos**

1. Fazer a identidade e o papel do operador virem do token, nunca da requisição.
2. Garantir que todo PR passe por build, testes e regras de arquitetura de forma automática.
3. Garantir as invariantes de unicidade no banco, e não só na aplicação.
4. Impedir que mensagens com erro travem o outbox, sem perder eventos.
5. Remover código sem uso e alinhar README e ADRs ao código.

**O que não foi verificado em execução**

| Item | Situação |
|---|---|
| Provider MongoDB | Falha concluída por leitura de `AccessModule.cs:47-51` e `ClientService.cs:16-20`; não foi executado |
| 15 testes de integração | Não rodaram, porque não havia Docker no ambiente da auditoria |
| Testes sem integração | 107 aprovados e 0 falhas, com o filtro atual |

## 2. Fora de escopo

**Evolução do produto:** RFID, Subscriber, Occupancy, Payment, e a sentinela retroativa de tarifa como regra de negócio. O tratamento de documento e LGPD saiu desta lista e virou a Onda 6.

**Débito adiado, com gatilho para reconsiderar**

| Item | Motivo para adiar | Gatilho |
|---|---|---|
| Mover o outbox do Kernel para `Shared.Infrastructure` | Compartilhar o outbox no Shared Kernel é legítimo e evita três cópias | Extração de um módulo, ou Kernel mudando com frequência demais |
| Desacoplar `IDomainEvent` de `INotification` | Exige um wrapper de notificação e altera todos os handlers | Troca do MediatR ou adoção de broker externo |
| Domain events reais levantados pelos agregados | Os integration events criados nos serviços atendem aos fluxos atuais | Efeito colateral interno a um contexto que precise da mesma transação |
| Contratos em projetos `*.Contracts` | Os contratos no Kernel ainda são poucos | Extração de módulo ou versionamento independente de contratos |
| Rate limiting no login | Fora do que a auditoria encontrou | Exposição pública da API |
| Várias instâncias do `OutboxProcessor` | O deploy atual tem uma instância | Deploy com mais de uma instância |
| Projetos separados por camada | Os testes de arquitetura da Onda 2 cobrem a proteção necessária | Violações recorrentes, ou extração de módulo |

## 3. Decisões

Aprovadas em 2026-11-03, seguindo as recomendações. A onda indicada é a primeira que depende de cada decisão.

### D1: MongoDB (antes da Onda 4)

- **Decisão:** remover provider, driver e configuração, e registrar um ADR substituindo ADR-002 e ADR-004. O código fica no histórico do git, e Occupancy pode reavaliar a escolha.
- **Descartado:** consertar, o que exigiria outbox e `IUnitOfWork` com atomicidade no Mongo, `ITicketService` e testes.

### D2: Matriz de autorização (antes da Onda 1)

Matriz aprovada:

| Endpoint | Público | Qualquer operador | Owner/Admin | Owner |
|---|---|---|---|---|
| `POST /operators/login` | ✅ | | | |
| `POST /operators/owner` (ver D5) | ✅ | | | |
| `POST /operators` | | | ✅ (Admin cria só Operator) | |
| `GET /operators` (paginada) | | | ✅ | |
| `GET /operators/me`, `POST /operators/me/password` | | ✅ | | |
| `GET /operators/{id}` | | | ✅ | |
| `POST /operators/{targetId}/promote` e `/demote` | | | | ✅ |
| `POST /operators/{targetId}/activate` e `/deactivate` | | | ✅ (Admin só sobre Operator) | |
| `POST`, `GET`, `PATCH /clients`; `POST /clients/search`; `POST` e `DELETE` de veículos | | ✅ | | |
| `GET /clients/{id}/document-accesses` | | | | ✅ |
| `DELETE /clients/{id}` | | | ✅ | |
| `POST /access/tickets`, `POST .../exit`, `POST /access/tickets/open-by-plate`, `GET /access/tickets...` | | ✅ | | |
| `GET /billing/pricing-policies/active` | | ✅ | | |
| `GET /billing/pricing-policies/history` e `/effective-at` | | | ✅ | |
| `POST /billing/pricing-policies` | | | | ✅ |
| `GET /billing/tickets/fee-history` | | | ✅ | |
| `GET /billing/tickets/{id}` | | ✅ | | |

Três linhas foram aprovadas como sugestão, sem regra de negócio explícita, e podem ser revistas: `GET /operators/{id}`, `active` e `GET /billing/tickets/{id}`.

### D3: Revogação de acesso (antes da Onda 1)

- **Decisão:** aceitar até 60 minutos de validade do token após desativação ou rebaixamento nos endpoints fora de Management, e documentar em ADR. Em Management, o papel e o estado continuam vindo do banco.
- **Descartado:** checar `IsActive` em `OnTokenValidated`, com revogação imediata e uma consulta ao banco por requisição.

### D4: Unicidade de placa e documento (confirmada pelo usuário em 2026-11-04)

- **Decisão:** índice único global de placa em `vehicles` e de documento (CPF ou CNPJ) em `clients`. Na Onda 3 o documento era só o CPF; a Onda 6 o generaliza e move o índice para `document_hmac`.
- **Confirmado:** cada placa tem um único cliente responsável, e não há fluxo próprio de transferência de veículo. A placa é removida do cliente antigo e registrada no novo.
- **Responsável, não proprietário:** o registro identifica quem responde pelo veículo no estacionamento. A propriedade não é verificada.
- **Fora do modelo por ora:** compartilhamento de veículo e frota fora do modelo de empresa. Se houver demanda real, trocar o índice único de placa por uma tabela de vínculos.

### D5: Bootstrap do Owner (antes da Onda 1)

- **Decisão:** manter público, único por índice (Onda 3), e documentar o risco de quem chegar primeiro virar Owner.
- **Descartado por ora:** exigir um segredo de bootstrap por configuração. Adotar se a API ficar exposta antes da configuração inicial.

## 4. Ondas

Cada onda é entregue por um PR `feature/*` para `develop`. A ordem importa: a Onda 0 cria a rede usada pelas seguintes.

### Onda 0: Linha de base

**TD-001: Criar o CI**
- **Evidência:** não existe `.github`, embora `README.md:286` afirme que as regras rodam em CI/CD.
- **Requisito:** GitHub Actions em PRs para `develop` e `main`, com build, testes unitários, arquitetura e integração com Docker.
- **Aceite:**
  - Um PR com teste quebrado fica vermelho.
  - Os quatro grupos aparecem separados no resultado.

**TD-002: Fazer os testes de integração rodarem**
- **Evidência:** os 15 testes não foram executados na auditoria.
- **Requisito:** rodar os 15 no CI e corrigir o que falhar.
- **Aceite:** os 15 aparecem como executados e aprovados no CI.

**TD-003: Corrigir o filtro de testes**
- **Evidência:** o filtro `FullyQualifiedName!~Integration` (`README.md:504,636`) descarta o teste unitário `RegisterEntryAsync_ShouldEnqueueTicketOpenedIntegrationEvent` (`Unit/Application/TicketServiceTests.cs:48`).
- **Requisito:** marcar os testes com `[Trait("Category","Integration")]` e filtrar por `Category!=Integration`.
- **Aceite:**
  - O filtro executa esse teste.
  - O filtro não executa nenhum teste que use Testcontainers.

**TD-004: Resolver o aviso MSB3277**
- **Evidência:** conflito entre EF Core Relational 8.0.2 e 8.0.11 no build.
- **Requisito:** alinhar as versões.
- **Aceite:** `dotnet build` não emite MSB3277.

**TD-005: Criar a base de testes E2E**
- **Evidência:** não há `WebApplicationFactory`, e `public partial class Program` não existe.
- **Requisito:** fixture com `WebApplicationFactory` e Testcontainers MySQL, cobrindo os três bancos e aplicando as migrations reais.
- **Aceite:** um teste faz bootstrap do Owner, login e uma chamada autenticada via HTTP.

### Onda 1: Segurança

Comece pelos testes E2E, que devem **falhar antes da correção**.

**TD-101: Tirar o `requesterId` da rota**
- **Evidência:** `ManagementEndpoints.cs:33-38,54-57,63-66,73-76,82-85`; os endpoints não leem claims.
- **Requisito:** o solicitante vem do claim `sub`. Rotas novas: `POST /operators` e `POST /operators/{targetId}/promote|demote|activate|deactivate`.
- **Aceite:**
  - Um token de Operator recebe 403 nessas ações, mesmo conhecendo o GUID do Owner.
  - As rotas antigas deixam de existir.

**TD-102: Proteger a definição de tarifa**
- **Evidência:** `BillingEndpoints.cs:12-18` só exige login; `PricingPolicyService.cs:65` usa `request.OwnerId`.
- **Requisito:** aplicar a policy Owner e obter o `OwnerId` do claim, removendo-o do body.
- **Aceite:**
  - Operator e Admin recebem 403.
  - A política gravada tem o `OwnerId` do token, mesmo que o body traga outro valor.

**TD-103: Aplicar policies conforme D2**
- **Evidência:** `AuthenticationExtensions.cs:40` chama `AddAuthorization()` sem policies, e os endpoints usam `RequireAuthorization()` sem papel.
- **Requisito:** aplicar a matriz aprovada em D2.
- **Aceite:** testes E2E cobrem cada célula da matriz: sem token 401, papel insuficiente 403, papel suficiente com sucesso.

**TD-104: Rejeitar solicitante inativo**
- **Evidência:** `OperatorService.cs:164-166`, em que `GetExistingAsync` não checa `IsActive`.
- **Requisito:** um solicitante inativo não executa ações em Management.
- **Aceite:** um operador desativado com token ainda válido recebe 403 ao chamar uma ação de Management.

**TD-105: Corrigir os códigos HTTP de autenticação e autorização**
- **Evidência:** `ExceptionHandlingExtensions.cs:26` transforma `DomainException` em 422, inclusive em login inválido e falta de permissão.
- **Requisito:** novas exceções no Kernel, mapeadas para 401 (credenciais inválidas) e 403 (sem permissão).
- **Aceite:**
  - Credencial inválida retorna 401.
  - Falta de permissão retorna 403.
  - Regra de negócio continua retornando 422.

**TD-106: Não revelar o estado da conta no login**
- **Evidência:** `OperatorService.cs:31-32` responde "Account is inactive" antes de verificar a senha.
- **Requisito:** a mesma resposta genérica para e-mail inexistente, senha errada e conta inativa.
- **Aceite:** os três casos retornam 401 com corpo idêntico.

**TD-107: Tornar a ativação simétrica à desativação**
- **Evidência:** `OperatorService.cs:131` restringe Admin em `DeactivateAsync`, mas `ActivateAsync` (`:143`) não restringe.
- **Requisito:** Admin não ativa Admin nem Owner.
- **Aceite:** Admin recebe 403 ao ativar outro Admin, e continua podendo ativar um Operator.

**TD-108: Validar o segredo JWT no boot**
- **Evidência:** `AuthenticationExtensions.cs:22-23` só verifica se o valor é nulo.
- **Requisito:** fora de Development, o boot falha se `Jwt:Secret` for placeholder ou tiver menos de 32 bytes.
- **Aceite:** testes cobrem placeholder e segredo curto (falha) e segredo válido (sobe), sem expor o valor no erro.

**TD-109: Remover segredos versionados**
- **Evidência:** `appsettings.json:4,7,10` e os defaults de `docker-compose.yml:8,51`.
- **Requisito:**
  - Remover esses valores e exigir configuração por ambiente, com um `.env.example` sem valores reais.
  - Rotacionar qualquer valor que tenha sido usado de verdade, porque eles continuam no histórico do git.
- **Aceite:**
  - O compose falha com mensagem clara se faltar uma variável obrigatória.
  - Nenhuma senha nem segredo permanece nesses arquivos.

**TD-110: Reduzir privilégios do banco (item menor)**
- **Evidência:** as conexões do compose usam `Uid=root`, e a porta 3306 é publicada por padrão.
- **Requisito:** usar um usuário próprio com acesso apenas aos schemas do projeto, e não expor a 3306 por padrão.
- **Aceite:**
  - A API sobe e aplica as migrations com esse usuário.
  - A 3306 fica inacessível do host na configuração padrão.

**TD-111: Não expor o hash de senha**
- **Evidência:** `PasswordHash.cs:27`, em que `ToString()` retorna o hash.
- **Requisito:** `ToString()` não retorna o valor.
- **Aceite:** teste unitário confirma que o resultado não contém o hash.

### Onda 2: Rede de arquitetura

**TD-201: Parametrizar os testes por contexto**
- **Evidência:** `DomainArchitecture.cs:9-10` só carrega Access e Management.
- **Requisito:** aplicar as mesmas regras a Access, Management e Billing.
- **Aceite:** introduzir um `using` de EF no Domain de Billing faz o teste falhar.

**TD-202: Adicionar as regras que faltam**
- **Evidência:** não há testes para `README.md:293-294`, nem para MediatR e ASP.NET.
- **Requisito:**
  - Proibir referência entre contextos.
  - Proibir MediatR no Domain.
  - Proibir ASP.NET em Domain e Application.
  - Exigir que cada repositório concreto implemente sua porta.
- **Aceite:** cada regra tem uma violação de prova, feita em um teste temporário ou fixture, que a faz falhar.

**TD-203: Melhorar as mensagens de falha**
- **Evidência:** os testes só verificam `IsSuccessful`.
- **Requisito:** o assert lista os tipos que violam a regra.
- **Aceite:** a mensagem de falha contém o nome completo do tipo violador.

**TD-204: Tornar as exceções explícitas**
- **Evidência:** `PhoneNumber.cs:3` usa libphonenumber no Domain; `TicketClosedHandler.cs:3`, `TicketOpenedHandler.cs:3` e `ClientRegisteredHandler.cs:3` usam MediatR na Application.
- **Requisito:**
  - Declarar libphonenumber como exceção documentada no Domain.
  - Declarar como permitidos os handlers MediatR na Application.
- **Aceite:** outra dependência externa no Domain faz o teste falhar, e o teste contém a justificativa das duas exceções.

### Onda 3: Consistência

**TD-301: Garantir unicidade no banco**
- **Evidência:** `OperatorService.cs:47`, `Access/Application/TicketService.cs:38`, `PricingPolicyService.cs:54` e `Client.cs:52`; os índices em `AppDbContext.cs:102` e `BillingDbContext.cs:59-62` não são únicos.
- **Requisito:**
  - Um único Owner.
  - Um único ticket aberto por placa.
  - Uma única política ativa.
  - Placa e CPF conforme D4.
  - Como o MySQL não tem índice parcial, usar coluna gerada ou índice funcional. `Role` e `Status` são gravados como string.
  - O índice é a garantia; a verificação prévia fica só para dar uma mensagem amigável.
- **Aceite:** um teste com MySQL real dispara duas operações concorrentes para cada invariante, e só uma é gravada.

**TD-302: Tratar duplicatas existentes**
- **Evidência:** dados atuais podem violar os novos índices, porque hoje nada impede duplicatas.
- **Requisito:** a migration detecta duplicatas e falha com mensagem clara, **sem apagar dados**.
- **Aceite:** com duplicatas semeadas, a migration falha, informa o que corrigir e mantém os dados intactos.

**TD-303: Traduzir conflitos para HTTP 409**
- **Evidência:** a violação de índice viraria 500 em `ExceptionHandlingExtensions.cs:27`.
- **Requisito:** os `UnitOfWork` traduzem o erro MySQL 1062 para `ConflictException`, mapeada para 409.
- **Aceite:** a perda de uma corrida retorna 409, sem detalhes do banco na resposta.

**TD-304: Outbox com backoff e dead-letter**
- **Evidência:** `OutboxProcessor.cs:83` repete para sempre, e `OutboxRepository.cs:24-29` busca as 50 mais antigas sem filtro de tentativa, então mensagens com erro podem ocupar o lote inteiro.
- **Requisito:**
  - Adicionar `AttemptCount`, `LastError`, `NextAttemptAt` e `DeadLetteredAt`.
  - Aplicar backoff exponencial com limite.
  - Fazer `GetPendingAsync` respeitar `NextAttemptAt` e ignorar mensagens em dead-letter.
  - Documentar como fazer replay.
- **Aceite:**
  - Com 50 mensagens em erro e uma válida, a válida é publicada no ciclo seguinte.
  - Uma mensagem esgotada recebe `DeadLetteredAt` e `LastError`.
  - O replay documentado a publica de novo.

> ⚠️ **Armadilha: ticket fechado antes da primeira política**
>
> A sentinela de `PricingPolicyService.cs:40-48` conta com o retry de `TicketClosed` até o Owner definir a primeira política. Um dead-letter ingênuo mandaria esses eventos para o descarte e **a cobrança nunca seria calculada**.

**TD-305: Preservar a cobrança sem política definida**
- **Requisito:** escolher um limite de tentativas que cubra esse intervalo, ou reprocessar essas mensagens ao definir a primeira política.
- **Aceite:** um teste com MySQL real fecha um ticket sem política, esgota as tentativas configuradas, define a primeira política e confirma que a `Fee` é calculada e congelada, com o replay documentado se o desenho usar replay.

**TD-306: Provar a atomicidade**
- **Evidência:** `Integration/Service/ClientServiceTests.cs:19,58` usa InMemory e só conta linhas após um sucesso.
- **Requisito:** simular falha no `SaveChanges` com MySQL real.
- **Aceite:** após a falha, nem o agregado nem a mensagem de outbox existem.

**TD-307: Migrar os testes InMemory**
- **Evidência:** `AccessOutboxRepositoryTests.cs:19` e `Integration/Service/ClientServiceTests.cs:19`.
- **Requisito:** migrar os 8 testes para Testcontainers.
- **Aceite:** nenhum teste de integração referencia o provider InMemory.

**TD-308: Cobrir o que está sem teste**
- **Evidência:** `OutboxProcessor` não tem teste, e Billing e Management não têm testes de integração.
- **Requisito:** cobrir `OutboxProcessor` (sucesso, falha e isolamento entre mensagens) e os repositórios, UoW e migrations de Billing e Management.
- **Aceite:**
  - O fluxo de entrada e saída, da publicação no Access até a `Fee` no Billing, passa com MySQL real.
  - Uma reentrega da mesma mensagem não duplica efeito.

**TD-309: Rejeitar fator negativo em `Money`**
- **Evidência:** `Money.cs:28`, em que `operator *` usa o construtor sem validação.
- **Requisito:** um fator negativo é rejeitado.
- **Aceite:** `Money * -1` lança `DomainException`.

### Onda 4: Limpeza mínima

**TD-401: Aplicar D1**
- **Evidência:** `AccessModule.cs:47-51` e `Infraestructure/mongo/`.
- **Requisito:** remover provider, driver, `DatabaseProvider` e o `MongoDB.Driver` citado nos testes de arquitetura, e registrar o ADR.
- **Aceite:** o build passa sem o pacote `MongoDB.Driver`, e a API sobe com a configuração documentada.

**TD-402: Remover o handler sem efeito**
- **Evidência:** `ClientRegisteredHandler.cs:20-26` só registra log e tem um TODO.
- **Requisito:** remover o handler e `ClientRegisteredHandlerTests.cs`. Os eventos sem consumidor continuam como contrato.
- **Aceite:** build e testes passam, e o Access continua gravando `ClientRegisteredIntegrationEvent`.

**TD-403: Remover o outbox de Management**
- **Evidência:** `ManagementModule.cs:30-39` e a interface; nenhum serviço grava nele.
- **Requisito:** remover interface, repositório, registro e processor. Criar a migration que apaga `management_outbox_messages` **só depois de confirmar que a tabela está vazia**.
- **Aceite:**
  - O processor de Management não é mais iniciado.
  - A migration falha, sem apagar nada, se a tabela tiver linhas.

**TD-404: Mover as portas de repositório do Access**
- **Evidência:** `Access/Domain/IClientRepository.cs` e `ITicketRepository.cs`, enquanto Billing e Management as mantêm em Application.
- **Requisito:** mover para Application.
- **Aceite:** os três contextos usam a mesma convenção, e existe um teste de arquitetura que a verifica.

**TD-405: Não devolver o agregado no login**
- **Evidência:** `OperatorService.cs:26` e `JwtTokenGenerator.cs:8,12`.
- **Requisito:** `LoginAsync` retorna um DTO, e a Api não usa mais `OperatorUser`.
- **Aceite:** não há referência a `NeoParking.Management.Domain` em `NeoParking.Api`, e os claims do token não mudam.

**TD-406: Renomear `Infraestructure`**
- **Evidência:** `src/Modules/Access/Infraestructure/`.
- **Requisito:** renomear para `Infrastructure`, em um commit só de renomeação.
- **Aceite:** build e testes passam, e o namespace, que já está correto, não muda.

**TD-407: Remover `using` sem uso**
- **Evidência:** `ManagementEndpoints.cs:4`.
- **Requisito:** remover.
- **Aceite:** `NeoParking.Api` não importa mais `Management.Infrastructure` nesse arquivo.

**TD-408: Exigir `Billing:ConnectionString`**
- **Evidência:** `BillingModule.cs:16-18` cai no banco de Access quando a chave falta.
- **Requisito:** remover o fallback.
- **Aceite:** sem a chave, o boot falha com mensagem clara, e o README e o `appsettings` de exemplo trazem a chave.

### Onda 5: Documentação

**TD-501: Alinhar o README ao código**
- **Evidência:** `README.md:103-117,140,151-199,211-212,244-246,256-265,350,367-385,418,497-498`.
- **Requisito:**
  - Incluir Billing, os quatro eventos, todos os endpoints e a matriz de D2.
  - Corrigir portas (5114 no `dotnet run`, 8080 no Docker), schemas e configuração.
  - Remover `IEventDispatcher`, `MediatREventDispatcher` e `Endpoints.cs`.
  - Declarar o Shared Kernel como: "contém primitivas, contratos e outbox compartilhado; depende de EF Core e MediatR".
  - Corrigir o `a` solto (`:309`) e o comentário duplicado (`:319-320`).
- **Aceite:** seguindo o README do zero, uma pessoa sobe o ambiente, faz o bootstrap e chama um endpoint protegido.

**TD-502: Descrever os testes como são**
- **Evidência:** `README.md:471-488`.
- **Requisito:** mostrar a pirâmide real, com `.http` descrito como teste manual e os E2E automatizados como os da Onda 1.
- **Aceite:** as camadas e os comandos do README batem com o resultado do CI.

**TD-503: Decidir o Git Flow**
- **Evidência:** `README.md:546-560,628` manda fazer squash, mas o histórico mostra merge commits de PR e commits diretos no `develop`.
- **Requisito:** escolher entre squash e o que o histórico mostra, e configurar a proteção de branch de acordo.
- **Aceite:** o README descreve a regra escolhida, e as configurações do GitHub a aplicam.

**TD-504: Atualizar ADRs e context map**
- **Evidência:** ADR-001 afirma Transaction Script para Billing e Active Record para Management, mas o código usa agregados nos dois; `README:48` e `ADR:23` falam em desconto para mensalistas, contra a decisão do usuário; ADR-002 e ADR-004 tratam do Mongo; os ADRs têm data de 2024-11-30, anterior ao primeiro commit (2025-08-13); o context map traz Occupancy e Payments, que não existem.
- **Requisito:**
  - Manter a classificação do ADR-001: Access é core e Billing é genérico (decisão do usuário em 2026-11-03).
  - No ADR-001, alterar só os padrões: refletir a implementação atual de Billing e Management, ou justificar a diferença.
  - Remover de `README:48` e `ADR:23` o desconto para mensalistas: o mensalista paga valor fixo por assinatura.
  - Corrigir as datas.
  - Registrar os ADRs de D1 e D3.
  - Refazer o context map com relações reais, marcando o que é futuro.
- **Aceite:** cada contexto e seta do mapa corresponde a um módulo ou evento do código, ou está marcado como futuro.

### Onda 6: Privacidade

Aprovada pelo usuário em 2026-11-04. Vem antes de EVO-0, C1, C4 e C5, porque esses itens dependem do cliente, e mudar o documento depois sairia mais caro. Os itens seguem esta ordem.

**TD-601: Tirar documento e placa de logs e erros**
- **Evidência:** `Plate.cs:23`, `Client.cs:53` e `TicketService.cs:40` repetem a placa em mensagens que chegam ao log e à resposta; `DbContextConflictExtensions.cs` mantém o erro MySQL 1062, que traz o valor duplicado, como exceção interna; `RequestLoggingMiddleware.cs` registra o path, que contém a placa em `/access/tickets/by-plate/{plate}/exit`; `AddAspNetCoreInstrumentation` grava o mesmo path em `url.path`.
- **Requisito:** documento e placa não aparecem em logs, mensagens de exceção, respostas de erro nem no texto do erro 1062. Logs e tracing registram o padrão da rota, não o valor.
- **Aceite:** um teste E2E captura os logs reais da API e confirma que placa e documento não aparecem depois de formato inválido, conflito de índice e saída por placa; outro confirma o `url.path` do tracing.

**TD-602: Cliente com tipo e documento**
- **Evidência:** `Client.cs` só aceita `Cpf`, e `clients.cpf` tem 11 caracteres; empresas ficam de fora.
- **Requisito:**
  - `Type` (`Individual`, `Company`) e `Document` (CPF ou CNPJ, validado conforme o tipo). A API troca `cpf` por `type` e `document`.
  - CNPJ alfanumérico: coluna de texto e validação pelo algoritmo oficial, com valor do caractere igual a ASCII − 48, sem assumir só dígitos.
  - Clientes existentes migram como `Individual`. Avulso continua sem documento. Estrangeiro fica fora do escopo por agora.
- **Aceite:** testes cobrem CPF válido e inválido, CNPJ numérico e alfanumérico válido e inválido, e casos conhecidos da especificação; a migration mantém os clientes existentes como `Individual`.

**TD-603: Mascarar o documento nas respostas**
- **Evidência:** `ClientService.ToResponse` devolve o CPF completo a qualquer operador.
- **Requisito:** todas as respostas trazem o documento mascarado. O valor completo só sai por rota própria, para Owner e Admin, com registro persistido de autor (`sub`), cliente e data, sem o valor do documento.
- **Aceite:** testes confirmam a máscara em criação, consulta, listagem e atualização; 403 para Operator na rota completa; um registro de auditoria por leitura, sem o valor.

**TD-604: Criptografar o documento**
- **Evidência:** o documento fica legível no banco em `clients`.
- **Requisito:**
  - `document_encrypted` com AES-256-GCM, nonce aleatório e versão da chave no valor.
  - `document_hmac` com HMAC-SHA256 e chave própria, incluindo o tipo no cálculo. O índice único passa para essa coluna.
  - CPF e CNPJ recebem a mesma proteção.
  - As chaves vêm do ambiente, e o boot falha com mensagem clara, sem expor o valor, se uma delas faltar ou tiver tamanho errado.
- **Aceite:** um teste confirma que o documento não fica legível em nenhuma coluna; duplicata continua em 409; boot sem chave ou com chave inválida falha.

**TD-605: Migrar os documentos existentes sem perda**
- **Evidência:** MySQL não tem AES-GCM, e a chave não pode entrar no SQL; a coluna em texto puro não pode sair antes do preenchimento.
- **Requisito:** expandir, preencher e só então remover, de forma idempotente e retomável. A coluna em texto puro só sai depois do preenchimento concluído e verificado. Uma falha no meio não perde dados e tem mensagem clara.
- **Aceite:** com o usuário restrito, testes cobrem o banco do zero, dados da versão anterior e falha seguida de nova tentativa.

**Decisões tomadas na implementação:** registradas no ADR-007 e no README: a migração roda no boot da API, a reversão com clientes é recusada e a rotação de chave não foi implementada.

**TD-606: Retenção e anonimização de inativos (pendente de definição jurídica)**
- **Evidência:** nada define por quanto tempo guardar dados de clientes inativos.
- **Requisito:** definir prazo e base legal com o jurídico; só então implementar a anonimização. **Não implementar antes disso.**
- **Aceite:** a definir com a regra.

### Onda 7: Prontidão

Aprovada pelo usuário em 2026-11-04. Reúne os pontos soltos encontrados na auditoria do README e o que falta no código antes de operar. Os itens de processo e ambiente não entram aqui: estão em [Fora da conta do código](#fora-da-conta-do-código).

**Já concluídos:**

**TD-701: Dependências sob vigilância (PR #20)**
- **Evidência:** `README:369-370` prometia atualização mensal e correção de CVE alta ou crítica, sem nada que aplicasse isso.
- **Requisito:** Dependabot mensal para NuGet e GitHub Actions; o CI falha em severidade alta ou crítica de produção.
- **Aceite:** `scripts/ci/check-vulnerable-packages.sh` testado com 8 casos e executado no CI.

**TD-702: Exigir `Management:ConnectionString` (PR #20)**
- **Evidência:** `ManagementModule.cs` caía na connection string do Access, o mesmo problema do TD-408.
- **Requisito:** sem a chave, o boot falha com mensagem clara.
- **Aceite:** `ManagementModuleTests`.

**TD-703: Rota de saúde (PR #20)**
- **Evidência:** não havia rota de saúde na API, só o healthcheck do MySQL no compose.
- **Requisito:** `GET`/`HEAD /health` anônimo, só com o status, 200 ou 503, checando os três bancos. Não mede disponibilidade.
- **Aceite:** `HealthEndpointTests` e `DatabaseHealthCheckTests`, com banco no ar e fora do ar.

**TD-704: Scripts em LF (PR #22)**
- **Evidência:** com `core.autocrlf=true`, `entrypoint.sh` ficava em CRLF e a API não subia.
- **Requisito:** `.gitattributes` com `*.sh text eol=lf`.
- **Aceite:** clone novo no Windows e compose real sem conversão manual: `/health` 200 e nenhum reinício.

**TD-705: Atualizar `MySqlConnector` (PR #22)**
- **Evidência:** a versão 2.3.5 tinha o aviso GHSA-473q-m89c-ghf8.
- **Requisito:** versão 2.6.1 em todos os projetos.
- **Aceite:** aviso removido e testes em MySQL real com Pomelo 8.0.2.

**TD-706: Provar que o CI fica vermelho (PR #21)**
- **Evidência:** faltava o último aceite do TD-001.
- **Requisito:** um teste quebrado deixa o check `ci` vermelho.
- **Aceite:** executado no PR #21, fechado sem merge. O check só bloqueia o merge com proteção de branch, que depende do TD-503.

**TD-707: Corrida ao definir tarifa (PR #23)**
- **Evidência:** `PricingPolicyService` lia o relógio antes da política ativa, e uma revisão concorrente mais nova resultava em 422 intermitente.
- **Requisito:** a política ativa é sempre encerrada em um momento válido.
- **Aceite:** teste determinístico, visto falhando antes da correção, e 12 rodadas seguidas de E2E sem falha.

**A fazer agora:**

**TD-708: Tempo de login igual para e-mail inexistente**
- **Evidência:** `OperatorService.cs:30` lança a exceção sem executar o BCrypt, então o tempo de resposta pode revelar se o e-mail existe.
- **Requisito:** e-mail inexistente também executa o BCrypt, contra um hash fictício fixo com o mesmo custo dos hashes reais. Os corpos de resposta continuam idênticos.
- **Aceite:** teste confirma a execução desse caminho, sem medir tempo.

**TD-709: Falha de login sem stack trace e conflito sem e-mail**
- **Evidência:** `UseExceptionHandler` registra `fail` com stack trace em toda falha de login; `OperatorService.cs:194` repete o e-mail na mensagem de conflito.
- **Requisito:** falha de login esperada não gera `fail` nem stack trace, e a mensagem de conflito não traz o e-mail. Os demais erros continuam com log completo.
- **Aceite:** teste com os logs reais da API cobre login recusado, conflito de e-mail e um erro inesperado.

**TD-710: Criptografar nome e telefone do cliente**
- **Evidência:** `clients.Name` e `clients.phone_number` ficam legíveis, e `ClientRegisteredIntegrationEvent` grava o nome no outbox, embora não tenha consumidor.
- **Requisito:**
  - Nome e telefone cifrados com a mesma chave do documento, sem índice cego, porque nada busca por esses campos. As respostas continuam trazendo os dois.
  - O nome sai do evento e das mensagens já gravadas no outbox.
  - Migração como no TD-605: expandir, preencher, verificar e só então remover o texto puro, de forma retomável e sem perda em caso de falha.
- **Aceite:** com o usuário restrito, testes cobrem o banco do zero, dados da versão anterior e falha seguida de nova tentativa; nenhuma coluna guarda o texto, e o compose real confirma isso no banco.

**TD-711: Rotação da chave de criptografia**
- **Evidência:** só existe a versão `v1`, e não há como trocar a chave sem perder acesso aos dados.
- **Requisito:** leitura pela versão gravada no valor, gravação pela versão atual e um processo documentado e retomável para recifrar. A troca de `DOCUMENT_HMAC_KEY` fica documentada como limitação.
- **Aceite:** teste com chaves geradas na execução cobre rotação, interrupção seguida de nova tentativa, chave antiga ausente e unicidade preservada.

**TD-712: Avisos altos em dependências de teste**
- **Evidência:** o CI avisa sobre SSH.NET 2023.0.0, System.Net.Http 4.3.0 e System.Text.RegularExpressions 4.3.0.
- **Requisito:** menor atualização que remova os avisos, mantendo os testes em MySQL real. O que não tiver correção é relatado.
- **Aceite:** `dotnet list package --vulnerable --include-transitive` sem esses avisos e testes de integração aprovados.

**TD-713: `NeoParking.Api.http` com autenticação**
- **Evidência:** as requisições protegidas não enviam token e retornam 401.
- **Requisito:** fluxo de login, com token nas requisições protegidas, conferido contra as rotas e a API atual de `/clients`.
- **Aceite:** cada requisição corresponde a uma rota real, e as protegidas enviam o token.

**TD-714: Medir o `ServerVersion.AutoDetect`**
- **Evidência:** `AutoDetect` está no lambda de `AddDbContext` dos três módulos e parece abrir uma conexão a cada escopo. Com o banco fora do ar, resolver o `DbContext` pode lançar exceção.
- **Requisito:** medir com contagem real. Se confirmado, detectar uma única vez, sem guardar falhas. Se não, registrar o resultado e não mudar.
- **Aceite:** teste com a contagem de conexões do MySQL e teste do comportamento com o banco fora do ar.

**TD-715: Rever a promessa de REST externo**
- **Evidência:** `README:55` fala em REST para sistemas externos, mas não há consumidor externo definido.
- **Requisito:** reescrever como premissa de estilo, sem prometer integração.
- **Aceite:** o README descreve a API como é.

**Depois, sem data:**

**TD-716: Teste de carga repetível**
- **Evidência:** `README:52` fala em 1000+ transações por dia sem nenhuma medição.
- **Requisito:** teste repetível de entrada e saída de tickets que gere uma linha de base. **Não implementar agora.**
- **Aceite:** a definir quando entrar.

**Decisão do usuário: placa sem criptografia.** A placa é dado operacional, usado em busca, índice e eventos. A proteção vem de logs limpos (TD-601), controle de acesso (D2) e retenção (TD-606).

#### Fora da conta do código

Processo e ambiente não entram no percentual:

- TD-503: método de merge e proteção de branch.
- Ambiente de staging.
- Deploy.
- Promoção de `develop` para `main`.

#### Progresso

Regra: cada item implementado e testado conta como concluído, e todos valem o mesmo. Por isso, o número mostra a quantidade de itens, não o esforço: C3 e C4 pesam muito mais que um item de débito técnico.

| Frente | Concluídos | Total | % | O que falta |
|---|---:|---:|---:|---|
| Produto (`product-evolution.md`) | 1 | 8 | 12,5% | EVO-0, C1, C3, C4, C5, C6, C7 |
| Débito técnico (Ondas 0 a 8, sem TD-503) | 58 | 68 | 85,3% | TD-606, TD-708 a TD-716 |
| Observabilidade (`observability.md`) | 0 | 21 | 0% | OBS-001 a OBS-603 |
| **Total** | **59** | **97** | **60,8%** | |

Cálculo: (1 + 58 + 0) / (8 + 68 + 21) = 59 / 97. Débito técnico: Onda 0 (5), Onda 1 (11), Onda 2 (4), Onda 3 (9), Onda 4 (8), Onda 5 (3, sem TD-503), Onda 6 (6), Onda 7 (16) e Onda 8 (6).

**Aguardando merge:** a Onda 6 e o TD-707 estão no PR #23, e a Onda 8 está na branch `feature/wave-8-routes`, que depende dele. A spec `observability.md` está na branch `docs/observability-spec`, com status "Proposta".

### Onda 8: Rotas para a view

Aprovada pelo usuário em 2026-11-04. Reúne as rotas que as telas do wireframe pedem e a API não tem. Depende da Onda 6 (PR #23), porque usa o documento cifrado e a auditoria.

**TD-801: Listar operadores**
- **Evidência:** só existe `GET /operators/{id}`, e nenhuma tela chega a um id sem uma lista.
- **Requisito:** `GET /operators?page=&pageSize=`, para Owner e Admin, com papel e estado vindos do banco (D3). A resposta traz id, nome, e-mail, papel e estado, nunca o hash.
- **Aceite:** testes cobrem paginação, limite de página, matriz D2 e ausência do hash no corpo.

**TD-802: Paginação e busca de clientes**
- **Evidência:** `GET /clients` devolve a lista inteira, sem filtro.
- **Requisito:**
  - `GET /clients?page=&pageSize=` responde com o envelope paginado, no máximo 100 itens por página. A ordem é por id, estável mas sem significado, porque nome e telefone serão cifrados (TD-710).
  - `POST /clients/search` recebe no corpo a placa exata ou o tipo e o documento exato, usando o HMAC já existente, e devolve o cliente ou 404. Placa e documento não entram na URL, em logs nem em traces.
- **Aceite:** testes cobrem as duas buscas, os critérios inválidos, a máscara do documento e a ausência dos valores nos logs.

**Decisão do usuário: sem busca de clientes por nome.** Ela conflita com o TD-710, que cifra o nome. Fica de fora por enquanto, sem índice cego novo, e será revista quando a tela for usada de verdade.

**TD-803: Conferir o ticket aberto sem placa na URL**
- **Evidência:** `GET /access/tickets?plate=` leva a placa na query.
- **Requisito:** `POST /access/tickets/open-by-plate` recebe a placa no corpo e devolve o ticket aberto ou 404. `GET /access/tickets?plate=` e `POST /access/tickets/by-plate/{plate}/exit` não mudam nesta onda.
- **Aceite:** testes cobrem ticket aberto, ticket fechado, placa inválida e ausência da placa em logs e traces.

**TD-804: Dados do próprio operador**
- **Evidência:** a navegação precisaria ler o papel direto do JWT.
- **Requisito:** `GET /operators/me`, para qualquer operador ativo, com id vindo do `sub` e papel vindo do banco (D3).
- **Aceite:** testes cobrem cada papel, papel alterado depois da emissão do token e conta inativa.

**TD-805: Consultar a auditoria de acesso ao documento**
- **Evidência:** `client_document_accesses` só é gravada.
- **Requisito:** `GET /clients/{id}/document-accesses?page=&pageSize=`, só para o Owner: o Admin também lê documentos e não deve auditar a si mesmo. A resposta traz operador e horário, nunca o documento. Como a auditoria sobrevive à exclusão do cliente, a rota também responde depois dela.
- **Aceite:** testes cobrem a matriz D2, a ordem, a paginação, a ausência do documento e o cliente excluído.

**TD-806: Trocar a própria senha**
- **Evidência:** `UpdatePasswordRequest` e `OperatorUser.UpdatePassword` existem sem rota.
- **Requisito:** `POST /operators/me/password` exige a senha atual e usa `UpdatePasswordRequest`. Senha atual errada ou senha nova vazia respondem 422 sem lançar exceção, para não gerar stack trace nem levar o e-mail ao log. Esta onda não cria revogação de token nem política de senha.
- **Aceite:** testes cobrem senha trocada, senha atual errada, logs reais e o comportamento do token anterior, que continua válido até expirar (D3).

## 5. Estratégia de testes e Definition of Done

| Camada | O que cobre | Ferramenta |
|---|---|---|
| Unitários | Domain e Application, com mocks só nas portas | xUnit, Moq, FluentAssertions |
| Integração | Repositórios, UoW, migrations, índices, outbox e atomicidade | Testcontainers MySQL |
| Arquitetura | Camadas e fronteiras dos três contextos | NetArchTest |
| E2E | HTTP, JWT, policies e códigos de resposta | `WebApplicationFactory` com Testcontainers |

InMemory não é aceito como teste de integração, porque não valida SQL, restrições nem transações. Os `.http` continuam só como apoio manual.

**Uma onda está pronta quando:**

- [ ] Os critérios de aceite de todos os itens têm teste automatizado, quando forem testáveis por código.
- [ ] O CI está verde, com todos os grupos de teste (a partir da Onda 0).
- [ ] Testes de correção de bug ou segurança foram vistos falhando antes da correção.
- [ ] Migrations rodam do zero e sobre um banco com dados da versão anterior.
- [ ] README e ADRs mudam no mesmo PR quando um contrato, uma rota ou uma configuração muda.
- [ ] Nenhum segredo real foi adicionado ao repositório.

## 6. Mudanças incompatíveis e migração

| Onda | Mudança | Quem é afetado | Migração |
|---|---|---|---|
| 1 | Rotas de `/operators` sem `requesterId` | Clientes da API e `.http` | Atualizar as chamadas; sem período de compatibilidade, porque as rotas antigas são a falha |
| 1 | `OwnerId` sai do body da tarifa | Clientes da API | Remover o campo |
| 1 | Login e permissões passam de 422 para 401 e 403 | Clientes que tratam 422 | Atualizar o tratamento de erro |
| 1 | Novas restrições de papel (D2) | Operadores | Comunicar a matriz antes do deploy |
| 1 | Boot exige segredos válidos, e `appsettings.json` deixa de trazê-los | Ambientes e desenvolvedores | Definir as variáveis e rotacionar os valores usados antes do deploy |
| 3 | Novos índices únicos | Dados existentes | Rodar a detecção, corrigir duplicatas manualmente e só então migrar |
| 3 | Novas colunas no outbox | Mensagens pendentes | Valores padrão que mantêm as pendentes elegíveis para envio |
| 3 | Conflitos passam a retornar 409 | Clientes da API | Tratar o novo código |
| 4 | Remoção do provider Mongo (D1) | Quem usa `DatabaseProvider: MongoDB` | Nenhum caminho automático; hoje esse modo parece nem funcionar |
| 4 | `Billing:ConnectionString` obrigatório | Ambientes sem a chave | Adicionar a chave; confirmar antes se Billing grava no banco de Access nesse ambiente, pois os dados precisariam ser movidos |
| 6 | API de clientes troca `cpf` por `type` e `document` | Clientes da API e `.http` | Atualizar as chamadas; sem período de compatibilidade |
| 6 | Respostas passam a trazer o documento mascarado | Quem lê o CPF da resposta | Usar a rota própria, para Owner e Admin |
| 6 | Boot exige chaves de documento | Ambientes e desenvolvedores | Gerar as chaves e guardá-las fora do banco antes do deploy |
| 6 | Coluna de documento em texto puro é removida | Dados existentes e backups | Fazer backup; a API preenche e verifica antes de remover. Backups antigos continuam com texto puro |

## 7. Riscos

| Risco | Mitigação |
|---|---|
| Dead-letter perde a cobrança de tickets fechados antes da primeira política | Critério de aceite do TD-305, obrigatório para concluir a Onda 3 |
| Os testes de integração podem falhar ao rodar pela primeira vez | TD-002 vem antes de tudo; reavaliar o tamanho da Onda 0 se aparecerem falhas reais |
| Duplicatas em produção bloqueiam a migration | TD-302 falha de forma segura; rodar a detecção antes do deploy |
| Coluna gerada ou índice funcional não ser bem representado pelo EF e pelo Pomelo | Validar no início da Onda 3, com uma migration real em MySQL 8.4 |
| Segredos continuam no histórico do git | Rotacionar; reescrever o histórico só se algum valor real tiver sido exposto |
| Token continua válido após rebaixamento ou desativação | Limite de 60 minutos documentado em D3 |
| O bootstrap público permite que um estranho vire Owner | Documentar em D5; adotar o segredo de bootstrap se a API ficar exposta antes da configuração inicial |
| Renomeações e movimentações geram conflitos | Fazer a Onda 4 em commits pequenos, sem misturar com mudança de comportamento |
| Remover o Mongo pesa na futura decisão de Occupancy | ADR registra o motivo, e o histórico do git preserva o código |
| Perder a chave de criptografia torna os documentos irrecuperáveis | Guardar as chaves fora do banco e do backup dele, com cópia segura |
| Texto puro permanece em backups e binlogs antigos | Documentar no README e expirá-los conforme a política de retenção |
