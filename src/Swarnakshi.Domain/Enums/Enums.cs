namespace Swarnakshi.Domain.Enums;

public enum UserRole { Owner = 1, SubOwner = 2, Supervisor = 3, Accountant = 4 }

/// <summary>Generic lifecycle status for approvable / transactional entities.</summary>
public enum TransactionStatus
{
    Draft = 0,
    Submitted = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    Posted = 6
}

public enum SiteStatus { Planned = 0, Active = 1, OnHold = 2, Completed = 3, Cancelled = 4 }

public enum ProjectStatus { Planned = 0, Active = 1, OnHold = 2, Completed = 3, Cancelled = 4 }

public enum MaterialRequestStatus
{
    Draft = 0,
    Submitted = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Issued = 5,
    PartiallyIssued = 6,
    Cancelled = 7
}

public enum MaterialRequestType { FromStock = 1, Purchase = 2 }

public enum InventoryTransactionType
{
    OpeningStock = 1,
    PurchaseReceipt = 2,
    Transfer = 3,
    ProjectConsumption = 4,
    Adjustment = 5,
    ReturnFromProject = 6,
    Wastage = 7,
    OtherReceipt = 8,
    OtherIssue = 9
}

public enum PaymentStatus { Unpaid = 0, PartiallyPaid = 1, Paid = 2 }

public enum ProjectExpenseType
{
    Material = 1,
    Labour = 2,
    Contractor = 3,
    Direct = 4,
    Transport = 5,
    Machinery = 6,
    Other = 7
}

public enum LabourPeriodType { Daily = 1, Weekly = 2, Custom = 3 }

public enum ContractWorkStatus { Planned = 0, Active = 1, Completed = 2, Cancelled = 3, OnHold = 4 }

public enum ContractorPaymentKind { Advance = 1, Partial = 2, Final = 3, Adjustment = 4 }

public enum InventoryValuationMethod { WeightedAverage = 1, Fifo = 2, ManualRate = 3 }

public enum ApprovalAction { Submitted = 1, Approved = 2, Rejected = 3, Cancelled = 4, Posted = 5, Reopened = 6 }

/// <summary>Input control a material specification field renders as.</summary>
public enum SpecFieldKind { Text = 1, Number = 2, Select = 3 }

/// <summary>What a payment to an employee is for.</summary>
public enum EmployeePaymentKind
{
    Salary = 1,
    /// <summary>Money advanced against future salary — recovered by later salary payments.</summary>
    Advance = 2,
    Bonus = 3,
    Reimbursement = 4
}
