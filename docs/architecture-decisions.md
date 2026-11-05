# Architecture Decision Records (ADRs)

## ADR-001: Modular Architecture Based on Subdomain Classification

**Status**: Accepted, patterns revised on 2026-11-04
**Date**: 2025-12-01
**Context**: Need to choose architecture pattern based on subdomain types using Tactical Design Decision Tree

### Subdomain Analysis
- **Core Domain**: Access (vehicle entry/exit control - the essence of parking; RFID is planned)
- **Supporting Domain**: Occupancy (spot availability, capacity monitoring; planned, no module yet)
- **Generic Domain**: Billing (pricing and fee calculation; payment processing is planned), Identity/Management

### Decision
- **Access Context**: Rich Domain Model (core business logic)
- **Billing Context**: Rich Domain Model, with `PricingPolicy` and `Ticket` aggregates and the `Money` value object
- **Identity/Management**: Rich Domain Model, with the `OperatorUser` aggregate and the `PasswordHash` value object
- **Occupancy Context**: Transaction Script (planned; supporting operations, moderate complexity)

### Rationale
1. **Access** is the core differentiator - and can change faster
2. **Occupancy** will support the core by providing real-time availability data
3. **Billing** stays a generic subdomain. Subscribers will pay a fixed price through their subscription, with no discounts on hourly rates
4. **Identity/Management** provides management features, operators can authorize and authenticate

### Why generic subdomains use aggregates
The first version of this ADR chose Transaction Script for Billing and Active Record for Management. The code uses aggregates in both, and that is kept for now because each has invariants worth protecting in one place: a closed ticket's frozen fee and policy revisions in Billing; the unique Owner and role changes in Management. The classification does not change.

### Consequences
- ✅ Flexibility in billing rules without affecting other contexts
- ✅ One tactical pattern across the existing modules
- ❌ More modeling than a generic subdomain usually needs

---

## ADR-002: Database Strategy per Context

**Status**: Superseded by [ADR-005](#adr-005-mysql-only)
**Date**: 2025-12-01

### Decision
Database per bounded context with provider flexibility, considering MongoDB for Access and Occupancy. The part that still applies, one schema per context, moved to ADR-005.

---

## ADR-003: Event-Driven Communication

**Status**: Accepted
**Date**: 2025-12-01

### Decision
Integration events for inter-context communication, written to an outbox in the same transaction as the data and published in-process with MediatR.

### Rationale
- **Access** (core) publishes entry/exit events that drive the system
- **Billing** subscribes to access events for fee calculation
- **Occupancy** (planned) will control physical availability independently
- Loose coupling allows core domain to evolve independently

---

## ADR-004: Multi-Database Provider Support

**Status**: Superseded by [ADR-005](#adr-005-mysql-only)
**Date**: 2025-12-01

### Decision
Repository pattern with a provider configurable between MySQL and MongoDB for the Access context. The provider and its configuration were removed.

---

## ADR-005: MySQL Only

**Status**: Accepted
**Date**: 2026-11-03
**Supersedes**: ADR-002, ADR-004

### Context
The MongoDB provider of Access had no outbox, no atomic `IUnitOfWork`, no ticket support, and no tests, so it could not be selected safely.

### Decision
Remove the MongoDB provider, driver and configuration. Every context uses MySQL, with one schema per context.

### Consequences
- ✅ One persistence path to maintain, test and migrate
- ✅ Outbox and atomicity work the same way in every module
- ❌ Bringing MongoDB back means recovering the code from git history and completing what it lacked

Occupancy can reassess the choice when it is built.

---

## ADR-006: Token Revocation

**Status**: Accepted
**Date**: 2026-11-03

### Context
Tokens are valid for 60 minutes. Checking the operator's state on every request would revoke access immediately, at the cost of one database query per request.

### Decision
Outside Management, accept up to 60 minutes of token validity after an operator is deactivated or demoted. In Management, role and active state keep coming from the database.

### Consequences
- ✅ No database query on each request to other modules
- ✅ Account administration always uses the current state
- ❌ A deactivated or demoted operator can still operate Access and Billing until the token expires

---

## ADR-007: Client Document Protection

**Status**: Accepted
**Date**: 2026-11-04

### Context
Clients with a contract need a CPF or CNPJ, and the database should not expose it to anyone who can read it. Uniqueness must keep working without decrypting every row. MySQL has no AES-GCM, and a key cannot go into migration SQL.

### Decision
Encrypt the document in the application with AES-256-GCM and keep an HMAC-SHA256 with a separate key for the unique index. Both keys come from the environment. Migrate existing rows in three phases, expand, fill and verify, then remove the plain text, with the fill running in the application at boot.

### Consequences
- ✅ A database leak alone does not expose documents, and uniqueness stays in a database index
- ✅ A failed migration keeps the plain text and can be run again
- ❌ Losing the encryption key loses every document, so the keys need their own backup
- ❌ Key rotation is not implemented: the value carries a key version, but rotating needs a new process
- ❌ Older backups and binary logs still hold plain text until they expire
