# API Endpoints for Aquaculture Management

## Authentication
- `POST /api/auth/register` - register a new user and issue JWT
- `POST /api/auth/login` - authenticate and receive JWT

## Pond Management
- `GET /api/ponds` - list all ponds
- `GET /api/ponds/{id}` - get pond details
- `POST /api/ponds` - create a new pond
- `PUT /api/ponds/{id}` - update a pond
- `DELETE /api/ponds/{id}` - delete a pond

## Group Management
- `GET /api/groups` - get all groups
- `POST /api/groups` - create a group

## Loan Management
- `POST /api/loans` - create a loan
- `POST /api/loans/{loanId}/repay` - repay a loan
- `GET /api/loans/{loanId}/summary` - get loan summary and outstanding balance

## Notes
- Add future endpoints for transaction history, reports, and audit logs.
- Domain endpoints should enforce JWT authentication with `[Authorize]`.
- Current frontend expects flat JSON DTO-style responses for list/detail APIs.
