---
name: neoparking-observability
description: NeoParking rules for traces, metrics and structured logs. Use when adding or reviewing OpenTelemetry, ILogger calls, telemetry attributes or the observability exporters. The source of truth is docs/specs/observability.md.
---

# NeoParking observability

`docs/specs/observability.md` is the source of truth. If this file disagrees with it, follow the spec and fix this file.

## Packages by project

| Project | Can use | Cannot use |
|---|---|---|
| `NeoParking.Api` | `OpenTelemetry.*`; the only place that configures it | |
| Kernel and Application | `ActivitySource`, `Activity`, `Meter` from the BCL | `OpenTelemetry.*` |
| Domain | No telemetry | `System.Diagnostics`, `OpenTelemetry.*` |
| `NeoParking.E2E.Tests` | `OpenTelemetry.Exporter.InMemory` | |

No Serilog: `ILogger`, `AddJsonConsole` and the OpenTelemetry provider are enough. Do not add `OpenTelemetry.Instrumentation.EntityFrameworkCore`, which is beta only.

## Allowed attributes

- Identify routes by `http.route`, the pattern, never the real path.
- In `neoparking.*` metrics, use only the closed attribute list in the spec. Do not add a new attribute without changing the spec.
- Do not add `Enrich` beyond the approved ones, or attributes for operator, client, IP or `enduser.*`.

## Forbidden values

These must never appear in span names, attributes, events, links or status, in baggage, in resource attributes, in log bodies, templates, scopes or properties, or in metric attributes:

- CPF, CNPJ, plate, ticket code, exit reason, e-mail, password;
- JWT, connection string, database password, `Jwt:Secret`, `DOCUMENT_*` keys;
- database names outside `db.namespace`;
- `db.statement`, `db.connection_string`, `db.user`.

Also:

- Set `OTEL_SEMCONV_STABILITY_OPT_IN=database`: without it, MySqlConnector emits the database attributes above.
- Keep `RecordException` off in the instrumentations, because the event carries the exception message.
- Keep `url.query` redacted.
- Never use `Exporter.Console` in production.
- Log with message templates, without interpolating sensitive values.

## How to test

1. Use the in-memory exporters for traces, metrics and logs in the E2E fixture.
2. Seed the forbidden values and run the flows in section 5 of the spec.
3. Compare raw values and, where relevant, values without format masks.
4. Prove that the test detects a leak: it must fail when a value is seeded in an attribute on purpose.
5. Prove the architecture rule the same way, with a temporary violation (OBS-501).
6. With the exporter off, the API must boot normally.
