# Swarnakshi

**Construction Expense & Inventory Management** — an Expense, Inventory, Project-Cost and Payment
management system for construction businesses (multi-site, multi-villa).

## Tech stack

| Layer    | Choice |
|----------|--------|
| Backend  | ASP.NET Core (.NET 10) Web API, Clean Architecture |
| ORM / DB | EF Core 10 + SQLite (swappable to SQL Server) |
| Auth     | JWT bearer, role-based |
| Frontend | React + Vite + TypeScript + Tailwind (mobile-first PWA-style SPA) |

## Solution layout

```
src/
  Swarnakshi.Domain          entities, enums, domain rules (no dependencies)
  Swarnakshi.Application      DTOs, service interfaces, validators, use-case services
  Swarnakshi.Infrastructure   EF Core DbContext, configs, migrations, seed, JWT, repositories
  Swarnakshi.Api              controllers, middleware, DI composition root
web/                          React frontend
docs/                         architecture, data model, workflows, progress log
```

## Getting started

Prerequisites: .NET 10 SDK, Node 20+.

```bash
# backend — http://localhost:5080, API docs at /scalar/v1
dotnet restore
dotnet run --project src/Swarnakshi.Api
# DB (swarnakshi.db) is migrated + seeded automatically on first run in Development.
# Manual migration: dotnet dotnet-ef database update --project src/Swarnakshi.Infrastructure --startup-project src/Swarnakshi.Api

# frontend — http://localhost:5173 (proxies /api to :5080)
cd web
npm install
npm approve-scripts esbuild   # one-time: npm gates the esbuild postinstall
npm run dev
```

Default login (seeded): `owner@swarnakshi.local` / `Owner@123`.

## Documentation

- [Architecture & implementation report](docs/01-architecture.md)
- [Data model](docs/02-data-model.md)
- [Workflows: approval, inventory, costing](docs/03-workflows.md)
- [API reference](docs/04-api.md)
- [Progress log](docs/05-progress.md) — read this first when picking up work

## Contributing (team)

- Branch per feature: `feat/<area>-<short>`, PR into `main`.
- Update `docs/05-progress.md` in the same PR as the code.
- Never hard-delete financial transactions — cancel / reverse / void.
- Business logic lives in `Application`/`Domain`, never in controllers or the UI.
