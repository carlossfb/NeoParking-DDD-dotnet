# 🅿️ NeoParking - DDD Approach

> Applying Domain-Driven Design (DDD) in practice with a pragmatic and objective approach.

---

## 📖 Table of Contents

1. [About](#1-about)
2. [Domain Description](#2-domain-description)
3. [General Assumptions](#3-general-assumptions)
4. [Process Discovery](#4-process-discovery)
5. [Project Structure and Architecture](#5-project-structure-and-architecture)
6. [Context Map](#6-context-map)
7. [Events](#7-events)
8. [Architecture Tests](#8-architecture-tests)
9. [DevSec](#9-devsec)
10. [.NET](#10-net)
11. [Tests](#11-tests)
12. [How to Contribute](#12-how-to-contribute)
13. [References](#13-references)

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
- Owner/Client
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

- **Operator** — The entity responsible for managing the entire parking system. Configures operational rules, monitors the system, manages pricing policies, handles audits, and ensures infrastructure and services are functioning properly.
- **Client** — A customer who owns a vehicle and has the right to use the parking service. Can be either a casual user (pay-per-use) or a registered subscriber. Associated with tickets, payments, and identification methods like RFID.
- **Subscriber** — A special type of client with an active subscription plan (e.g., monthly pass). Typically has extended privileges such as automated entry and exit via RFID, and is charged through a recurring fee rather than per visit.
- **Ticket** — A representation of a parking session. Includes entry time, vehicle identification, and assigned parking area. Can be physical (printed with QR code) or digital.
- **Fee** — The monetary charge applied to a parking session. Usually calculated based on duration, but may include special pricing rules, grace periods, or penalties.
- **RFID** — A Radio Frequency Identification tag assigned to a vehicle or user. Allows automated access control by detecting and identifying subscribers at entry and exit points without manual intervention.

---

## 5. Project Structure and Architecture

The project follows a **Modular Monolith** with **Domain-Driven Design** tactical patterns. Each Bounded Context is a single class library — layers are separated by folders, not by projects, enforced through architecture tests.

📋 **[View Complete ADRs →](docs/architecture-decisions.md)**

![Arch](https://github.com/carlossfb/NeoParking-DDD/blob/main/docs/graph/arch.drawio.png)

### Solution Structure

```
NeoParking-DDD-dotnet/
├── src/
│   ├── Shared/
│   │   └── NeoParking.Shared.Kernel/     # Base types shared across modules
│   │       ├── Primitives/
│   │       │   └── Entity.cs             # Base aggregate/entity class
│   │       ├── Events/
│   │       │   └── IDomainEvent.cs
│   │       └── Exceptions/
│   │           └── DomainException.cs
│   │
│   ├── Modules/
│   │   └── Access/                       # Access Bounded Context
│   │       └── NeoParking.Access.csproj  # Single project per BC
│   │           ├── Domain/               # Entities, VOs, repository interfaces
│   │           │   ├── Entities/
│   │           │   ├── ValueObjects/
│   │           │   └── Repositories/
│   │           ├── Application/          # Use cases, DTOs, service interfaces
│   │           │   ├── UseCases/
│   │           │   └── DTOs/
│   │           └── Infrastructure/       # EF Core, MongoDB, mappers
│   │               ├── Persistence/
│   │               │   ├── MySql/
│   │               │   └── Mongo/
│   │               ├── Repositories/
│   │               └── Migrations/
│   │
│   ├── NeoParking.Api/                   # Web API — entrypoint and DI composition
│   │   ├── Endpoints/
│   │   │   └── ClientEndpoints.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── NeoParking.Tests/                 # All tests
│       ├── Unit/
│       │   ├── Domain/
│       │   └── Application/
│       ├── Integration/
│       │   ├── Repository/
│       │   └── Service/
│       ├── Architecture/
│       └── E2E/
│
└── NeoParking.sln
```

### Dependency Rules

```
NeoParking.Shared.Kernel   ← no dependencies
NeoParking.Access.Domain   → Shared.Kernel
NeoParking.Access.Application → Domain
NeoParking.Access.Infrastructure → Domain + Application
NeoParking.Api             → Application + Infrastructure (DI composition only)
NeoParking.Tests           → all
```

### Module Registration Pattern

Each module exposes its services through extension methods, keeping `Program.cs` clean:

```csharp
// Program.cs
builder.Services.AddAccessModule(builder.Configuration);
```

```csharp
// AccessModule.cs (inside NeoParking.Access.Infrastructure)
public static IServiceCollection AddAccessModule(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // registers DbContext or MongoDbContext based on provider config
    // registers IClientRepository → MySqlClientRepository or MongoClientRepository
    // registers IClientService → ClientService
    return services;
}
```

---

## 6. Context Map

![ContextMap](https://www.plantuml.com/plantuml/png/RLBDIWCn4BxdAOPwi8MjegKN3r9RL-n1MhJrv2KamngQJPPCgXRnWNmENynPTxTDGTX3-7w-6RxP2KKPuhQqWZR6LJB84WAgA5rX4Ju5mDG7ZM7chGzmCXwFQqYgJH7yrkaMplESuSS62Gu3N5oAhoHIXk3V_-9QnsWqOe4uXMbjGisuY_WHIHoczswKGgAEUd7zcJNeOWRF-6gKnGnMHcqm3deW2HfrwbyejQsaKxiaOkvN6Hm8Ne-NB9g4FPo6J8srh4WYbd9NyXgKmHqYDTPMbHhdcaKcxiuVf9C5rfOaP4qOE669eJH4nhisg7DnD_it_7p3kg8OaAki2uNejIUnvuV3QsZBkeB5_TCHq-ts3vKKqP0yj5Dhh90l0_pX6riyyRERWabNdU5eYLjVIkCWhvq4_UPEC-i9TGOOrTb0237X0Vph_G80)

---

## 7. Events

TODO

---

## 8. Architecture Tests

The project enforces architectural boundaries through **NetArchTest**, ensuring DDD and Hexagonal Architecture rules are respected automatically in CI/CD.

### Layer Separation Rules

- **Domain Independence** — Domain layer has no external dependencies (EF Core, MongoDB, etc.)
- **Application Isolation** — Application layer does not depend on Infrastructure
- **Dependency Direction** — Domain never depends on Application or Infrastructure
- **Repository Contracts** — All concrete repositories implement `IClientRepository`

### Running Architecture Tests

```bash
dotnet test --filter "FullyQualifiedName~Architecture"
```

---

## 9. DevSec

The project implements security-first development practices with integrated vulnerability scanning and dependency management.

### Software Composition Analysis (SCA)

We use **Snyk** for continuous security monitoring of dependencies and transitive packages.

```bash
# Install Snyk CLI
npm install -g snyk

# Authenticate
snyk auth

# Scan for vulnerabilities
snyk test
```

### Security Practices

- **Dependency Pinning** — Explicit package versions to prevent supply chain attacks
- **Regular Updates** — Monthly security updates for critical packages
- **Vulnerability Remediation** — Immediate patching of high/critical CVEs
- **No Secrets in Code** — Credentials via environment variables or `appsettings.Development.json` (gitignored)

---

## 10. .NET

### Prerequisites

- .NET 8 SDK
- Docker (for MySQL or MongoDB)
- `dotnet-ef` tool v8: `dotnet tool install --global dotnet-ef --version 8.0.11`

### Running locally

**1. Start the database**

```bash
# MySQL
docker run -d \
  --name neoparking-mysql \
  -e MYSQL_ROOT_PASSWORD=password \
  -e MYSQL_DATABASE=neoparking_access \
  -p 3306:3306 \
  mysql:8

# or MongoDB
docker run -d -p 27017:27017 --name neoparking-mongo mongo
```

**2. Configure**

Create `src/NeoParking.Api/appsettings.Development.json` (gitignored):

```json
{
  "Access": {
    "DatabaseProvider": "MySQL",
    "ConnectionString": "Server=localhost;Database=neoparking_access;Uid=root;Pwd=password;AllowPublicKeyRetrieval=true;SslMode=none;"
  }
}
```

For MongoDB:
```json
{
  "Access": {
    "DatabaseProvider": "MongoDB",
    "ConnectionString": "mongodb://localhost:27017/neoparking_access"
  }
}
```

**3. Run migrations (MySQL only)**

```bash
dotnet ef database update \
  --project src/Modules/Access/NeoParking.Access.csproj \
  --startup-project src/NeoParking.Api/NeoParking.Api.csproj
```

**4. Run the API**

```bash
dotnet run --project src/NeoParking.Api/NeoParking.Api.csproj
```

### Adding a new Bounded Context

```bash
# Create module
dotnet new classlib -n NeoParking.{Context} -o src/Modules/{Context}/NeoParking.{Context}
dotnet sln add src/Modules/{Context}/NeoParking.{Context}/NeoParking.{Context}.csproj

# Add references
dotnet add src/Modules/{Context}/NeoParking.{Context}/NeoParking.{Context}.csproj \
  reference src/Shared/NeoParking.Shared.Kernel/NeoParking.Shared.Kernel.csproj

dotnet add src/NeoParking.Api/NeoParking.Api.csproj \
  reference src/Modules/{Context}/NeoParking.{Context}/NeoParking.{Context}.csproj

dotnet add src/NeoParking.Tests/NeoParking.Tests.csproj \
  reference src/Modules/{Context}/NeoParking.{Context}/NeoParking.{Context}.csproj
```

### Commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run API
dotnet run --project src/NeoParking.Api/NeoParking.Api.csproj

# Create migration
dotnet ef migrations add {Name} \
  --project src/Modules/Access/NeoParking.Access.csproj \
  --startup-project src/NeoParking.Api/NeoParking.Api.csproj

# Apply migration
dotnet ef database update \
  --project src/Modules/Access/NeoParking.Access.csproj \
  --startup-project src/NeoParking.Api/NeoParking.Api.csproj
```

---

## 11. Tests

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
| Integration | Repository + Service with InMemory DB | xUnit + EF InMemory |
| Architecture | Layer dependency rules | NetArchTest |
| E2E | HTTP endpoints against running API | REST Client (`.http` file) |

### Running Tests

```bash
# All tests
dotnet test

# By category
dotnet test --filter "FullyQualifiedName~Unit"
dotnet test --filter "FullyQualifiedName~Integration"
dotnet test --filter "FullyQualifiedName~Architecture"
dotnet test --filter "FullyQualifiedName~E2E"

# With detailed output
dotnet test --logger "console;verbosity=detailed"
```

---

## 12. How to Contribute

TODO

---

## 13. References

- Evans, Eric. *Domain-Driven Design: Tackling Complexity in the Heart of Software*. Addison-Wesley, 2003.
- Khononov, Vlad. *Learning Domain-Driven Design: Aligning Software Architecture and Business Strategy*. O'Reilly, 2021.
- Vernon, Vaughn. *Implementing Domain-Driven Design*. Addison-Wesley, 2013.