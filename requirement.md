Act as a senior software architect and full-stack developer.

I want to build a web application for managing fish farming ponds (aquaculture management system) used by small groups of farmers.

Business Context:
- There are multiple ponds.
- Each pond is owned/managed by an individual farmer.
- Farmers sometimes form groups (around 10 members).
- A group may assign 1–2 managers who handle shared financial records.
- Currently, lack of transparency causes financial disputes among members.

Goal:
Build a system where:
1. Each farmer independently manages their own pond.
2. Each farmer can track:
   - Investments
   - Expenses
   - Revenue
   - Profit/Loss
3. Group-level financial visibility is transparent.
4. Reduce disputes by maintaining clear, auditable records.

Requirements:

🔹 User Roles:
- Admin
- Farmer (pond owner)
- Group Manager

🔹 Core Features:
1. Authentication (JWT-based login/register)
2. Pond Management
   - Create/Edit/Delete ponds
   - Assign owner
3. Financial Tracking
   - Add investments
   - Track expenses (feed, labor, medicine, etc.)
   - Record sales/revenue
   - Auto-calculate profit/loss
4. Group Management
   - Create groups
   - Add/remove members
   - Assign group manager(s)
5. Shared Financial Ledger
   - Group-level transactions
   - Individual contribution tracking
6. Reports & Dashboard
   - Pond-wise profit/loss
   - Monthly reports
   - Group financial summary
7. Audit & Transparency
   - Transaction history
   - Logs of changes

🔹 Technical Requirements:
- Backend: .NET Core Web API
- Frontend: React (with TypeScript)
- Database: SQL Server
- Authentication: JWT
- ORM: Entity Framework Core

🔹 Non-Functional:
- Clean architecture
- Scalable design
- Secure APIs
- Proper validation
- Error handling

Tasks:
1. Design database schema (ER diagram + tables)
2. Define API endpoints (RESTful)
3. Create backend project structure (Clean Architecture)
4. Generate entity models and DTOs
5. Implement core services (financial calculations, group logic)
6. Suggest frontend structure
7. Provide sample UI pages (dashboard, pond, transactions)
8. Add sample test data

Output everything step by step in a structured manner.
🔹 Interest & Loan Management:
- Users can lend or borrow money
- Support Simple and Compound Interest
- Auto-calculate interest based on time
- Track repayments and outstanding balance
- Maintain full audit trail of interest calculations

Tasks:
- Design Loan and Interest tables
- Implement interest calculation service
- Create APIs:
  - Create Loan
  - Calculate Interest
  - Repay Loan
  - Get Loan Summary