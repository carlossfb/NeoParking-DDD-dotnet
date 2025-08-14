# 🅿️ NeoParking - DDD Approach

> Applying Domain-Driven Design (DDD) in practice with a pragmatic and objective approach.

---

## 📖 Table of Contents

1. [About](#about)  
2. [Domain Description](#domain-description)  
3. [General Assumptions](#general-assumptions)  
4. [Process Discovery](#process-discovery)  
5. [Project Structure and Architecture](#project-structure-and-architecture)  
6. [Aggregates](#aggregates)  
   6.1 [Ticket](#ticket)  
   6.2 [Owner](#owner)  
   6.3 [Owner](#occupation) 
7. [Events](#events)  
   7.1 [Events in Repositories](#events-in-repositories)  
8. [ArchUnit](#archunit)  
9. [Functional Thinking](#functional-thinking)   
10. [Architecture-Code Gap](#architecture-code-gap)  
11. [Model-Code Gap](#model-code-gap)  
12. [.NET](#.net)  
13. [Tests](#tests)  
14. [How to Contribute](#how-to-contribute)  
15. [References](#references)

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

Breaks down the system into its main modules:

- "Owner Context"
- "Ticketing Context"
- "Occupation Context"
- "Payment Context"
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


## 6. Aggregates

### 6.1 Ticket

![Aggregate root Ticket](https://www.plantuml.com/plantuml/png/XP11Qm8n58Jl-HMFFIb5eTSzY4KjUB9GxVqHnpMOP2LvAxR5_zwONLjSa5uIvfiFCcGQXOCqpYenOa5hemyGUgcgW8e5D6Y9yHNDQYyuor2f8-i3Lw2YhnVqhOYqvHYfxJ8W6f7akDoMjDaivnqyOM-qnPjbGNcNFbQT0Y_XSvx8shZah7Qx2BdbctCsyeyIXQM9PRVgFQUs6kNhYNG02zf-CBn1MyS-uqRsTJ1VM5od_QbiTNET_WhymW0BZbYaWPbTL3gC5uGTQz2yTcpyuWGlglTnipndIyOTBLmIuftTazFCnwwYJgFYkQ3nUD9xeYGu8v4c3qLRnpllegRmsyRP7m00)


### 6.2 Owner

![Aggregate root Owner](https://www.plantuml.com/plantuml/png/RP8_Ry8m4CLtVufJ9aeLrAvCzC_2K85KmMxP3nFLiIFdA4KLtxs4NArYTKhUx_nyTtbPzAmJ7rglk64FMXq34ht4mFXYgaK_EPVWD4EfJfiFxK1LKQdeMn4Ph_jUJ8EconLnK4ixfyrc-Ieiw1PpUkVK12V8nC3QS4cxNK4wVMeHBkVMuyjsDHRgw-EU4bpJCz9rYCcTq4DdDUEMMNXaLhYts3V6e_0BBMuegGENQDS4A-pvjFQQCHcNecuGVvoorHju7stscxqE3hUrl3y574KCvyyg1o4jSnIVeox2-2yW4trCnPVtZK7eSHs_ABNHmlyocK5-W-gnecbTK_KmcqshFD9yQDkhireWs4FhVm00)


### 6.3 Occupation

![Aggregate root Occupation](https://www.plantuml.com/plantuml/png/TP1DQiCm48NtEiNWLK99eDiiGaBTGbPUf3d0L7us8lenyhWKIkvUANM30ir6GjzyyqRIRXIECZchn8W5DLfF0ui9dlPz7z6pW1O5VGbUy2LBfGRMP-v1rNyz5Nqh8crv7ClC38bM56xeq6xeT3hKSs7WU3Q-Fmv1SkKeCPtA9eE4FqAmO3_p5W8jQFUoeQRhHaZDWdVZy2kihjiJkPJTp1cBPG7V8d055vKMumMnOmsgWsFZNp_XHTrUaw_sbqB63HOENiOxkNHp-4DHKVaxrND5w_NsViI6u4ngspvsyuXIlAWTV3iv-mC0)


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



---
'
## 13. Tests

TODO

---

## 14. How to Contribute

TODO

---

## 15. References

TODO
