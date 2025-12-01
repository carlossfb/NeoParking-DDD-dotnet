# 🅿️ NeoParking - DDD Approach

> Applying Domain-Driven Design (DDD) in practice with a pragmatic and objective approach.

---

## 📖 Table of Contents

1. [About](#1-about)
2. [Domain Description](#2-domain-description)  
3. [General Assumptions](#3-general-assumptions)  
4. [Process Discovery](#4-process-discovery)  
5. [Project Structure and Architecture](#5-project-structure-and-architecture)  
6. [Context Map](#6-Context-Map)  
7. [Events](#7-events)  
   7.1 [Events in Repositories](#71-events-in-repositories)  
8. [ArchUnit](#8-archunit)   
9. [.NET](#9-net)  
10. [Tests](#10-tests)  
11. [How to Contribute](#11-how-to-contribute)  
12. [References](#12-references)

---

## 1. About

NeoParking is a domain-driven sample project that aims to model a real-world parking operation using strategic and tactical DDD principles.  
It highlights architecture design, domain modeling, and implementation choices aligned with a real business case.

---

## 2. Domain Description

NeoParking is a conventional parking lot that seeks to modernize its operations. Its main differential is the human service and the trust it offers, but it faces challenges with manual ticket management and lack of visibility about spot availability. The vision is to integrate technology to optimize processes, improve customer experience, and ensure efficiency while maintaining the human touch that differentiates it.

The main revenue of NeoParking comes from two sources: walk-in customers in the "free zone," who pay for occasional use, and monthly subscribers who guarantee a stable and predictable income. The current operation management is a combination of human supervision and manual processes. I, as the owner, supervise the team, handle daily financial management, and make occupancy decisions reactively based on observation. The modernization we are planning aims to transform this manual management into a more proactive and data-driven approach.

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


## 4. Process Discovery

As an initial step, I decided to understand a little more about the business domain using user story and event storming. Through the big picture, some information that was hidden in the process was made explicit. I continued to initially place the relevant events, organized them chronologically, associated them with actions and then with the responsible actors, and if it made sense, I also associated a reading model.

<iframe width="768" height="496" src="https://miro.com/app/live-embed/uXjVJXjg3VM=/?focusWidget=3458764636230014271&embedMode=view_only_without_ui&embedId=886965748997" frameborder="0" scrolling="no" allow="fullscreen; clipboard-read; clipboard-write" allowfullscreen></iframe>


Definitions around domain extracted from user stories:

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

- Operator
The entity responsible for managing the entire parking system. The operator configures operational rules, monitors the system, manages pricing policies, handles audits, and ensures that the infrastructure and services are functioning properly.
- Client
A customer who owns a vehicle and has the right to use the parking service. The owner can be either a casual user (pay-per-use) or a registered subscriber. An owner is associated with tickets, payments, and possibly identification methods like RFID.
- Subscriber
A special type of client with an active subscription plan (e.g., monthly pass). Subscribers typically have extended privileges such as automated entry and exit via RFID, and are not charged per visit but through a recurring fee.
- Ticket
A representation of a parking session. It includes information such as entry time, vehicle identification, and assigned parking area. Tickets can be physical (e.g., printed with a QR code) or digital, and are used for validating entry, exit, and fee calculation.
- Fee
The monetary charge applied to a parking session. The fee is usually calculated based on duration, but may also include special pricing rules, grace periods, or penalties. It determines what the owner must pay before exit is authorized.
- RFID
A Radio Frequency Identification tag assigned to a vehicle or user. RFID allows for automated access control by enabling the system to detect and identify subscribers or authorized vehicles at entry and exit points without manual intervention.


---

## 5. Project Structure and Architecture

I chose to use the tactical design decision tree, ref: Learning Domain-Driven Design: Aligning Software Architecture and Business Strategy -  Vlad Khononov

The architectural decisions based on this approach are described in more detail in the Architecture Decision Records (ADRs).

📋 **[View Complete ADRs →](docs/architecture-decisions.md)**

![Arch](https://github.com/carlossfb/NeoParking-DDD/blob/main/docs/graph/arch.drawio.png)
---


## 6. Context Map

![ContextMap](https://www.plantuml.com/plantuml/png/RLBDIWCn4BxdAOPwi8MjegKN3r9RL-n1MhJrv2KamngQJPPCgXRnWNmENynPTxTDGTX3-7w-6RxP2KKPuhQqWZR6LJB84WAgA5rX4Ju5mDG7ZM7chGzmCXwFQqYgJH7yrkaMplESuSS62Gu3N5oAhoHIXk3V_-9QnsWqOe4uXMbjGisuY_WHIHoczswKGgAEUd7zcJNeOWRF-6gKnGnMHcqm3deW2HfrwbyejQsaKxiaOkvN6Hm8Ne-NB9g4FPo6J8srh4WYbd9NyXgKmHqYDTPMbHhdcaKcxiuVf9C5rfOaP4qOE669eJH4nhisg7DnD_it_7p3kg8OaAki2uNejIUnvuV3QsZBkeB5_TCHq-ts3vKKqP0yj5Dhh90l0_pX6riyyRERWabNdU5eYLjVIkCWhvq4_UPEC-i9TGOOrTb0237X0Vph_G80)



---

## 7. Events

TODO

### 7.1 Events in Repositories

TODO

---

## 8. ArchUnit

TODO

---


## 9. .NET

### Configuração do Projeto

Passo a passo para criação da estrutura do projeto:

```bash
# Criar solução
dotnet new sln -n Neoparking

# Criar diretório para módulos
mkdir module

# Criar projeto de biblioteca de classes
dotnet new classlib -n Access -o .\module\Access\src\

# Criar projeto de testes
dotnet new xunit -n Access.Tests -o module/Access/test

# Adicionar projetos à solução
dotnet sln add .\module\Access\src\Access.csproj
dotnet sln add .\module\Access\test\Access.Tests.csproj

# Adicionar referência do projeto principal no projeto de testes
dotnet add module\Access\test\Access.Tests.csproj reference module\Access\src\Access.csproj
```

### Comandos para Execução

```bash
# Restaurar dependências
dotnet restore

# Compilar projeto
dotnet build

# Executar testes
dotnet test
```

---

## 10. Tests

TODO

---

## 11. How to Contribute

TODO

---

## 12. References

TODO
