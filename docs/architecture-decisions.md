# Architecture Decision Records (ADRs)

## ADR-001: Modular Architecture Based on Subdomain Classification

**Status**: Accepted  
**Date**: 2024-11-30  
**Context**: Need to choose architecture pattern based on subdomain types using Tactical Design Decision Tree

### Subdomain Analysis
- **Core Domain**: Access (vehicle entry/exit control, RFID management - the essence of parking)
- **Supporting Domain**: Occupancy (spot availability, capacity monitoring)
- **Generic Domain**: Billing (payment processing, pricing calculations), Identity/Management

### Decision
- **Access Context**: Rich Domain Model (core business logic)
- **Occupancy Context**: Transaction Script (supporting operations, moderate complexity)
- **Billing Context**: Transaction Script (generic payment processing with some domain features)
- **Identity/Management**: Active Record (simple CRUD operations)

### Rationale
1. **Access** is the core differentiator - and can change faster
2. **Occupancy** supports the core by providing real-time availability data
3. **Billing** uses generic payment processing with minimal domain-specific features (subscriber discounts)
4. **Identity/Management** provides management features, operators can authorize and authenticate

### Consequences
- ✅ Flexibility in billing rules without affecting other contexts
- ✅ Optimal complexity distribution
- ❌ Different patterns require team knowledge diversity

---

## ADR-002: Database Strategy per Context

**Status**: Accepted  
**Date**: 2024-11-30  

### Decision
Database per bounded context with provider flexibility

### Rationale
- **Access**: Core domain with complex business rules → SQL (MySQL) for ease of use, NoSQL (MongoDB) for higher flexibility (preferred)
- **Occupancy**: Real-time updates, simple state management → NoSQL (MongoDB) preferred
- **Billing**: Generic payment processing → SQL (MySQL) for transactional integrity
- **Identity/Management**: Standard CRUD → SQL (MySQL)

### Implementation
Repository pattern with provider abstraction allows switching between MySQL/MongoDB per context needs.

---

## ADR-003: Event-Driven Communication

**Status**: Accepted  
**Date**: 2024-11-30  

### Decision
Domain events for inter-context communication

### Rationale
- **Access** (core) publishes entry/exit events that drive the entire system
- **Occupancy** Independent, controls physical availability with IoT
- **Billing** subscribes to access events for fee calculation
- Loose coupling allows core domain to evolve independently

---

## ADR-004: Multi-Database Provider Support

**Status**: Accepted  
**Date**: 2024-11-30  

### Context
Access context needs flexibility to switch between MySQL and MongoDB based on operational requirements.

### Decision
Implement repository pattern with configurable database providers:
- MySQL for relational data consistency
- MongoDB for high-throughput, document-based operations

### Implementation
```csharp
public enum DatabaseProvider { MySQL, MongoDB }

services.AddAccessModule(configuration); // Auto-detects provider from config
```

### Consequences
- ✅ Operational flexibility
- ✅ Performance optimization per use case
- ❌ Additional complexity in data access layer
- ❌ Need for provider-specific models and mappers