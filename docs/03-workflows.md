# 03 — Workflows

## A. Approval engine (generic)

```
create (Draft) ─ submit ─► Submitted/PendingApproval ─ Owner decision ─┬─ Approve ─► Approved ─ post ─► Posted
                                                                        └─ Reject  ─► Rejected
any pre-post state ─ cancel ─► Cancelled
```

- `IApprovalService.Submit(entityType, id)` creates `ApprovalRequest`, sets entity `Status=PendingApproval`.
- `IApprovalService.Decide(approvalRequestId, approve, remarks)` — Owner/permitted only. On approve,
  calls the registered `IApprovalHandler` for that `entityType` to run side effects **inside one
  `IUnitOfWork` transaction**, then sets `Status=Posted` (or `Approved`+`Issued` for requests).
- Every transition writes `ApprovalHistory`. Backend enforces role; UI only hides buttons.

## B. Inventory flow

### Purchase (adds stock)
```
Supervisor/Accountant create PurchaseHeader+Items (Draft)
  └─ optional approval (Setting: PurchaseNeedsApproval)
On post:
  for each item → InventoryTransaction(PurchaseReceipt, +qty, rate)
                → InventoryBalance recomputed (weighted avg)
  PurchaseHeader.Status = Posted; payable tracked (PaidAmount/BalanceAmount)
```
A purchase targets **site inventory**. It is NOT a project expense. `ProjectId` on a purchase is
informational only.

### Material request from stock (Scenario A)
```
Supervisor: MaterialRequest(FromStock) + items → Submit
Owner: Approve  (reject stops here)
Issue: validate stock ≥ approvedQty (or AllowNegativeStock)
  → InventoryTransaction(ProjectConsumption, −qty, rate = current weighted-avg)
  → InventoryBalance reduced
  → ProjectExpense(ExpenseType=Material, amount = qty×rate, SourceType=InventoryTransaction)
  → request Status = Issued / PartiallyIssued
```
Inventory is **never** reduced before approval.

### Material request needing purchase (Scenario B)
```
Supervisor: MaterialRequest(Purchase) + items → Submit
Owner: Approve
Accountant: create PurchaseHeader linked to request → post → stock enters site inventory
Then: issue to project as in Scenario A
Traceability chain: MaterialRequest → PurchaseHeader → InventoryTransaction(receipt)
                    → InventoryTransaction(consumption) → ProjectExpense
```

### Returns / adjustments / wastage
Separate `InventoryTransaction` types; adjustments configurable to require approval.
Return from project also writes a negative `ProjectExpense` (reversal) for that project.

## C. Weighted-average valuation

On any positive stock movement into (SiteId, MaterialId):
```
newQty   = oldQty + inQty
newValue = oldValue + inQty * inRate
newAvg   = newValue / newQty        (guard newQty > 0)
```
On consumption/issue: `rate = current newAvg`, `Value -= qty * avg`, `Quantity -= qty`.

## D. Project cost roll-up

```
ProjectMaterialCost   = Σ ProjectExpense where ExpenseType=Material
ProjectLabourCost     = Σ LabourEntry.Amount (posted)  == Σ ProjectExpense ExpenseType=Labour
ProjectContractorCost = Σ ContractorPayment.Amount (Posted)
ProjectOtherCost      = Σ ProjectExpense where ExpenseType in (Direct,Transport,Machinery,Other)
ProjectTotalCost      = Material + Labour + Contractor + Other
ProjectRevenue        = Project.ContractSaleValue
ProjectMargin         = Revenue - ProjectTotalCost
BudgetVariance        = Project.EstimatedCost - ProjectTotalCost
```
Cost-by-head = group `ProjectExpense` by `ExpenseHeadId`.

## E. Contractor ledger
```
ContractWork.TotalPaid = Σ Posted ContractorPayment for that work
ContractWork.Balance   = ContractAmount - TotalPaid
```
Payment blocked if `Amount > Balance` unless Owner sets `AllowOverpayment` on the decision.

## F. Customer ledger
```
Received    = Σ Posted CustomerPayment for project
Outstanding = Project.ContractSaleValue - Received
```

## G. No double counting (invariant)

A purchase of ₹50,000 → inventory value ₹50,000, project cost ₹0.
After 50% consumed → project material cost ₹25,000, remaining inventory value ₹25,000.
Reports never sum "purchases" into "project cost". Company spend = purchases + direct expenses
+ labour + contractor payments (mutually exclusive buckets).

## H. Transaction numbering

`{PREFIX}-{YYYY}-{00001}` from `TransactionSequence` row locked in the write transaction.
Prefixes: `MATREQ, PUR, INV, CON, LAB, CONPAY, EXP, CUSTPAY`.
