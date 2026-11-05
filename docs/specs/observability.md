# Spec: Observabilidade com OpenTelemetry

| | |
|---|---|
| **Status** | Proposta |
| **Data** | 2026-11-04 |
| **Escopo** | Traces, métricas e logs da API, com exportação opcional por OTLP. Não inclui escolher o backend de produção (seção 8). |
| **Relacionadas** | [`technical-debt-remediation.md`](technical-debt-remediation.md), [`product-evolution.md`](product-evolution.md) |

As evidências usam `develop` em `9e69b24`. Onde citado, a Onda 6 vem de `feature/privacy-wave`, ainda sem merge, e é pré-requisito desta spec.

## 1. Objetivos

1. Seguir uma operação da requisição ao outbox e ao consumidor, com logs, traces e métricas correlacionados.
2. Medir o que hoje é só afirmação: `README:52` (1000+ transações por dia) e `README:53` (99,5% com degradação controlada).
3. Manter a API funcionando sem backend de telemetria: exportação desligada por padrão.
4. Não vazar dado pessoal nem segredo, com prova automatizada.

**Fora de escopo:** auto-instrumentação sem código, profiling, spans por método de serviço, Serilog, broker externo, rate limiting e alta disponibilidade.

## 2. O que já existe e o que falta

| Área | Existe | Falta |
|---|---|---|
| Traces | ASP.NET Core e HttpClient (`ObservabilityExtensions.cs:20-22`) | Exporter; spans de banco; continuidade no outbox; versão real do serviço (`:19` fixa `1.0.0`) |
| Métricas | Nada configurado | `WithMetrics`, medidores nativos, técnicos e de negócio |
| Logs | `ILogger` em texto; templates estruturados no outbox (`OutboxProcessor.cs:73,87-89,98-104`) | JSON, correlação nativa, níveis corretos e OTLP |
| Correlação | `CorrelationId` derivado do TraceId (`HttpCorrelationIdProvider.cs:15-32`), gravado nos eventos (`TicketService.cs:51,146`) | Contexto de span; fora de HTTP, cada chamada gera um Guid novo (`:36`) |
| Health | `/health` e checagem dos 3 bancos (`Program.cs:65-66,84-86`) | Métrica e medição externa |
| Privacidade | Onda 6: `RouteLabel`, `LogCapture`, erro 1062 sem valor | Prova em traces e métricas; dependências novas |

## 3. Decisões propostas

### Versões e pacotes

O projeto fixa 1.15.x (`NeoParking.Api.csproj:17-19`). Versões conferidas no NuGet em 2026-11-04:

| Pacote | Versão | Uso |
|---|---|---|
| `OpenTelemetry.Extensions.Hosting` | 1.20.1 | SDK e DI |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.20.1 | Spans HTTP |
| `OpenTelemetry.Instrumentation.Http` | 1.20.1 | Spans de saída |
| `OpenTelemetry.Instrumentation.Runtime` | 1.20.1 | Métricas de runtime |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.20.1 | OTLP |
| `OpenTelemetry.Exporter.InMemory` | 1.20.1 | Só testes |

**Descartado:** `OpenTelemetry.Instrumentation.EntityFrameworkCore`, ainda só em beta (1.20.1-beta.1). O Pomelo usa o MySqlConnector, que já publica spans e métricas nativos com a fonte e o medidor `MySqlConnector`, sem pacote novo.

### Serilog: não

`ILogger` com `AddJsonConsole` e o provider OpenTelemetry cobre JSON em stdout, escopos, TraceId e SpanId e OTLP. O ASP.NET Core já acrescenta os ids pelo `ActivityTrackingOptions`; confirmar no aceite do OBS-301. Serilog só traria dependência e outro pipeline para auditar. Reavaliar apenas se surgir um destino sem OTLP nem stdout.

### Limites de arquitetura

| Projeto | Pode usar | Não pode usar |
|---|---|---|
| `NeoParking.Api` | Pacotes `OpenTelemetry.*`; é o único lugar de configuração | |
| Kernel e Application | `System.Diagnostics` (`ActivitySource`, `Activity`, `Meter`), da BCL | Pacotes `OpenTelemetry.*` |
| Domain | Nada de telemetria | `System.Diagnostics` e `OpenTelemetry.*` |
| `NeoParking.E2E.Tests` | `Exporter.InMemory` | |

A regra atual não barra isso, porque `Domain_ShouldOnlyDependOnAllowedLibraries` permite qualquer `System.*` (`DomainArchitecture.cs:83`). Por isso existe o OBS-501.

### `CorrelationId`

Mantém-se: faz parte do contrato dos eventos e dos dados já gravados (`IDomainEvent.cs:11`). Ele continua sendo o TraceId em formato Guid e entra como atributo `neoparking.correlation_id` nos spans de consumo. A continuidade técnica passa para o contexto W3C (OBS-103). Removê-lo fica para quando houver contrato de eventos versionado.

## 4. Itens

### Fundação

**OBS-001: Atualizar pacotes e identidade do serviço**
- **Evidência:** `NeoParking.Api.csproj:17-19`; `ObservabilityExtensions.cs:19` fixa `serviceVersion: "1.0.0"`.
- **Requisito:** usar as versões da seção 3; obter a versão do assembly e o ambiente da configuração, como `deployment.environment.name` (conferir o status na semconv ao implementar).
- **Aceite:** um teste com exporter em memória lê `service.name` e `service.version` do recurso.

**OBS-002: Exportação OTLP opcional**
- **Evidência:** nenhum exporter está registrado (`ObservabilityExtensions.cs:17-22`).
- **Requisito:**
  - Só exportar se `OTEL_EXPORTER_OTLP_ENDPOINT` estiver definido. Sem ele, nada tenta conectar.
  - Endpoint inválido gera um aviso e desliga a exportação, sem derrubar o boot.
  - Cabeçalhos de autenticação vêm de `OTEL_EXPORTER_OTLP_HEADERS`, tratados como segredo: no compose, passagem sem valor, como os demais; nunca em log.
- **Aceite:**
  - Boot sem endpoint e com endpoint inválido funciona.
  - Com o coletor fora do ar, `/health` e um fluxo de entrada e saída seguem respondendo.

### Traces

**OBS-101: Spans de banco**
- **Evidência:** `ObservabilityExtensions.cs:20-22` não registra banco; `MySqlConnector` está no Kernel.
- **Requisito:**
  - `AddSource("MySqlConnector")`.
  - Definir `OTEL_SEMCONV_STABILITY_OPT_IN=database`. **Sem isso, o MySqlConnector emite os atributos legados, inclusive `db.statement`, `db.connection_string` e `db.user`.** Com ela, emite `db.system.name`, `db.namespace` e `db.query.text`, que são estáveis.
  - Só parâmetros, nunca literais com dado pessoal: `db.query.parameter.*` não é emitido, mas `db.query.text` traz o texto do comando.
- **Aceite:** um teste confirma `db.system.name=mysql`, ausência de `db.connection_string` e `db.user`, e que nenhum valor sensível da seção 5 aparece em `db.query.text`. Isso inclui os comandos da migração da Onda 6, porque eles usam SQL próprio.

**OBS-102: Controlar o ruído**
- **Evidência:** 2 outboxes em ciclos de 10 s (`OutboxProcessor.cs:17,52`) somam 17.280 consultas por dia, sem nenhum pai. Isso é muito mais que as transações previstas em `README:52`. O `/health` também geraria spans.
- **Requisito:**
  - Não exportar spans do polling de um ciclo vazio.
  - Excluir `/health` dos traces, mantendo-o nas métricas.
  - Amostragem inicial de 100% (`AlwaysOn`): com esse volume, é barata. Revisar com o volume medido, sem criar configuração antes disso.
- **Aceite:** em um teste com 3 ciclos vazios e 3 chamadas a `/health`, nenhum span é exportado; um ciclo com mensagem exporta o consumo e o banco.

**OBS-103: Continuidade no outbox**
- **Evidência:** `OutboxMessage.cs:12-20` não guarda contexto; `OutboxProcessor.cs:83` publica fora de qualquer span; `TicketClosedHandler.cs:19` e `Billing/Application/TicketService.cs:65` rodam sem relação com a requisição original.
- **Requisito:**
  - Nova coluna opcional `trace_parent`, preenchida em `OutboxMessage.Create` com o `traceparent` W3C, nos outboxes de Access e Billing. Mensagens antigas ficam com nulo e continuam elegíveis.
  - Um `ActivitySource` no Kernel cria um span `process` por mensagem, `Consumer`, novo e **com link** para o contexto da criação. Links são o padrão da semconv para mensageria, e aqui a entrega ocorre em lote e bem depois da requisição.
  - Atributos: `messaging.system=neoparking.outbox`, `messaging.operation.type=process`, `messaging.destination.name` com o tipo do evento, `neoparking.correlation_id` e `neoparking.outbox.module`. **Toda a semconv de mensageria está em status Development;** usar o nome atual e rever ao atualizar.
  - O span nunca recebe o payload, que contém placa.
  - Falha: status `Error`, com `error.type` e sem a mensagem da exceção.
- **Aceite:**
  - Teste de integração: `POST` de saída gera um span que aponta por link para o trace da requisição, e a Fee é criada no mesmo contexto.
  - A migration funciona do zero e sobre dados existentes.
  - Mensagem antiga sem `trace_parent` é entregue normalmente.

**OBS-104: Contexto recebido de fora**
- **Evidência:** `HttpCorrelationIdProvider.cs:15` usa o TraceId, que a API aceita de um `traceparent` enviado pelo cliente.
- **Requisito:** documentar que o `CorrelationId` não é identificador de segurança nem de auditoria, e não usar baggage recebido. Com `AlwaysOn`, um cliente não muda a amostragem. Rever se a API ficar atrás de um gateway.
- **Aceite:** um teste envia `traceparent` e `baggage` e confirma que o valor do baggage não aparece em nenhum sinal.

### Métricas

Regras gerais: nomes de negócio no namespace `neoparking.*`. Atributos somente da lista fechada desta seção. Nunca ids, placa, código, operador ou rota real.

**OBS-201: Medidores existentes**
- **Evidência:** não há `WithMetrics` em `ObservabilityExtensions.cs`.
- **Requisito:** registrar `Microsoft.AspNetCore.Hosting` (`http.server.request.duration`, estável), `Microsoft.AspNetCore.Diagnostics` (`aspnetcore.diagnostics.exceptions`, estável), `MySqlConnector` (`db.client.connections.*`) e Runtime. Conflitos 409 e erros 5xx vêm de `http.response.status_code` com `http.route`, sem métrica própria.
- **Aceite:** teste após `POST` conflitante lê `http.server.request.duration` com status 409 e rota no padrão.

**OBS-202: Saúde do outbox**
- **Evidência:** `OutboxProcessor.cs:85,95-104` só registra logs; `OutboxRepository.cs:24-34` só lê pendentes elegíveis.
- **Requisito:**

| Métrica | Tipo | Atributos |
|---|---|---|
| `neoparking.outbox.deliveries` | Counter | `module`, `outcome` (`success`, `retry`, `waiting_policy`, `dead_letter`) |
| `neoparking.outbox.pending` | Gauge | `module` |
| `neoparking.outbox.oldest_age` (`s`) | Gauge | `module` |
| `neoparking.outbox.dead_lettered` | Gauge | `module` |

  - `waiting_policy` separa o `RetryLaterException` (`OutboxMessage.cs:77,86`), que nunca chega a dead-letter e não pode acionar o alerta de falha.
  - Os valores saem de uma consulta agregada no ciclo do outbox, não do callback da métrica.
- **Aceite:** testes em MySQL real cobrem sucesso, retry, política ausente e dead-letter, além da idade após um atraso simulado.

**OBS-203: Métricas de negócio**
- **Evidência:** entrada em `TicketService.cs:34`; saídas em `:58,68,77`; Fee em `Billing/Application/TicketService.cs:65` e seguintes.
- **Requisito:**

| Métrica | Tipo | Atributos |
|---|---|---|
| `neoparking.tickets.opened` | Counter | nenhum |
| `neoparking.tickets.closed` | Counter | `exit.method` (`ticket_id`, `code`, `plate`) |
| `neoparking.fees.charged` | Counter | nenhum |
| `neoparking.fees.amount` (`BRL`) | Histogram | nenhum |

  Contar só depois do commit. Usar `IMeterFactory` para isolar os testes.
- **Aceite:** um fluxo completo incrementa cada métrica uma vez; uma saída perdida em concorrência não incrementa `closed`.

**OBS-204: Autenticação**
- **Evidência:** `OperatorService.cs:30,33` lança a mesma exceção para os três casos, de propósito (TD-106). O medidor nativo de autenticação só existe no .NET 10.
- **Requisito:** `neoparking.auth.failures`, com `reason` igual a `invalid_credentials`, `invalid_token` ou `forbidden`. **Não distinguir** e-mail inexistente, senha errada e conta inativa.
- **Aceite:** os três casos do TD-106 geram o mesmo `reason`, e nenhum atributo traz e-mail.

**OBS-205: Saúde do banco**
- **Evidência:** `DatabaseHealthCheck.cs:12-16` só responde quando alguém chama `/health`.
- **Requisito:** `neoparking.health.status` (1 ou 0), por `IHealthCheckPublisher`, sem nome de banco nem host. Intervalo padrão do framework (30 s, confirmar ao implementar).
- **Aceite:** o valor vai a 0 com o banco fora e volta a 1 depois.

### Logs

**OBS-301: JSON e correlação**
- **Evidência:** `appsettings.json` não define formato; o console é texto.
- **Requisito:** `AddJsonConsole` fora de Development, em UTC, com escopos. `TraceId` e `SpanId` em todo log dentro de um span, inclusive nos do consumo do outbox. `ActivityTrackingOptions.Baggage` desligado.
- **Aceite:** um teste de processo captura uma linha JSON válida com `TraceId` igual ao do span. Texto continua em Development.

**OBS-302: Log de requisição**
- **Evidência:** `RequestLoggingMiddleware.cs:31-37` tem `traceId` no texto; ele aparece em todo `/health`. A linha `:34` grava o caminho real em `develop`; a Onda 6 troca por `RouteLabel`.
- **Requisito:** usar um template sem `TraceId`, com `Method`, `Route`, `StatusCode` e `ElapsedMs`. `/health` só em `Debug`. Conferir o restante dos templates: propriedades nomeadas, sem interpolação.
- **Aceite:** o teste de privacidade cobre a linha, com a rota no padrão.

**OBS-303: Níveis**
- **Evidência:** `ExceptionHandlingExtensions.cs:15` usa `UseExceptionHandler`, e cada exceção tratada, inclusive login inválido, vira `fail` com stack trace.
- **Requisito:**

| Caso | Nível | Stack trace |
|---|---|---|
| 401, 403, 404, 409, 422 | `Information` | Não |
| 500 | `Error` | Sim |
| Outbox: retry, política ausente | `Warning` | Sim |
| Outbox: dead-letter ou falha de ciclo | `Error` | Sim |

  O handler passa a registrar por tipo de exceção, e o `ExceptionHandlerMiddleware` deixa de registrar em duplicidade, por filtro de categoria. Mensagens de exceção de domínio seguem sem dado pessoal (TD-601).
- **Aceite:** teste com login inválido: uma linha `Information`, sem stack trace; exceção não mapeada: `Error` com stack trace.

**OBS-304: stdout e OTLP**
- **Evidência:** não há provider de log OTLP.
- **Requisito:** stdout é sempre ligado e é a fonte em ambiente sem coletor. OTLP entra só com OBS-002 ativo, com `IncludeScopes=true` e `IncludeFormattedMessage=true`. Com os dois, não coletar stdout também no destino, para não duplicar.
- **Aceite:** teste com exporter em memória: um log carrega os ids do span ativo.

### Privacidade

**OBS-401: Prova automatizada**
- **Evidência:** `LogCapture.cs` (Onda 6) só cobre logs.
- **Requisito:** seção 5, com exporters em memória de traces, métricas e logs no fixture E2E.
- **Aceite:** o teste percorre os fluxos sensíveis e falha ao semear um valor sensível de propósito em um atributo: isso prova que ele detecta.

**OBS-402: Atributos de risco**
- **Evidência:** `OutboxProcessor.cs:98-104` passa a exceção ao log; `RouteLabel` (Onda 6) define `url.path`.
- **Requisito:**
  - `RecordException` desligado nas instrumentações: evento de exceção traz a mensagem.
  - `url.query` continua redigido, como é o padrão; confirmar em teste.
  - Não adicionar atributo de operador, cliente, IP ou `enduser.*`.
  - Nenhum `Enrich` além dos aprovados, e `Exporter.Console` nunca em produção.
- **Aceite:** coberto por OBS-401.

### Arquitetura

**OBS-501: Regras**
- **Evidência:** `DomainArchitecture.cs:83` permite qualquer `System.*`.
- **Requisito:** só `NeoParking.Api` depende de `OpenTelemetry.*`; o Domain não depende de `System.Diagnostics`.
- **Aceite:** a regra fica vermelha com uma violação temporária; depois volta ao normal.

### Operação

**OBS-601: Perfil no compose**
- **Evidência:** `docker-compose.yml` só tem `mysql` e `api`.
- **Requisito:**
  - Perfil opcional `observability`, com `grafana/otel-lgtm` em versão fixa e portas só em `127.0.0.1`. A imagem é para desenvolvimento, demonstração e testes, conforme o próprio projeto.
  - `docker compose up` sem o perfil continua sem esse serviço.
  - Um coletor próprio (`memory_limiter`, `batch`) fica para o ambiente real, conforme a decisão da seção 8.
- **Aceite:** com o perfil, o fluxo de entrada e saída aparece no Grafana com trace, métrica e log correlacionados; sem ele, a API sobe igual.

**OBS-602: SLOs e alertas**
- **Evidência:** `README:52-53` afirma metas sem medição.
- **Requisito:** seção 6, como arquivo versionado com as consultas após a escolha do backend. Ajustar o README para distinguir meta e medição.
- **Aceite:** cada alerta é disparado uma vez em ambiente de teste.

**OBS-603: Medição externa de disponibilidade**
- **Evidência:** a própria API não consegue relatar que está fora do ar.
- **Requisito:** uma verificação externa de `GET /health` em intervalo fixo, por ferramenta do backend ou do provedor.
- **Aceite:** derrubar a API gera falha na medição e alerta.

## 5. Privacidade: requisito testável

**Valores semeados pelo teste:** CPF, CNPJ alfanumérico, placa, código de ticket, motivo de saída, e-mail, senha, JWT, connection string, senha do banco, `Jwt:Secret`, chaves `DOCUMENT_*` e nomes de banco fora de `db.namespace`.

**Onde procurar:** nome, atributos, eventos, links e status de spans; baggage; atributos de recurso; corpo, template, escopos e propriedades de logs; atributos de todas as métricas.

**Fluxos:** login válido e inválido; cliente criado, duplicado e consultado; `GET /clients/{id}/document`; entrada; saída por id, código e placa; ciclo do outbox com sucesso e falha; `/health`.

**Regras adicionais:**
- O identificador de rota é o padrão (`http.route`), nunca o caminho real.
- `db.statement`, `db.connection_string` e `db.user` não podem existir (OBS-101).
- A verificação compara o valor bruto e, onde couber, sem máscara de formatação.
- Só atributos da lista fechada aparecem nas métricas de `neoparking.*`.

## 6. O que medir

| Meta | Medição | Limite honesto |
|---|---|---|
| 99,5% (`README:53`), cerca de 3h36 fora do ar em 30 dias | OBS-603 | Enquanto não houver 30 dias medidos, é meta, não resultado |
| Degradação controlada (`README:53`) | Taxa de 5xx, `/health` e idade do outbox | A API ainda não define um modo degradado; a telemetria mostra a falha, não a evita |
| 1000+ por dia (`README:52`), cerca de 1 a cada 86 s | `neoparking.tickets.*` | Mostra o volume real; capacidade exige teste de carga, ainda sem spec |
| Latência | `http.server.request.duration` por `http.route` | Sem meta no README; definir depois de 2 semanas de linha de base |

**Alertas iniciais** (limites revistos após a linha de base):

| Alerta | Condição inicial |
|---|---|
| API indisponível | 2 falhas seguidas em OBS-603 |
| Dead-letter | `outbox.dead_lettered` > 0 |
| Outbox atrasado | `oldest_age` > 15 min por 5 min, fora de `waiting_policy` |
| Tickets sem tarifa | `deliveries{outcome=waiting_policy}` crescendo por 15 min |
| Erros 5xx | Taxa > 1% em 15 min, **com pelo menos 20 requisições**: com pouco tráfego, um erro isolado acionaria o alerta |
| Pool de conexões | `db.client.connections.timeouts` > 0 |
| Falhas de login | Pico acima da linha de base; só após medir |

## 7. Ordem, custo e riscos

**Dependência:** Onda 6 (`RouteLabel`, `LogCapture`, `WithoutDatabaseErrorValuesInLogs`). Não exportar nada antes dela nem antes de OBS-401.

**Ordem:**
1. OBS-501, OBS-001, OBS-401 e OBS-402: a rede de segurança vem primeiro.
2. OBS-301, OBS-302 e OBS-303: já valem sem backend e resolvem a pendência do log de login.
3. OBS-101, OBS-102, OBS-201 e OBS-002.
4. OBS-103 e OBS-104.
5. OBS-202 a OBS-205 e OBS-304.
6. OBS-601, OBS-602 e OBS-603, depois da decisão de backend.

**Custo:** o volume previsto é pequeno; o maior risco de volume é o polling, tratado por OBS-102. Com a exportação desligada, o custo fica em criar as atividades. Medir antes de reduzir amostragem.

| Risco | Mitigação |
|---|---|
| Dado pessoal em telemetria, que costuma ter retenção e acesso próprios | OBS-401, OBS-101 e revisão a cada novo atributo |
| Coletor fora do ar afetar a API | Exportação em lote, com descarte; aceite de OBS-002 |
| Polling dominar os traces | OBS-102 |
| Mudança na semconv de mensageria | Atributos de OBS-103 concentrados em um único ponto do Kernel |
| Alertas ruidosos com pouco tráfego | Mínimo de eventos e revisão após linha de base |
| Métrica com alta cardinalidade | Lista fechada e verificação em teste |

## 8. Perguntas abertas

| # | Pergunta | Recomendação |
|---|---|---|
| Q1 | Qual backend em produção? | Grafana Cloud, se não houver quem opere a stack; Prometheus, Tempo e Loki próprios, se os dados precisarem ficar sob seu controle. Em ambos os casos, OTLP evita dependência de fornecedor. Conferir limites e preço na contratação. |
| Q2 | A telemetria pode sair do país ou ir a um terceiro? | Depende da análise de LGPD. OBS-401 reduz o risco, mas não substitui a análise. |
| Q3 | Por quanto tempo reter? | Traces e logs: 14 dias; métricas: 90 dias. Definir com o custo real e com o TD-606. |
| Q4 | Quem recebe os alertas? | Definir antes de OBS-602. |

## 9. Referências

- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/): traces, métricas e logs estáveis.
- [Semconv de métricas HTTP](https://opentelemetry.io/docs/specs/semconv/http/http-metrics/): `http.server.request.duration` estável; as demais métricas de servidor, em desenvolvimento.
- [Semconv de banco](https://opentelemetry.io/docs/specs/semconv/db/database-spans/): estável; `db.query.parameter.*` em desenvolvimento e opt-in.
- [Semconv de mensageria](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/): em desenvolvimento; links como padrão.
- [Métricas do ASP.NET Core](https://opentelemetry.io/docs/specs/semconv/dotnet/dotnet-aspnetcore-metrics/): autenticação e autorização só no .NET 10.
- [MySqlConnector: tracing](https://mysqlconnector.net/diagnostics/tracing/) e [métricas](https://mysqlconnector.net/diagnostics/metrics/).
- [OTLP em .NET, Microsoft](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-otlp-example).
- [`grafana/otel-lgtm`](https://github.com/grafana/docker-otel-lgtm): destinado a desenvolvimento, demonstração e testes.
- [Versões no NuGet](https://www.nuget.org/profiles/OpenTelemetry).
