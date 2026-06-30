# 🅿️ NeoParking - DDD Approach

> Applying Domain-Driven Design (DDD) in practice with a pragmatic and objective approach.

---

## 📖 Table of Contents

1. [About](#1-about)
2. [Domain Description](#2-domain-description)
3. [General Assumptions](#3-general-assumptions)
4. [Process Discovery](#4-process-discovery)
5. [Bounded Contexts](#5-bounded-contexts)
6. [Project Structure and Architecture](#6-project-structure-and-architecture)
7. [Context Map](#7-context-map)
8. [Events](#8-events)
9. [Authentication & Authorization](#9-authentication--authorization)
10. [Architecture Tests](#10-architecture-tests)
11. [DevSec](#11-devsec)
12. [.NET](#12-net)
13. [Tests](#13-tests)
14. [How to Contribute](#14-how-to-contribute)
15. [References](#15-references)

---

## 1. About

NeoParking is a domain-driven sample project that aims to model a real-world parking operation using strategic and tactical DDD principles.
It highlights architecture design, domain modeling, and implementation choices aligned with a real business case.

---

## 2. Domain Description

NeoParking is a conventional parking lot that seeks to modernize its operations. Its main differential is the human service and the trust it offers, but it faces challenges with manual ticket management and lack of visibility about spot availability. The vision is to integrate technology to optimize processes, improve customer experience, and ensure efficiency while maintaining the human touch that differentiates it.

The main revenue of NeoParking comes from two sources: walk-in customers in the "free zone," who pay for occasional use, and monthly subscribers who guarantee a stable and predictable income. The current operation management is a combination of human supervision and manual processes.

---

## 3. General Assumptions

### Business Assumptions

**Payment Models Volatility**: Subscription, monthly, and daily payment models are subject to frequent changes due to market conditions and business strategy adjustments. However, the **hourly payment model remains the core stable revenue stream** and serves as the foundation for all pricing calculations.

**Pricing Strategy**: The hourly model acts as the base unit, with other models being derivatives or packages of hourly rates with discounts or special conditions.

### Technical Assumptions

**Scalability**: System designed for medium-scale operations (1000+ daily transactions)
**Availability**: 99.5% uptime requirement with graceful degradation
**Data Consistency**: Eventual consistency acceptable between contexts
**Integration**: REST APIs for external systems, domain events for internal communication

---

## 4. Process Discovery

As an initial step, I decided to understand a little more about the business domain using user story and event storming. Through the big picture, some information that was hidden in the process was made explicit.

[Big Picture](https://miro.com/app/live-embed/uXjVJXjg3VM=/?focusWidget=3458764636230014271&embedMode=view_only_without_ui&embedId=230222431067)

### Nouns (specific to NeoParking domain)
- Owner
- Client
- Subscriber driver
- RFID
- Ticket
- Fee
- Parking operator / Manager
- Available parking spots
- Subscription plan
- Payment

### Verbs (with context)

- Register entry (vehicle/subscriber entry recorded)
- Present RFID or QR Code (at entry for identification)
- Register exit (vehicle/subscriber exit recorded)
- Validate ticket or identification (at exit)
- Control duration of stay (for fee calculation)
- View available parking spots (in real-time)
- Monitor parking occupancy (for operational decisions)
- Manage subscription plan (renew, cancel, update)
- Update access permissions (based on subscription status)
- Process payment (for subscriptions or one-time tickets)

### Domain language

- **Operator** — An internal system user (Operator, Admin, or Owner) who authenticates to operate any module of NeoParking. Distinct from the parking *Client* — this is staff, not a customer.
- **Client** — A customer who owns a vehicle and has the right to use the parking service. Can be either a casual user (pay-per-use) or a registered subscriber. Associated with tickets, payments, and identification methods like RFID.
- **Subscriber** — A special type of client with an active subscription plan (e.g., monthly pass). Typically has extended privileges such as automated entry and exit via RFID, and is charged through a recurring fee rather than per visit.
- **Ticket** — A representation of a parking session. Includes entry time, vehicle identification, and assigned parking area. Can be physical (printed with QR code) or digital.
- **Fee** — The monetary charge applied to a parking session. Usually calculated based on duration, but may include special pricing rules, grace periods, or penalties.
- **RFID** — A Radio Frequency Identification tag assigned to a vehicle or user. Allows automated access control by detecting and identifying subscribers at entry and exit points without manual intervention.

---

## 5. Bounded Contexts

### Access

Owns the relationship with the parking's customers — registering clients, their vehicles, and identification methods. Publishes integration events (e.g. `ClientRegisteredIntegrationEvent`) through the outbox pattern so other contexts can react without coupling to Access's internals.

### Management

The **gatekeeper** of the system. Owns authentication and authorization for every internal user (`Operator`, `Admin`, `Owner`) who needs to operate any other module. No business module is meant to be reachable without first passing through Management's login and role checks.

- **Owner** — single, unique account, created once via a bootstrap endpoint. Cannot be demoted, deactivated, or duplicated.
- **Admin** — created by the Owner; can create and manage Operators.
- **Operator** — day-to-day staff account; cannot create or manage other accounts.

Issues JWT tokens on successful login, consumed by every protected endpoint across the API via ASP.NET Core's standard `RequireAuthorization()` pipeline.

> Future Bounded Contexts (pricing, spot availability, subscriptions) will sit behind Management's authorization gate the same way Access does today.

---

## 6. Project Structure and Architecture

The project follows a **Modular Monolith** with **Domain-Driven Design** tactical patterns. Each Bounded Context is a single class library — layers are separated by folders, not by projects, enforced through architecture tests. Tests live in a sibling `tests/` folder, fully decoupled from production code — no test assembly is ever published or shipped.

📋 **[View Complete ADRs →](docs/architecture-decisions.md)**

![Arch](https://github.com/carlossfb/NeoParking-DDD/blob/main/docs/graph/arch.drawio.png)

### Solution Structure

```
NeoParking-DDD-dotnet/
├── src/
│   ├── Shared/
│   │   └── NeoParking.Shared.Kernel/        # Base types shared across modules
│   │       ├── Primitives/
│   │       │   └── Entity.cs                # Base aggregate/entity class
│   │       ├── Events/
│   │       │   ├── IDomainEvent.cs
│   │       │   ├── IEventDispatcher.cs
│   │       │   └── ClientRegisteredIntegrationEvent.cs  # cross-BC contract
│   │       ├── Outbox/
│   │       │   ├── OutboxMessage.cs
│   │       │   └── IOutboxRepository.cs
│   │       ├── Observability/
│   │       │   └── ICorrelationIdProvider.cs
│   │       └── Exceptions/
│   │           ├── DomainException.cs
│   │           └── NotFoundException.cs
│   │
│   ├── Modules/
│   │   ├── Access/                          # Access Bounded Context
│   │   │   ├── NeoParking.Access.csproj      # Single project per BC
│   │   │   ├── Domain/                       # Client, Vehicle, VOs (Cpf, Plate, PhoneNumber)
│   │   │   ├── Application/                  # ClientService, DTOs, interfaces
│   │   │   ├── Infraestructure/               # EF Core (MySQL) + MongoDB, outbox processor
│   │   │   └── Migrations/
│   │   │
│   │   └── Management/                       # Management Bounded Context — the gatekeeper
│   │       ├── NeoParking.Management.csproj
│   │       ├── Domain/                        # OperatorUser, OperatorRole, PasswordHash
│   │       ├── Application/                   # OperatorService, IOperatorRepository, IPasswordHasher
│   │       ├── Infrastructure/                # EF Core (MySQL), BCrypt hasher, outbox processor
│   │       └── Migrations/
│   │
│   └── NeoParking.Api/                       # Web API — entrypoint and DI composition
│       ├── Endpoints/
│       │   ├── Endpoints.cs                   # Access endpoints
│       │   └── ManagementEndpoints.cs         # Login, Owner bootstrap, Operator CRUD
│       ├── Middleware/
│       │   └── RequestLoggingMiddleware.cs
│       ├── JwtTokenGenerator.cs
│       ├── MediatREventDispatcher.cs
│       ├── HttpCorrelationIdProvider.cs
│       ├── Program.cs
│       └── appsettings.json
│
├── tests/                                    # Decoupled from src/ — never shipped
│   ├── NeoParking.Access.Tests/
│   │   ├── Unit/
│   │   └── Integration/                      # Testcontainers (MySQL)
│   ├── NeoParking.Management.Tests/
│   │   └── Unit/
│   └── NeoParking.Tests/
│       └── Architecture/                      # NetArchTest — cross-cutting rules
│
└── Neoparking.sln
```

### Dependency Rules

```
NeoParking.Shared.Kernel        ← no dependencies
NeoParking.Access.Domain        → Shared.Kernel
NeoParking.Access.Application   → Domain
NeoParking.Access.Infrastructure→ Domain + Application
NeoParking.Management.Domain    → Shared.Kernel
NeoParking.Management.Application → Domain
NeoParking.Management.Infrastructure → Domain + Application
NeoParking.Api                  → Access + Management (DI composition only)
```

Bounded Contexts never reference each other's projects directly. Cross-BC communication happens exclusively through integration events published to the outbox and defined in `Shared.Kernel/Events` — e.g. Management reacts to `ClientRegisteredIntegrationEvent` without depending on `NeoParking.Access`.

### Module Registration Pattern

Each module exposes its services through extension methods, keeping `Program.cs` clean:

```csharp
// Program.cs
builder.Services.AddAccessModule(builder.Configuration);
builder.Services.AddManagementModule(builder.Configuration);
```

```csharp
// AccessModule.cs / ManagementModule.cs
public static IServiceCollection AddManagementModule(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // registers ManagementDbContext (MySQL)
    // registers IOperatorRepository → MySqlOperatorRepository
    // registers IPasswordHasher → BcryptPasswordHasher
    // registers OperatorService
    // registers the outbox repository + background processor
    return services;
}
```

---

## 7. Context Map

![ContextMap](https://www.plantuml.com/plantuml/png/RLBDIWCn4BxdAOPwi8MjegKN3r9RL-n1MhJrv2KamngQJPPCgXRnWNmENynPTxTDGTX3-7w-6RxP2KKPuhQqWZR6LJB84WAgA5rX4Ju5mDG7ZM7chGzmCXwFQqYgJH7yrkaMplESuSS62Gu3N5oAhoHIXk3V_-9QnsWqOe4uXMbjGisuY_WHIHoczswKGgAEUd7zcJNeOWRF-6gKnGnMHcqm3deW2HfrwbyejQsaKxiaOkvN6Hm8Ne-NB9g4FPo6J8srh4WYbd9NyXgKmHqYDTPMbHhdcaKcxiuVf9C5rfOaP4qOE669eJH4nhisg7DnD_it_7p3kg8OaAki2uNejIUnvuV3QsZBkeB5_TCHq-ts3vKKqP0yj5Dhh90l0_pX6riyyRERWabNdU5eYLjVIkCWhvq4_UPEC-i9TGOOrTb0237X0Vph_G80)

---

## 8. Events

### Integration Events (cross-BC, via outbox)

| Event | Published by | Consumed by | Purpose |
|---|---|---|---|
| `ClientRegisteredIntegrationEvent` | Access | Management | Notify other contexts a new client exists |

Each Bounded Context owns its own outbox table and background processor. Domain events are dispatched in-process via MediatR (`IEventDispatcher` → `MediatREventDispatcher`); integration events crossing BC boundaries are additionally persisted to the outbox for at-least-once delivery.

---

## 9. Authentication & Authorization

Management issues JWTs on successful login (`POST /operators/login`). Tokens carry the operator's id, email, and role as claims, validated on every request via ASP.NET Core's JWT Bearer middleware.

### Endpoint protection

| Endpoint | Auth required? | Reason |
|---|---|---|
| `POST /operators/login` | No | Entry point — credentials exchanged for a token |
| `POST /operators/owner` | No | One-time bootstrap of the single Owner account |
| `POST /operators/{requesterId}` (create operator) | Yes | Only Owner/Admin may create accounts |
| `GET /operators/{id}` | Yes | Internal data |
| `POST /operators/{requesterId}/promote/{targetId}` | Yes | Owner-only |
| `POST /operators/{requesterId}/demote/{targetId}` | Yes | Owner-only |
| `POST /operators/{requesterId}/deactivate/{targetId}` | Yes | Owner/Admin only |
| `POST /operators/{requesterId}/activate/{targetId}` | Yes | Owner/Admin only |

Role-based business rules (e.g. only the Owner can create an Admin) are enforced in `OperatorService`, not just at the HTTP layer — the same invariants hold even if called from another internal context.

### Configuration

```json
{
  "Jwt": {
    "Secret": "REPLACE_WITH_A_LONG_RANDOM_SECRET",
    "Issuer": "neoparking",
    "Audience": "neoparking",
    "ExpireMinutes": "60"
  }
}
```

---

## 10. Architecture Tests

The project enforces architectural boundaries through **NetArchTest**, ensuring DDD and Hexagonal Architecture rules are respected automatically in CI/CD.

### Layer Separation Rules

- **Domain Independence** — Domain layer has no external dependencies (EF Core, MongoDB, etc.)
- **Application Isolation** — Application layer does not depend on Infrastructure
- **Dependency Direction** — Domain never depends on Application or Infrastructure
- **No Cross-BC Project References** — Access and Management never reference each other directly; only `Shared.Kernel`
- **Repository Contracts** — All concrete repositories implement their respective interface (`IClientRepository`, `IOperatorRepository`)

### Running Architecture Tests

```bash
dotnet test tests/NeoParking.Tests --filter "FullyQualifiedName~Architecture"
```

---

## 11. DevSec

The project implements security-first development practices with integrated vulnerability scanning and dependency management.

### Software Composition Analysis (SCA)
a
We use **Snyk** for continuous security monitoring of dependencies and transitive packages.

```bash
# Install Snyk CLI
npm install -g snyk

# Authenticate
snyk auth

# Scan for vulnerabilities
# Scan for vulnerabilities
dotnet restore
snyk test --file=Neoparking.sln
```

### Security Practices

- **Dependency Pinning** — Explicit package versions to prevent supply chain attacks
- **Regular Updates** — Monthly security updates for critical packages
- **Vulnerability Remediation** — Immediate patching of high/critical CVEs
- **No Secrets in Code** — Credentials via environment variables or `appsettings.Development.json` (gitignored)
- **Password Storage** — Operator passwords are hashed with BCrypt; plaintext is never persisted (`PasswordHash` value object)
- **Token-Based Auth** — All sensitive Management endpoints require a valid JWT, validated against issuer, audience, signature, and expiry

---

## 12. .NET

### Prerequisites

- .NET 8 SDK
- Docker (for MySQL, MongoDB, and running the full stack via `docker-compose`)
- `dotnet-ef` tool v8: `dotnet tool install --global dotnet-ef --version 8.0.11`

### Running locally with Docker Compose (recommended)

```bash
docker compose up -d --build
```

This starts MySQL with separate schemas for each Bounded Context (`neoparking_access`, `neoparking_management`, created via `scripts/mysql/init.sql`) and the API, applying EF Core migrations automatically on boot.

### Running locally without Docker Compose

**1. Start the database**

```bash
docker run -d \
  --name neoparking-mysql \
  -e MYSQL_ROOT_PASSWORD=password \
  -e MYSQL_DATABASE=neoparking_access \
  -p 3306:3306 \
  mysql:8
```

**2. Configure**

Create `src/NeoParking.Api/appsettings.Development.json` (gitignored):

```json
{
  "Access": {
    "DatabaseProvider": "MySQL",
    "ConnectionString": "Server=localhost;Database=neoparking_access;Uid=root;Pwd=password;AllowPublicKeyRetrieval=true;SslMode=none;"
  },
  "Management": {
    "ConnectionString": "Server=localhost;Database=neoparking_management;Uid=root;Pwd=password;AllowPublicKeyRetrieval=true;SslMode=none;"
  },
  "Jwt": {
    "Secret": "REPLACE_WITH_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS",
    "Issuer": "neoparking",
    "Audience": "neoparking",
    "ExpireMinutes": "60"
  }
}
```

For MongoDB (Access only):
```json
{
  "Access": {
    "DatabaseProvider": "MongoDB",
    "ConnectionString": "mongodb://localhost:27017/neoparking_access"
  }
}
```

**3. Run migrations**

```bash
dotnet ef database update \
  --project src/Modules/Access/NeoParking.Access.csproj \
  --startup-project src/NeoParking.Api/NeoParking.Api.csproj

dotnet ef database update \
  --project src/Modules/Management/NeoParking.Management.csproj \
  --startup-project src/NeoParking.Api/NeoParking.Api.csproj
```

**4. Run the API**

```bash
dotnet run --project src/NeoParking.Api/NeoParking.Api.csproj
```

**5. Bootstrap the Owner account**

```bash
curl -X POST http://localhost:8080/operators/owner \
  -H "Content-Type: application/json" \
  -d '{"name":"Carlos","email":"carlos@neoparking.com","password":"ChangeMe123!"}'
```

### Adding a new Bounded Context

```bash
# Create production module
dotnet new classlib -n NeoParking.{Context} -o src/Modules/{Context}
dotnet sln add src/Modules/{Context}/NeoParking.{Context}.csproj

dotnet add src/Modules/{Context}/NeoParking.{Context}.csproj \
  reference src/Shared/NeoParking.Shared.Kernel/NeoParking.Shared.Kernel.csproj

dotnet add src/NeoParking.Api/NeoParking.Api.csproj \
  reference src/Modules/{Context}/NeoParking.{Context}.csproj

# Create its dedicated test project — always under tests/, never under src/
dotnet new xunit -n NeoParking.{Context}.Tests -o tests/NeoParking.{Context}.Tests
dotnet sln add tests/NeoParking.{Context}.Tests/NeoParking.{Context}.Tests.csproj

dotnet add tests/NeoParking.{Context}.Tests/NeoParking.{Context}.Tests.csproj \
  reference src/Modules/{Context}/NeoParking.{Context}.csproj
```

### Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run API
dotnet run --project src/NeoParking.Api/NeoParking.Api.csproj

# Create migration for a specific module
dotnet ef migrations add {Name} \
  --project src/Modules/{Access|Management}/NeoParking.{Access|Management}.csproj \
  --startup-project src/NeoParking.Api/NeoParking.Api.csproj

# Apply migration
dotnet ef database update \
  --project src/Modules/{Access|Management}/NeoParking.{Access|Management}.csproj \
  --startup-project src/NeoParking.Api/NeoParking.Api.csproj
```

---

## 13. Tests

### Test Pyramid

```
        E2E (Few)
       /          \
  Integration (Some)
 /                  \
Unit Tests (Many)
```

| Layer | Scope | Tool |
|---|---|---|
| Unit | Domain entities, VOs, Application service | xUnit + Moq + FluentAssertions |
| Integration | Repository + Outbox with real MySQL | xUnit + Testcontainers |
| Architecture | Layer and cross-BC dependency rules | NetArchTest |
| E2E | HTTP endpoints against running API | REST Client (`.http` file) |

Tests are fully decoupled from production code under `tests/`, one project per Bounded Context plus a shared `NeoParking.Tests` project for architecture rules. No test assembly is referenced by, or shipped with, the API.

### Running Tests

```bash
# All tests
dotnet test

# Per Bounded Context
dotnet test tests/NeoParking.Access.Tests
dotnet test tests/NeoParking.Management.Tests

# Architecture only
dotnet test tests/NeoParking.Tests --filter "FullyQualifiedName~Architecture"

# Skip integration tests (no Docker/Testcontainers required)
dotnet test --filter "FullyQualifiedName!~Integration"

# With detailed output
dotnet test --logger "console;verbosity=detailed"
```

> Integration tests use Testcontainers and require a running Docker daemon reachable from wherever `dotnet test` executes (e.g. inside WSL if Docker only runs there).

---

## 14. How to Contribute

This project follows **Git Flow**. Below is the standard path for shipping a feature, plus how releases and hotfixes are handled.

### Branches

| Branch | Purpose | Branches from | Merges into |
|---|---|---|---|
| `main` | Production-ready code, tagged releases | — | — |
| `develop` | Integration branch, always deployable to staging | — | — |
| `feature/{name}` | A single feature or fix in progress | `develop` | `develop` |
| `release/{version}` | Stabilizing a release candidate | `develop` | `main` and `develop` |
| `hotfix/{version}` | Urgent production fix | `main` | `main` and `develop` |

### Starting a feature

```bash
git checkout develop
git pull origin develop
git checkout -b feature/operator-password-reset
```

Branch name pattern: `feature/{kebab-case-name}` — short, descriptive, no ticket numbers required unless your team uses an issue tracker.

### Working on the feature

Commit as often as needed during development — these intermediate commits won't survive in `develop`'s history (see squash below), so don't over-engineer commit messages here. Push regularly so the branch is backed up and visible to reviewers:

```bash
git push -u origin feature/operator-password-reset
```

### Finishing the feature: squash merge into `develop`

**Why squash for features:** the value of "how I got here" lives in the Pull Request itself (GitHub/GitLab preserves every commit, comment, and review there permanently). What matters for `develop`'s own history is being **readable and revertable** — one commit per feature means `git log --oneline` stays clean, and reverting a whole feature is a single `git revert <hash>` instead of untangling a chain of WIP commits.

```bash
git checkout develop
git pull origin develop
git merge --squash feature/operator-password-reset
git commit -m "feat(management): add operator password reset flow"
git push origin develop
git branch -d feature/operator-password-reset
git push origin --delete feature/operator-password-reset
```

Or, if working through a Pull Request (recommended for any non-trivial change): open the PR from `feature/{name}` into `develop`, and use the platform's **"Squash and merge"** button — it does exactly the above and keeps the PR discussion linked to the resulting commit.

### Commit message convention

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(management): add operator password reset flow
fix(access): correct duplicate plate validation
refactor(shared-kernel): extract outbox dispatch interval to config
test(management): cover owner uniqueness invariant
docs(readme): document gitflow workflow
```

### Releases: merge commit into `main`

Unlike features, releases represent a real, immutable point in history — every commit on a release branch should be traceable, since the tag on `main` must match exactly what was shipped. **No squashing here.**

```bash
git checkout develop
git pull origin develop
git checkout -b release/1.2.0

# stabilize: bump version, update changelog, fix release-blocking bugs
git commit -m "chore(release): bump version to 1.2.0"

# merge into main with a real merge commit (no --squash)
git checkout main
git pull origin main
git merge --no-ff release/1.2.0
git tag -a v1.2.0 -m "Release 1.2.0"
git push origin main --tags

# merge back into develop so it has the release's stabilization commits too
git checkout develop
git merge --no-ff release/1.2.0
git push origin develop

git branch -d release/1.2.0
git push origin --delete release/1.2.0
```

### Hotfixes: urgent fixes on `main`

```bash
git checkout main
git pull origin main
git checkout -b hotfix/1.2.1

git commit -m "fix(access): patch outbox message duplication on retry"

git checkout main
git merge --no-ff hotfix/1.2.1
git tag -a v1.2.1 -m "Hotfix 1.2.1"
git push origin main --tags

git checkout develop
git merge --no-ff hotfix/1.2.1
git push origin develop

git branch -d hotfix/1.2.1
git push origin --delete hotfix/1.2.1
```

### Summary

| Flow | Strategy | Reason |
|---|---|---|
| `feature` → `develop` | Squash merge | Day-to-day commits are noisy; PR keeps the detailed history |
| `release` → `main` / `develop` | Merge commit (`--no-ff`) | Release history must be traceable commit-by-commit against the tag |
| `hotfix` → `main` / `develop` | Merge commit (`--no-ff`) | Same as release — urgent fixes need full traceability |

Before opening a PR or merging into `develop`, make sure:

```bash
dotnet build
dotnet test --filter "FullyQualifiedName!~Integration"
dotnet test tests/NeoParking.Tests --filter "FullyQualifiedName~Architecture"
```

---

## 15. References

- Evans, Eric. *Domain-Driven Design: Tackling Complexity in the Heart of Software*. Addison-Wesley, 2003.
- Khononov, Vlad. *Learning Domain-Driven Design: Aligning Software Architecture and Business Strategy*. O'Reilly, 2021.
- Vernon, Vaughn. *Implementing Domain-Driven Design*. Addison-Wesley, 2013.
