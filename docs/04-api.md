# 04 — API Reference

Base: `/api`. All responses wrapped: `{ "success": bool, "message": string?, "data": T?, "errors": [] }`.
Auth: `Authorization: Bearer <jwt>`. Errors use the same envelope with HTTP 4xx/5xx.
List endpoints accept `?page=&pageSize=&sort=&q=` plus resource-specific filters and return
`{ items, page, pageSize, total }`.

## Company registration (public)
| Method | Route | Notes |
|--------|-------|-------|
| POST | `/api/register` | `{companyName, companyCode, username, password, confirmPassword, contactEmail?, contactMobile?}` → creates the tenant, its Owner and its master data. Code unique; name need not be. |
| GET | `/api/register/code-available?code=` | live availability for the sign-up form |

## Auth
| Method | Route | Role | Notes |
|--------|-------|------|-------|
| POST | `/api/auth/login` | anon | `{login, password}` — `username@companycode` or a 10-digit mobile for a company user, a bare username for an EnterpriseAdmin. Returns `kind: "tenant" | "platform"`. |
| POST | `/api/auth/refresh` | anon | rotate tokens |
| POST | `/api/auth/logout` | any | revoke refresh token |
| GET  | `/api/auth/me` | any | current user + permissions + company licence state |

## Masters (Owner write; all read)
`/api/units`, `/api/material-categories`, `/api/material-subcategories`,
`/api/expense-heads`, `/api/expense-subheads`, `/api/labour-categories`, `/api/payment-methods`,
`/api/project-types`, `/api/suppliers`, `/api/contractors`, `/api/customers`, `/api/settings`
— standard `GET list`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` (delete only if unused & master).

## Party Master — contractors / customers / suppliers (`masters.manage` for writes; all roles read)
`{party}` is one of `contractors` | `customers` | `suppliers`.

| Method | Route | Notes |
|---|---|---|
| GET | `/api/{party}?q=&active=&type=&page=&pageSize=` | paged. `q` searches code, name, company, mobile, email, GSTIN and contractor type, case-insensitively. `active` omitted = all |
| GET | `/api/{party}/summary` | total / active / inactive |
| GET | `/api/{party}/types` | distinct contractor types, for the filter (empty for customers/suppliers) |
| GET | `/api/{party}/{id}` | full record + `codeLocked` + `usage` counts |
| POST | `/api/{party}` | create — always Active; the payload has no status field |
| PUT | `/api/{party}/{id}` | update. Code rejected (409) once any transaction references the record |
| POST | `/api/{party}/{id}/deactivate` | |
| POST | `/api/{party}/{id}/reactivate` | |

There is **no DELETE**. Deactivation is always allowed (unlike Material there is no stock guard) and
never touches historical rows. An inactive party is rejected server-side for new contracts
(`ContractService`), contractor payments and new projects (`ProjectService`), while existing
contracts, payments and projects keep resolving its name.

## Material Master (`masters.manage` for writes; all roles read)
| Method | Route | Notes |
|---|---|---|
| GET | `/api/materials?q=&categoryId=&subcategoryId=&brand=&unitId=&active=&page=&pageSize=&sort=` | paged list. `q` searches code, name, company/brand, category, subcategory and specification values, case-insensitively |
| GET | `/api/materials/summary` | total / active / inactive / categories |
| GET | `/api/materials/brands` | distinct companies, for the filter |
| GET | `/api/materials/spec-definitions?subcategoryId=` | specification fields the subcategory declares — drives the dynamic form |
| GET | `/api/materials/{id}` | full record + specs + `codeLocked` / `hasStock` / `totalStock` |
| GET | `/api/materials/{id}/stock` | stock by site, read from inventory |
| POST | `/api/materials` | create |
| PUT | `/api/materials/{id}` | update. Code is rejected (409) once any transaction references the material |
| POST | `/api/materials/{id}/deactivate` | 409 while stock exists at any site |
| POST | `/api/materials/{id}/reactivate` | |

There is **no DELETE** — lifecycle is Active ↔ Inactive so transaction history stays intact.
409 responses: duplicate code, duplicate identity (name + brand + specs), code change after use,
deactivation with stock.

## Sites & Projects
| Method | Route |
|--------|-------|
| GET/POST | `/api/sites`, `/api/sites/{id}` (GET/PUT) |
| GET | `/api/sites/{id}/inventory` — balances + value |
| GET/POST | `/api/projects`, `/api/projects/{id}` (GET/PUT) |
| GET | `/api/projects/{id}/summary` — full financial summary |
| GET | `/api/projects/{id}/expenses|materials|labour|contracts|contractor-payments|customer-payments|activity` |

## Inventory
| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/inventory?siteId=&lowStock=&categoryId=&q=` | balances |
| GET | `/api/inventory/{siteId}/{materialId}` | material detail + KPIs |
| GET | `/api/inventory/transactions?siteId=&projectId=&materialId=&type=&from=&to=` | ledger |
| POST | `/api/inventory/opening-stock` | Owner/Supervisor |
| POST | `/api/inventory/adjustments` | → approval if configured |
| POST | `/api/inventory/returns` | return from project |

## Procurement
| Method | Route |
|--------|-------|
| GET/POST | `/api/purchases`, `/api/purchases/{id}` |
| POST | `/api/purchases/{id}/submit` , `/api/purchases/{id}/post` |
| POST | `/api/purchases/{id}/payments` — record supplier payment |
| GET/POST | `/api/material-requests`, `/api/material-requests/{id}` |
| POST | `/api/material-requests/{id}/submit` |
| POST | `/api/material-requests/{id}/issue` — after approval, issues stock |

## Employees (payroll)
| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/employees?q=&active=&siteId=` | searchable by name, code, phone or designation |
| GET | `/api/employees/{id}` · `/api/employees/{id}/ledger` | ledger carries advances given / recovered / outstanding |
| POST/PUT | `/api/employees` · `/api/employees/{id}` | `masters.manage`; name, phone, salary and join date required |
| GET/POST | `/api/employee-payments` | `labour.create`; Salary(1) Advance(2) Bonus(3) Reimbursement(4) |
| POST | `/api/employee-payments/{id}/submit` · `/cancel` | submit sends it to the Owner for approval |

## Expenses / Labour / Contractors / Customers
| Method | Route |
|--------|-------|
| GET/POST | `/api/expenses` (project direct/other expenses) |
| GET/POST | `/api/labour` |
| GET/POST | `/api/contracts` (contract works) |
| GET/POST | `/api/contractor-payments`; POST `/{id}/submit` |
| GET/POST | `/api/customer-payments`; POST `/{id}/submit` |

## Approvals
| Method | Route | Role |
|--------|-------|------|
| GET | `/api/approvals?type=&status=pending` | Owner/SubOwner(permitted) |
| POST | `/api/approvals/{id}/approve` `{ remarks, allowOverpayment? }` | Owner |
| POST | `/api/approvals/{id}/reject` `{ remarks }` | Owner |
| GET | `/api/approvals/{id}/history` | Owner |

## Users (Owner / `users.manage`)
| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/users` | list (name, email, role, active, extra permissions, site ids) |
| GET | `/api/users/permission-keys` | all assignable permission keys |
| POST | `/api/users` | `{ name, email, password, role }` |
| PUT | `/api/users/{id}` | `{ name, role, isActive }` — last-Owner + self-lockout guarded |
| POST | `/api/users/{id}/password` | `{ password }` (≥8), revokes refresh token |
| PUT | `/api/users/{id}/permissions` | `{ permissions: [] }` — replaces Sub-Owner overrides |
| PUT | `/api/users/{id}/sites` | `{ siteIds: [] }` — Supervisor site scoping |

## Dashboard & Reports
| Method | Route |
|--------|-------|
| GET | `/api/dashboard` — role-aware payload. Needs `dashboard.view` (Owner, Sub-Owner, Accountant — **not** Supervisor). 403 otherwise. |
| GET | `/api/reports/*` — all need `reports.view` (Owner, Sub-Owner, Accountant — **not** Supervisor). |
| GET | `/api/reports/inventory/stock|valuation|ledger|purchase-register|consumption|low-stock` |
| GET | `/api/reports/project/cost-summary|expense-detail|budget-vs-actual|profitability` |
| GET | `/api/reports/contractor/ledger|outstanding` |
| GET | `/api/reports/customer/ledger|outstanding` |
| GET | `/api/reports/company/purchase-summary|expense-summary|inventory-value` |
| `?format=csv` on any report for export |

## Attachments
| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/attachments?entityType=&entityId=` | list |
| POST | `/api/attachments` | multipart form: `entityType`, `entityId`, `file` (≤15 MB; pdf/image/office/csv/txt) |
| GET | `/api/attachments/{id}/download` | streams the file |
| DELETE | `/api/attachments/{id}` | |


## Platform console (EnterpriseAdmin only)
Every route is `[PlatformOnly]` — a company token gets **403**. None of them returns business data.

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/platform/companies?q=` | every tenant: licence state, counts, admin logins |
| GET | `/api/platform/companies/{id}` | one tenant |
| PUT | `/api/platform/companies/{id}/license` | `{expiresOn, notes?}` — exact date |
| POST | `/api/platform/companies/{id}/license/extend` | `{days}` — extends from today if already lapsed |
| PUT | `/api/platform/companies/{id}/active` | `{isActive}` — suspend / reactivate |
| POST | `/api/platform/companies/{id}/reset-password` | `{userId, newPassword, confirmPassword}` — also revokes live sessions |
| POST | `/api/platform/change-password` | the operator's own password |

## Tenant gates
Every company endpoint carries `[TenantOnly]`, which answers before the action runs:

| Status | When |
|--------|------|
| 401 | not signed in · account deactivated · token predates a password reset |
| 402 | the company's licence has expired |
| 403 | a platform token · a suspended company |
