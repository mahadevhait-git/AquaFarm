# Frontend Architecture (Angular)

## Current Structure

- `frontend/src/app/components` - reusable standalone UI components
- `frontend/src/app/pages` - routed page components (`login`, `dashboard`, `pond`, `group`, `loan`)
- `frontend/src/app/services` - API and auth services
- `frontend/src/app/models` - TypeScript interfaces and enums
- `frontend/src/app/guards` - route guards (authentication)
- `frontend/src/app/app.routes.ts` - Angular router configuration
- `frontend/src/environments` - environment configuration (API base URL)

## Routing and Auth

- Routes:
  - `/login`
  - `/dashboard`
  - `/ponds`
  - `/groups`
  - `/loans`
- Protected routes use `authGuard`.
- JWT token and user role are stored in `localStorage`.

## API Integration

- `api.service.ts` wraps backend calls for:
  - auth (`/api/auth/*`)
  - ponds (`/api/ponds/*`)
  - groups (`/api/groups/*`)
  - loans (`/api/loans/*`)
- `auth.service.ts` manages token and role lifecycle.

## Current UI Scope

- Login form with demo credentials support.
- Dashboard pond listing.
- Pond create/list flow.
- Group create/list flow.
- Loan create flow.

## Suggested Next Steps

- Add an Angular `HttpInterceptor` to inject bearer token headers centrally.
- Introduce typed request/response DTOs in frontend services.
- Add global error handling and loading states.
- Add page-level tests for critical flows (login, list, create).
