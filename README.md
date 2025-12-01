# 🅿️ NeoParking - DDD Approach

> Applying Domain-Driven Design (DDD) in practice with a pragmatic and objective approach.

---

## 📖 Table of Contents

1. [About](#1-about)
2. [Domain Description](#2-domain-description)  
3. [General Assumptions](#3-general-assumptions)  
4. [Process Discovery](#4-process-discovery)  
5. [Project Structure and Architecture](#5-project-structure-and-architecture)  
6. [Bounded Contexts](#6-bounded-contexts)  
   6.1 [Access](#61-access)  
   6.2 [Occupancy](#62-occupancy)  
   6.3 [Billing](#63-billing)  
   6.4 [Management](#64-management) 
7. [Events](#7-events)  
   7.1 [Events in Repositories](#71-events-in-repositories)  
8. [ArchUnit](#8-archunit)  
9. [Functional Thinking](#9-functional-thinking)   
10. [Architecture-Code Gap](#10-architecture-code-gap)  
11. [Model-Code Gap](#11-model-code-gap)  
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

The main revenue of NeoParking comes from two sources: walk-in customers in the "free zone," who pay for occasional use, and monthly subscribers who guarantee a stable and predictable income. The current operation management is a combination of human supervision and manual processes. I, as the owner, supervise the team, handle daily financial management, and make occupancy decisions reactively based on observation. The modernization we are planning aims to transform this manual management into a more proactive and data-driven approach.

---

## 3. General Assumptions

![Context Map](https://www.plantuml.com/plantuml/png/ZPBBJW8n58RtVOgJsRWYn2qXX4GNccY61EB6i2YT0pOCfxMd1OtnXNmDNypQCfVPpRB_vIlyqoLxwNmurS9hNoFS6VBuuU5PMY6iL4TvG2ZA2w5hl0Bcyvq9L65rLHOB-180gfRCaBBjwGNVjAfHVFTe6wsEw3KTHX9pVe0ebGfMUcre9ACh33Wh-Nb2yYCXr_I0y5Xk8ERGaQn7OjP8R5mizXHtngH4T7k0uhQ0oMJH5M06va8iH1gvyPkHM_S6xj4YLRy_fBHaGF8EGUMVObZaGLCrWsRmMZwij_0Uq6baE6VWr2HNqzxa6rEbsQmffHV4OAyoXnqhfstQEjyqfhP7xFtpcEzzuboPFgss_rDK3ARp8iO75ikenrVy1m00)

Breaks down the system into its main bounded contexts:

- "Access Context"
- "Occupancy Context"
- "Billing Context"
- "Management Context"
- "IOT external Context"

---

## 4. Process Discovery

As an initial step, I decided to understand a little more about the business domain using user story and event storming. Through the big picture, some information that was hidden in the process was made explicit. I continued to initially place the relevant events, organized them chronologically, associated them with actions and then with the responsible actors, and if it made sense, I also associated a reading model.

![Event Storming_parte1](https://github.com/carlossfb/NeoParking-DDD/blob/main/docs/graph/temporary_clients.jpg)

![Event Storming_parte2](https://github.com/carlossfb/NeoParking-DDD/blob/main/docs/graph/creating_subscriber.jpg)

![Event Storming_parte3](https://github.com/carlossfb/NeoParking-DDD/blob/main/docs/graph/subscriber_workflow.jpg)


Definitions around domain extracted from user stories:

### Nouns (specific to NeoParking domain)
- Owner
- Subscriber driver
- RFID
- Ticket
- Fee
- Parking operator
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
- Owner
A customer who owns a vehicle and has the right to use the parking service. The owner can be either a casual user (pay-per-use) or a registered subscriber. An owner is associated with tickets, payments, and possibly identification methods like RFID.
- Subscriber
A special type of owner with an active subscription plan (e.g., monthly pass). Subscribers typically have extended privileges such as automated entry and exit via RFID, and are not charged per visit but through a recurring fee.
- Ticket
A representation of a parking session. It includes information such as entry time, vehicle identification, and assigned parking area. Tickets can be physical (e.g., printed with a QR code) or digital, and are used for validating entry, exit, and fee calculation.
- Fee
The monetary charge applied to a parking session. The fee is usually calculated based on duration, but may also include special pricing rules, grace periods, or penalties. It determines what the owner must pay before exit is authorized.
- RFID
A Radio Frequency Identification tag assigned to a vehicle or user. RFID allows for automated access control by enabling the system to detect and identify subscribers or authorized vehicles at entry and exit points without manual intervention.


---

## 5. Project Structure and Architecture

I chose to use the tactical design decision tree, ref: Learning Domain-Driven Design: Aligning Software Architecture and Business Strategy -  Vlad Khononov

![Event Storming_parte1](https://github.com/carlossfb/NeoParking-DDD/blob/main/docs/graph/arch.drawio.png)
---


## 6. Bounded Contexts





---

## 7. Events

TODO

### 7.1 Events in Repositories

TODO

---

## 8. ArchUnit

TODO

---

## 9. Functional Thinking

TODO

---


## 10. Architecture-Code Gap

TODO

---

## 11. Model-Code Gap

TODO

---

## 12. .NET

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

## 13. Tests

TODO

---

## 14. How to Contribute

TODO

---

## 15. References

TODO
