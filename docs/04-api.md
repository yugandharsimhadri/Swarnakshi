# 04 — API Reference

Base: `/api`. All responses wrapped: `{ "success": bool, "message": string?, "data": T?, "errors": [] }`.
Auth: `Authorization: Bearer <jwt>`. Errors use the same envelope with HTTP 4xx/5xx.
List endpoints accept `?page=&pageSize=&sort=&q=` plus resource-specific filters and return
`{ items, page, pageSize, total }`.

## Auth
| Method | Route | Role | Notes |
|--------|-------|------|-------|
| POST | `/api/auth/login` | anon | → access + refresh token, user profile |
| POST | `/api/auth/refresh` | anon | rotate tokens |
| POST | `/api/auth/logout` | any | revoke refresh token |
| GET  | `/api/auth/me` | any | current user + permissions |

## Masters (Owner write; all read)
`/api/units`, `/api/material-categories`, `/api/material-subcategories`, `/api/materials`,
`/api/expense-heads`, `/api/expense-subheads`, `/api/labour-categories`, `/api/payment-methods`,
`/api/project-types`, `/api/suppliers`, `/api/contractors`, `/api/customers`, `/api/settings`
— standard `GET list`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` (delete only if unused & master).

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
| GET | `/api/dashboard` — role-aware payload (owner/supervisor/accountant) |
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
