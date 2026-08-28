# Regalis WorkSpace

Premium coworking-space management platform — Phase 1 (Live Release).
Delivered by Amstag Tech Pvt Ltd.

Two portals (**Customer** and **Admin**) served by **one backend API** and **one
database**, with a single central booking & availability engine feeding one shared
calendar.

## Stack

| Tier | Technology |
|------|-----------|
| Frontend | React 19 + TypeScript + Vite (installable PWA) |
| Backend | ASP.NET Core Web API (`net8.0` LTS), EF Core 8 |
| Database | Microsoft SQL Server 2019+ |
| Auth | JWT access tokens + rotating refresh tokens, dynamic RBAC |

## Repository layout

```
backend/          ASP.NET Core solution (Domain / Shared / Application / Infrastructure / API)
frontend/         React + TS + Vite PWA (customer + admin portals)
database/         SQL scripts: create, seed, indexes, views, stored procedures
tests/            Backend unit + integration tests
documentation/    Architecture, database design, API plan, execution plan
```

## Getting started

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet run --project src/Regalis.API
```

Swagger UI: `https://localhost:7200/swagger` (Development only).

Secrets (connection string, JWT key, Razorpay keys) come from user-secrets or
environment variables — see `documentation/ARCHITECTURE.md` §4. `appsettings.json`
ships with empty placeholders and is safe to commit.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

## Git strategy

`main` (release-only) · `develop` (integration) · `feature/*` · `release/*` ·
`hotfix/*`. Every change arrives by pull request. No secrets are ever committed.

## Documentation

Start with [`documentation/PROJECT_ANALYSIS.md`](documentation/PROJECT_ANALYSIS.md),
then [`documentation/ARCHITECTURE.md`](documentation/ARCHITECTURE.md) and
[`documentation/15_DAY_EXECUTION_PLAN.md`](documentation/15_DAY_EXECUTION_PLAN.md).
