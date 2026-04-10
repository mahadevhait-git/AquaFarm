# Aquaculture Management System

## What was created

- `AquacultureManagement.slnx` - backend solution file
- `src/AquaFarm.Api` - ASP.NET Core Web API project
- `src/AquaFarm.Core` - domain entities, DTOs, service interfaces
- `src/AquaFarm.Infrastructure` - EF Core DbContext, loan/financial services, seed data
- `tests/AquaFarm.Tests` - xUnit test project scaffold
- `docs/architecture.md` - architecture overview
- `docs/api-endpoints.md` - REST API endpoint definitions
- `docs/frontend-structure.md` - Angular frontend structure and routing notes
- `frontend` - Angular frontend application

## How to run

1. Open the solution in Visual Studio or VS Code.
2. Build the API project: `dotnet build src/AquaFarm.Api/AquaFarm.Api.csproj`.
3. Run the API: `dotnet run --project src/AquaFarm.Api/AquaFarm.Api.csproj`.
4. In a second terminal, run the frontend:
   - `cd frontend`
   - `npm install`
   - `npm start`

Frontend default URL: `http://localhost:4200`  
Backend default URL: `http://localhost:5222`

The app uses SQL Server localdb by default. Update `src/AquaFarm.Api/appsettings.json` with a production connection string before deployment.

## Notes

- JWT authentication is configured with sample settings in `src/AquaFarm.Api/appsettings.json`.
- Sample seed data is created on startup when the database does not yet exist.
- Frontend structure is documented in `docs/frontend-structure.md`.
