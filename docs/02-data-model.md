# 02 — Data Model

Grouped by bounded context. All entities inherit `BaseEntity` (`Id: Guid`, `CreatedAt`,
`CreatedBy`). Transactional entities also inherit `AuditableEntity`
(`ModifiedAt/By`, `ApprovedAt/By`, `Status`, `Remarks`, `RowVersion`).

## Identity
- **User** (Id, Name, Email, PasswordHash, Role, IsActive, RefreshToken, RefreshTokenExpiry)
- **UserPermission** (UserId, PermissionKey, Granted) — overrides for SubOwner etc.
- **UserSiteAssignment** (UserId, SiteId) — Supervisor scoping

Role is an enum: `Owner, SubOwner, Supervisor, Accountant`.

## Masters (global)
- **Unit** (Code, Name, IsActive) — Nos, Bag, Kg, Ton, Cft, Cum, RM, SqFt, …
- **MaterialCategory** (Name, SortOrder, IsActive) — the approved 50-category taxonomy
- **MaterialSubcategory** (MaterialCategoryId, Name, IsActive) — unique (CategoryId, Name)
- **Material** (Code*, Name, MaterialSubcategoryId, **Brand**, Description, UnitId, SecondaryUnitId?,
  ConversionFactor?, **GenericMeasurement**, MinStockLevel, ReorderLevel, DefaultPurchaseRate,
  GstRate?, IsActive, Notes, **SpecSummary**, **SpecSignature***)
- **MaterialSpecDefinition** (MaterialSubcategoryId, Key, Label, Kind {Text|Number|Select},
  Options, IsRequired, PartOfIdentity, SortOrder, IsActive) — unique (SubcategoryId, Key)
- **MaterialSpecValue** (MaterialId, MaterialSpecDefinitionId, Value) — unique (MaterialId, DefinitionId)

### Material identity
An exact purchasable material is **Name + Brand + identity-bearing specifications**. That triple is
normalised (lowercased, whitespace-collapsed, key-sorted) into `Material.SpecSignature`, which carries
a **unique index** — duplicate prevention is a database constraint, not just a service check.
`SpecSummary` is the denormalised display form ("25 mm · Cold Water") and is what free-text search
matches on, so phrase queries work without provider-specific JSON operators.

Which specification fields apply is decided by the **subcategory** via `MaterialSpecDefinition`.
Company/Brand is a first-class Material column, never a spec field. Material never stores stock —
current stock is `InventoryBalance` per (Site, Material).
- **ExpenseHead** (Name, SortOrder, IsActive)
- **ExpenseSubhead** (ExpenseHeadId, Name, IsActive)
- **LabourCategory** (Name, IsActive)
- **PaymentMethod** (Name, IsActive)
- **ProjectType** (Name, IsActive)
- **Supplier** (Code, Name, Mobile, Email, Address, Pan, Gstin, IsActive, Notes)
- **Contractor** (Code*, Name, CompanyName, Mobile, Email, Address, Pan, Gstin, BankDetails,
  ContractorType, IsActive, Notes) — **global, not site-specific**
- **Customer** (Code*, Name, Mobile, Email, Address, Pan, Gstin, IsActive, Notes) — **global**
- **Setting** (Key, Value, SiteId?) — valuation method, AllowNegativeStock, numbering, …

\* unique, app-enforced + DB unique index.

## Sites & Projects
- **Site** (Code*, Name, Address, City, State, Pin, SupervisorUserId?, StartDate, Status, Notes)
- **Project** (Code*, Name, VillaNumber, SiteId→Site, CustomerId?→Customer, ProjectTypeId,
  Address, StartDate, ExpectedCompletionDate, ActualCompletionDate?, EstimatedCost,
  ContractSaleValue?, Status, Notes)

`Project.SiteId` immutable after inventory activity. Statuses: `Planned, Active, OnHold, Completed, Cancelled`.

## Inventory (site-level)
- **InventoryBalance** (SiteId, MaterialId, Quantity, AverageRate, Value) — unique (SiteId, MaterialId).
  Derived/maintained from ledger; never edited directly.
- **InventoryTransaction** (TxnNumber*, Date, SiteId, MaterialId, UnitId, Quantity(+/−), Rate,
  Amount, Type, ProjectId?, SourceRef, SourceType, SourceId, Remarks, +audit)
  Types: `OpeningStock, PurchaseReceipt, Transfer, ProjectConsumption, Adjustment,
  ReturnFromProject, Wastage, OtherReceipt, OtherIssue`.

Balance update is part of the same transaction that writes the ledger row.

## Procurement
- **Supplier** (see masters)
- **PurchaseOrder / PurchaseHeader** (TxnNumber*, SupplierId, SiteId, ProjectId?, InvoiceNumber,
  InvoiceDate, SubTotal, Discount, TaxAmount, OtherCharges, TotalAmount, PaidAmount, BalanceAmount,
  PaymentStatus, Status, +audit)
- **PurchaseItem** (PurchaseHeaderId, MaterialId, UnitId, Quantity, Rate, Discount, TaxAmount, LineTotal)
- **MaterialRequest** (TxnNumber*, SiteId, ProjectId→Project, RequestType {FromStock|Purchase},
  Status, RequestedByUserId, Notes, +audit)
- **MaterialRequestItem** (MaterialRequestId, MaterialId, UnitId, RequestedQty, ApprovedQty?,
  IssuedQty, Rate?)

`MaterialRequest.Status`: `Draft, Submitted, PendingApproval, Approved, Rejected, Issued,
PartiallyIssued, Cancelled`. Owner approval mandatory before any issue.

## Expenses
- **ProjectExpense** (TxnNumber*, ProjectId, Date, ExpenseHeadId, ExpenseSubheadId?, Description,
  Amount, ExpenseType {Material|Labour|Contractor|Direct|Transport|Machinery|Other},
  PaymentStatus, PaymentMethodId?, SourceType, SourceId, +audit)
  — one row per costed event; material consumption & posted payments create these automatically.
- **LabourEntry** (TxnNumber*, ProjectId, LabourCategoryId, Date, PeriodType {Daily|Weekly|Custom},
  PeriodStart, PeriodEnd, Amount, PaymentType, PaymentMethodId?, Remarks, +audit) — no worker master.

## Contractors
- **ContractWork** (ProjectId, ContractorId, WorkCategory, Description, EstimatedCost, ContractAmount,
  StartDate, ExpectedCompletion, ActualCompletion?, PaymentTerms, Status, TotalPaid, Balance)
  Status: `Planned, Active, Completed, Cancelled, OnHold`.
- **ContractorPayment** (TxnNumber*, ContractorId, ProjectId, ContractWorkId?, Date, Amount,
  PaymentMethodId, ReferenceNumber, Description, PaymentKind {Advance|Partial|Final|Adjustment},
  Status, +audit)

## Customers
- **CustomerPayment** (TxnNumber*, ProjectId, CustomerId, Date, Amount, PaymentMethodId, Reference,
  Description, Status, +audit)

## Approval & audit
- **ApprovalRequest** (EntityType, EntityId, CurrentStatus, RequestedByUserId, RequestedAt,
  DecidedByUserId?, DecidedAt?, Remarks)
- **ApprovalHistory** (ApprovalRequestId, Action, PreviousStatus, NewStatus, UserId, At, Remarks)
- **AuditLog** (EntityType, EntityId, Action, DataJson, UserId, At)
- **TransactionSequence** (Prefix, Year, LastNumber) — unique (Prefix, Year)
- **Attachment** (EntityType, EntityId, FileName, ContentType, Size, StoragePath, UploadedByUserId)

## Key relationship rules (enforced DB + app)

| Rule |
|------|
| Site 1—* Project |
| Site 1—* InventoryBalance / InventoryTransaction (project optional on txn) |
| Project *—1 Site (required), *—0..1 Customer |
| Contractor, Customer, Material, Unit — global, no SiteId |
| InventoryBalance unique (SiteId, MaterialId) |
| Consumption txn requires ProjectId whose SiteId == txn.SiteId |
| ContractorPayment.ContractWork (if set) must belong to same Project + Contractor |
| CustomerPayment.Customer must equal Project.Customer |
| No negative InventoryBalance.Quantity unless Setting AllowNegativeStock |
| Codes unique: Material, Project, Contractor, Customer, Site, Supplier |
| Posted financial rows immutable (reverse via new row) |
