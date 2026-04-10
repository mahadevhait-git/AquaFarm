# Aquaculture Management System Architecture

## Backend Architecture

- Clean Architecture with separate layers:
  - `AquaFarm.Core`: domain entities, DTOs, and service interfaces
  - `AquaFarm.Infrastructure`: EF Core persistence, database context, loan and financial services
  - `AquaFarm.Api`: ASP.NET Core Web API and HTTP controllers

## Domain Model

- Users: `Admin`, `Farmer`, `GroupManager`
- Ponds: owned by a farmer, optionally assigned to a group
- Groups: members and group-level shared ledger transactions
- Transactions: investments, expenses, revenue, loan disbursements, repayments
- Loans: principal, simple/compound interest, repayments, outstanding balance
- Audit trail: change logs for transparency

## Technical Stack

- Backend: .NET 10 Web API
- ORM: Entity Framework Core
- Database: SQL Server via connection string
- Authentication: JWT bearer tokens
- Testing: xUnit skeleton included
- Frontend: Angular (standalone components + Angular Router)

## Non-Functional Goals

- Modular separation of responsibilities
- Clear domain boundaries
- Secure API surface with token-based authentication
- Extensible services for financial calculations and loan logic
- Maintainable frontend structure with route guards and service abstraction
