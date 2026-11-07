using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Contractors;

// ---- Contract work ---------------------------------------------------
// A work order carries the amount that was agreed with the contractor, and nothing else. There used
// to be an estimate beside it, which no report ever read and no screen ever compared against: two
// figures where one is the truth is an invitation to pay against the wrong one.
public record ContractWorkDto(Guid Id, Guid ProjectId, string ProjectName, Guid ContractorId, string ContractorName,
    string WorkCategory, string? Description, decimal ContractAmount,
    DateOnly? StartDate, DateOnly? ExpectedCompletion, ContractWorkStatus WorkStatus,
    decimal TotalPaid, decimal Balance);

public record SaveContractWorkRequest(Guid ProjectId, Guid ContractorId, string WorkCategory, string? Description,
    decimal ContractAmount, DateOnly? StartDate, DateOnly? ExpectedCompletion,
    string? PaymentTerms, ContractWorkStatus WorkStatus);

public class SaveContractWorkValidator : AbstractValidator<SaveContractWorkRequest>
{
    public SaveContractWorkValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ContractorId).NotEmpty();
        RuleFor(x => x.WorkCategory).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ContractAmount).GreaterThan(0);
    }
}

public interface IContractWorkService
{
    Task<PagedResult<ContractWorkDto>> ListAsync(PageQuery page, Guid? projectId, Guid? contractorId, ContractWorkStatus? status, CancellationToken ct = default);
    Task<ContractWorkDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ContractWorkDto> CreateAsync(SaveContractWorkRequest req, CancellationToken ct = default);
    Task<ContractWorkDto> UpdateAsync(Guid id, SaveContractWorkRequest req, CancellationToken ct = default);
}

public class ContractWorkService(IAppDbContext db, IValidator<SaveContractWorkRequest> validator) : IContractWorkService
{
    public async Task<PagedResult<ContractWorkDto>> ListAsync(PageQuery page, Guid? projectId, Guid? contractorId, ContractWorkStatus? status, CancellationToken ct = default)
    {
        var q = db.ContractWorks.AsNoTracking();
        if (projectId is not null) q = q.Where(c => c.ProjectId == projectId);
        if (contractorId is not null) q = q.Where(c => c.ContractorId == contractorId);
        if (status is not null) q = q.Where(c => c.WorkStatus == status);
        return await q.OrderByDescending(c => c.CreatedAt).Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<ContractWorkDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.ContractWorks.AsNoTracking().Where(c => c.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("ContractWork", id);

    public async Task<ContractWorkDto> CreateAsync(SaveContractWorkRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        if (!await db.Projects.AnyAsync(p => p.Id == req.ProjectId, ct)) throw new NotFoundException("Project", req.ProjectId);
        if (!await db.Contractors.AnyAsync(c => c.Id == req.ContractorId && c.IsActive, ct))
            throw new AppException("Contractor not found or inactive.", 400);

        var work = new ContractWork
        {
            ProjectId = req.ProjectId, ContractorId = req.ContractorId, WorkCategory = req.WorkCategory,
            Description = req.Description, ContractAmount = req.ContractAmount,
            StartDate = req.StartDate, ExpectedCompletion = req.ExpectedCompletion, PaymentTerms = req.PaymentTerms,
            WorkStatus = req.WorkStatus, TotalPaid = 0m, Balance = req.ContractAmount,
            Status = TransactionStatus.Posted
        };
        db.ContractWorks.Add(work);
        await db.SaveChangesAsync(ct);
        return await GetAsync(work.Id, ct);
    }

    public async Task<ContractWorkDto> UpdateAsync(Guid id, SaveContractWorkRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var work = await db.ContractWorks.FirstOrDefaultAsync(c => c.Id == id, ct)
                   ?? throw new NotFoundException("ContractWork", id);
        if (req.ContractAmount < work.TotalPaid)
            throw new AppException($"Contract amount cannot be less than amount already paid ({work.TotalPaid:0.00}).", 409);

        work.WorkCategory = req.WorkCategory; work.Description = req.Description;
        work.ContractAmount = req.ContractAmount; work.StartDate = req.StartDate;
        work.ExpectedCompletion = req.ExpectedCompletion; work.PaymentTerms = req.PaymentTerms; work.WorkStatus = req.WorkStatus;
        work.Balance = work.ContractAmount - work.TotalPaid;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static readonly Expression<Func<ContractWork, ContractWorkDto>> Projection = c => new ContractWorkDto(
        c.Id, c.ProjectId, c.Project.Name, c.ContractorId, c.Contractor.Name, c.WorkCategory, c.Description,
        c.ContractAmount, c.StartDate, c.ExpectedCompletion, c.WorkStatus, c.TotalPaid, c.Balance);
}

// ---- Contractor payments -------------------------------------------
public record ContractorPaymentDto(Guid Id, string TxnNumber, Guid ContractorId, string ContractorName,
    Guid ProjectId, string ProjectName, Guid? ContractWorkId, DateOnly Date, decimal Amount,
    Guid PaymentMethodId, string PaymentMethodName, string? ReferenceNumber, string? Description,
    ContractorPaymentKind PaymentKind, TransactionStatus Status,
    ContractorPaymentChangeKind? PendingChange = null);

public record SaveContractorPaymentRequest(Guid ContractorId, Guid ProjectId, Guid? ContractWorkId, DateOnly Date,
    decimal Amount, Guid PaymentMethodId, string? ReferenceNumber, string? Description, ContractorPaymentKind PaymentKind);

public record ReasonRequest(string Reason);

/// <summary>The corrected payment, and why. Contractor and villa are not changeable: see <see cref="IContractorPaymentService.RequestChangeAsync"/>.</summary>
public record ChangeContractorPaymentRequest(SaveContractorPaymentRequest Payment, string Reason);

/// <summary>What a payment looks like, in the words an approver reads.</summary>
public record PaymentFacts(DateOnly Date, decimal Amount, string Method, string? Reference, string? Work,
    string? Description, ContractorPaymentKind Kind);

/// <summary>
/// A change waiting to be approved: what the payment is now beside what it would become. The
/// "before" is read from the payment itself, which cannot move while a change is pending, so it is
/// only the right comparison until the change is decided.
/// </summary>
public record ContractorPaymentChangeDto(Guid Id, string TxnNumber, ContractorPaymentChangeKind Kind, string Reason,
    Guid ContractorPaymentId, string PaymentTxnNumber, string ContractorName, string ProjectName,
    string? RequestedBy, DateTimeOffset RequestedAt, TransactionStatus Status,
    PaymentFacts Before, PaymentFacts After);

public class SaveContractorPaymentValidator : AbstractValidator<SaveContractorPaymentRequest>
{
    public SaveContractorPaymentValidator()
    {
        RuleFor(x => x.ContractorId).NotEmpty();
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public record ContractorLedgerRow(string Kind, string Ref, DateOnly Date, decimal Contracted, decimal Paid);
public record ContractorSummary(Guid ContractorId, string ContractorName, decimal TotalContracted,
    decimal TotalPaid, decimal Outstanding, IReadOnlyList<ContractorLedgerRow> Rows);

public interface IContractorPaymentService
{
    Task<PagedResult<ContractorPaymentDto>> ListAsync(PageQuery page, Guid? projectId, Guid? contractorId, TransactionStatus? status, CancellationToken ct = default);
    Task<ContractorPaymentDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ContractorPaymentDto> CreateAsync(SaveContractorPaymentRequest req, CancellationToken ct = default);
    Task<ContractorPaymentDto> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<ContractorSummary> LedgerAsync(Guid contractorId, CancellationToken ct = default);

    /// <summary>Corrects a payment nobody has acted on yet — a draft, or one the Owner sent back.</summary>
    Task<ContractorPaymentDto> UpdateAsync(Guid id, SaveContractorPaymentRequest req, CancellationToken ct = default);

    /// <summary>Withdraws a draft, or one the Owner sent back. Nothing was posted, so nothing needs approving.</summary>
    Task<ContractorPaymentDto> CancelAsync(Guid id, string reason, CancellationToken ct = default);

    /// <summary>
    /// Asks for a posted payment to be corrected. The payment is untouched until an Owner approves;
    /// then it is rewritten and the contract balance and the villa's cost follow it.
    ///
    /// <para>The contractor and the villa cannot be changed this way. Moving money from one villa to
    /// another changes two villas' costs and is a different decision from fixing a typo; the honest
    /// route is to delete the payment and enter it again against the right one.</para>
    /// </summary>
    Task<ContractorPaymentDto> RequestChangeAsync(Guid id, SaveContractorPaymentRequest req, string reason, CancellationToken ct = default);

    /// <summary>Asks for a posted payment to be removed. Voided on approval, never deleted.</summary>
    Task<ContractorPaymentDto> RequestDeleteAsync(Guid id, string reason, CancellationToken ct = default);

    Task<ContractorPaymentChangeDto> GetChangeAsync(Guid changeId, CancellationToken ct = default);
}

public class ContractorPaymentService(
    IAppDbContext db, IApprovalService approvals, ITransactionSequenceService sequences,
    IValidator<SaveContractorPaymentRequest> validator) : IContractorPaymentService
{
    public async Task<PagedResult<ContractorPaymentDto>> ListAsync(PageQuery page, Guid? projectId, Guid? contractorId, TransactionStatus? status, CancellationToken ct = default)
    {
        var q = db.ContractorPayments.AsNoTracking();
        if (projectId is not null) q = q.Where(p => p.ProjectId == projectId);
        if (contractorId is not null) q = q.Where(p => p.ContractorId == contractorId);
        if (status is not null) q = q.Where(p => p.Status == status);
        return await q.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt)
            .Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<ContractorPaymentDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.ContractorPayments.AsNoTracking().Where(p => p.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("ContractorPayment", id);

    public async Task<ContractorPaymentDto> CreateAsync(SaveContractorPaymentRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        if (!await db.Projects.AnyAsync(p => p.Id == req.ProjectId, ct)) throw new NotFoundException("Project", req.ProjectId);
        if (!await db.Contractors.AnyAsync(c => c.Id == req.ContractorId && c.IsActive, ct))
            throw new AppException("Contractor not found or inactive.", 400);
        await EnsureWorkMatchesAsync(req, ct);

        var payment = new ContractorPayment
        {
            TxnNumber = await sequences.NextAsync("CONPAY", ct),
            ContractorId = req.ContractorId, ProjectId = req.ProjectId, ContractWorkId = req.ContractWorkId,
            Date = req.Date, Amount = req.Amount, PaymentMethodId = req.PaymentMethodId,
            ReferenceNumber = req.ReferenceNumber, Description = req.Description, PaymentKind = req.PaymentKind,
            Status = TransactionStatus.Draft
        };
        db.ContractorPayments.Add(payment);
        await db.SaveChangesAsync(ct);
        return await GetAsync(payment.Id, ct);
    }

    public async Task<ContractorPaymentDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var payment = await db.ContractorPayments.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("ContractorPayment", id);
        // A rejected payment goes round again once it has been corrected.
        if (payment.Status is not (TransactionStatus.Draft or TransactionStatus.Rejected))
            throw new AppException($"Payment is already {payment.Status}.", 409);
        payment.Status = TransactionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);
        await approvals.SubmitAsync(ApprovalEntityTypes.ContractorPayment, payment.Id, payment.TxnNumber,
            null, payment.ProjectId, payment.Amount, ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContractorPaymentDto> UpdateAsync(Guid id, SaveContractorPaymentRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var payment = await db.ContractorPayments.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("ContractorPayment", id);
        if (payment.Status is not (TransactionStatus.Draft or TransactionStatus.Rejected))
            throw new AppException(WhyNotDirect(payment), 409);
        EnsureSameContractorAndVilla(payment, req);
        await EnsureWorkMatchesAsync(req, ct);

        payment.ContractWorkId = req.ContractWorkId; payment.Date = req.Date; payment.Amount = req.Amount;
        payment.PaymentMethodId = req.PaymentMethodId; payment.ReferenceNumber = req.ReferenceNumber;
        payment.Description = req.Description; payment.PaymentKind = req.PaymentKind;
        payment.Status = TransactionStatus.Draft; // a sent-back payment is a draft again
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContractorPaymentDto> CancelAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var payment = await db.ContractorPayments.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("ContractorPayment", id);
        if (payment.Status is not (TransactionStatus.Draft or TransactionStatus.Rejected))
            throw new AppException(WhyNotDirect(payment), 409);
        payment.Status = TransactionStatus.Cancelled;
        payment.Remarks = reason;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContractorPaymentDto> RequestChangeAsync(Guid id, SaveContractorPaymentRequest req, string reason, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var payment = await LoadPostedForChangeAsync(id, ct);
        EnsureSameContractorAndVilla(payment, req);
        await EnsureWorkMatchesAsync(req, ct);

        var unchanged = payment.Amount == req.Amount && payment.Date == req.Date
            && payment.ContractWorkId == req.ContractWorkId && payment.PaymentMethodId == req.PaymentMethodId
            && payment.PaymentKind == req.PaymentKind
            && (payment.ReferenceNumber ?? "") == (req.ReferenceNumber ?? "")
            && (payment.Description ?? "") == (req.Description ?? "");
        if (unchanged) throw new AppException("Nothing is different from the payment as it stands.", 400);

        await RaiseAsync(payment, ContractorPaymentChangeKind.Edit, reason, req, ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContractorPaymentDto> RequestDeleteAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var payment = await LoadPostedForChangeAsync(id, ct);
        var asItStands = new SaveContractorPaymentRequest(payment.ContractorId, payment.ProjectId, payment.ContractWorkId,
            payment.Date, payment.Amount, payment.PaymentMethodId, payment.ReferenceNumber, payment.Description, payment.PaymentKind);
        await RaiseAsync(payment, ContractorPaymentChangeKind.Delete, reason, asItStands, ct);
        return await GetAsync(id, ct);
    }

    public async Task<ContractorPaymentChangeDto> GetChangeAsync(Guid changeId, CancellationToken ct = default)
    {
        var c = await db.ContractorPaymentChanges.AsNoTracking().Where(x => x.Id == changeId)
            .Select(x => new
            {
                x.Id, x.TxnNumber, x.Kind, x.Reason, x.ContractorPaymentId, x.Status, x.CreatedAt,
                x.PreviousAmount, x.PreviousDate,
                PaymentTxn = x.ContractorPayment.TxnNumber,
                Contractor = x.ContractorPayment.Contractor.Name,
                Project = x.ContractorPayment.Project.Name,
                NowMethod = x.ContractorPayment.PaymentMethod.Name,
                NowReference = x.ContractorPayment.ReferenceNumber,
                NowDescription = x.ContractorPayment.Description,
                NowKind = x.ContractorPayment.PaymentKind,
                NowWork = x.ContractorPayment.ContractWork == null ? null : x.ContractorPayment.ContractWork.WorkCategory,
                x.Date, x.Amount, x.ReferenceNumber, x.Description, x.PaymentKind,
                Method = db.PaymentMethods.Where(m => m.Id == x.PaymentMethodId).Select(m => m.Name).FirstOrDefault(),
                Work = db.ContractWorks.Where(w => w.Id == x.ContractWorkId).Select(w => w.WorkCategory).FirstOrDefault(),
                By = db.Users.Where(u => u.Id == x.CreatedBy).Select(u => u.Name).FirstOrDefault()
            }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("ContractorPaymentChange", changeId);

        return new ContractorPaymentChangeDto(c.Id, c.TxnNumber, c.Kind, c.Reason, c.ContractorPaymentId,
            c.PaymentTxn, c.Contractor, c.Project, c.By, c.CreatedAt, c.Status,
            new PaymentFacts(c.PreviousDate, c.PreviousAmount, c.NowMethod, c.NowReference, c.NowWork, c.NowDescription, c.NowKind),
            new PaymentFacts(c.Date, c.Amount, c.Method ?? "", c.ReferenceNumber, c.Work, c.Description, c.PaymentKind));
    }

    private async Task<ContractorPayment> LoadPostedForChangeAsync(Guid id, CancellationToken ct)
    {
        var payment = await db.ContractorPayments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("ContractorPayment", id);
        if (payment.Status != TransactionStatus.Posted)
            throw new AppException(
                payment.Status is TransactionStatus.Draft or TransactionStatus.Rejected
                    ? "This payment has not been posted yet: edit or withdraw it directly."
                    : WhyNotDirect(payment), 409);
        if (await db.ContractorPaymentChanges.AnyAsync(
                c => c.ContractorPaymentId == id && c.Status == TransactionStatus.PendingApproval, ct))
            throw new AppException("A change to this payment is already waiting for approval.", 409);
        return payment;
    }

    private async Task RaiseAsync(ContractorPayment payment, ContractorPaymentChangeKind kind, string reason,
        SaveContractorPaymentRequest proposed, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new AppException("Say why — the Owner has to approve this.", 400);
        if (reason.Trim().Length > 500) throw new AppException("The reason is too long (500 characters at most).", 400);

        var change = new ContractorPaymentChange
        {
            TxnNumber = await sequences.NextAsync("CPCHG", ct),
            ContractorPaymentId = payment.Id, Kind = kind, Reason = reason.Trim(),
            PreviousAmount = payment.Amount, PreviousDate = payment.Date,
            ContractWorkId = proposed.ContractWorkId, Date = proposed.Date, Amount = proposed.Amount,
            PaymentMethodId = proposed.PaymentMethodId, ReferenceNumber = proposed.ReferenceNumber,
            Description = proposed.Description, PaymentKind = proposed.PaymentKind,
            Status = TransactionStatus.PendingApproval
        };
        db.ContractorPaymentChanges.Add(change);
        await db.SaveChangesAsync(ct);

        var siteId = await db.Projects.AsNoTracking().Where(p => p.Id == payment.ProjectId)
            .Select(p => (Guid?)p.SiteId).FirstOrDefaultAsync(ct);

        // allowAutoApprove: false — the limit exists so the Owner is not asked to stamp small new
        // invoices. A change to money already paid out is exactly what they want to see.
        await approvals.SubmitAsync(ApprovalEntityTypes.ContractorPaymentChange, change.Id,
            $"{payment.TxnNumber} · {(kind == ContractorPaymentChangeKind.Edit ? "change" : "delete")}",
            siteId, payment.ProjectId, kind == ContractorPaymentChangeKind.Edit ? proposed.Amount : payment.Amount,
            ct, allowAutoApprove: false);
    }

    private static string WhyNotDirect(ContractorPayment p) => p.Status switch
    {
        TransactionStatus.PendingApproval => "This payment is waiting for approval. Ask the Owner to reject it, then correct it.",
        TransactionStatus.Posted => "This payment has been posted. Ask for a change instead, and the Owner will approve it.",
        _ => $"This payment is {p.Status} and cannot be changed."
    };

    private static void EnsureSameContractorAndVilla(ContractorPayment payment, SaveContractorPaymentRequest req)
    {
        if (req.ContractorId != payment.ContractorId || req.ProjectId != payment.ProjectId)
            throw new AppException(
                "A payment cannot be moved to another contractor or villa. Delete it and enter it against the right one.", 409);
    }

    private async Task EnsureWorkMatchesAsync(SaveContractorPaymentRequest req, CancellationToken ct)
    {
        if (req.ContractWorkId is not { } wid) return;
        var work = await db.ContractWorks.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wid, ct)
                   ?? throw new NotFoundException("ContractWork", wid);
        if (work.ProjectId != req.ProjectId || work.ContractorId != req.ContractorId)
            throw new AppException("The selected contract does not match this project/contractor.", 409);
    }

    public async Task<ContractorSummary> LedgerAsync(Guid contractorId, CancellationToken ct = default)
    {
        var contractor = await db.Contractors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractorId, ct)
                         ?? throw new NotFoundException("Contractor", contractorId);

        var works = await db.ContractWorks.AsNoTracking().Where(w => w.ContractorId == contractorId)
            .Select(w => new { w.WorkCategory, w.StartDate, w.ContractAmount }).ToListAsync(ct);
        var payments = await db.ContractorPayments.AsNoTracking()
            .Where(p => p.ContractorId == contractorId && p.Status == TransactionStatus.Posted)
            .Select(p => new { p.TxnNumber, p.Date, p.Amount }).ToListAsync(ct);

        var rows = works.Select(w => new ContractorLedgerRow("Contract", w.WorkCategory, w.StartDate ?? default, w.ContractAmount, 0m))
            .Concat(payments.Select(p => new ContractorLedgerRow("Payment", p.TxnNumber, p.Date, 0m, p.Amount)))
            .OrderBy(r => r.Date).ToList();

        var contracted = works.Sum(w => w.ContractAmount);
        var paid = payments.Sum(p => p.Amount);
        return new ContractorSummary(contractor.Id, contractor.Name, contracted, paid, contracted - paid, rows);
    }

    // The change a payment is waiting on, if any, rides along with every row so the screen can say
    // so without a second request per payment.
    private Expression<Func<ContractorPayment, ContractorPaymentDto>> Projection => p => new ContractorPaymentDto(
        p.Id, p.TxnNumber, p.ContractorId, p.Contractor.Name, p.ProjectId, p.Project.Name, p.ContractWorkId,
        p.Date, p.Amount, p.PaymentMethodId, p.PaymentMethod.Name, p.ReferenceNumber, p.Description, p.PaymentKind, p.Status,
        db.ContractorPaymentChanges
            .Where(c => c.ContractorPaymentId == p.Id && c.Status == TransactionStatus.PendingApproval)
            .Select(c => (ContractorPaymentChangeKind?)c.Kind).FirstOrDefault());
}

public class ContractorPaymentApprovalHandler(IAppDbContext db, IProjectCostWriter costWriter) : IApprovalHandler
{
    public string EntityType => ApprovalEntityTypes.ContractorPayment;

    public async Task OnApprovedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var payment = await db.ContractorPayments.FirstOrDefaultAsync(p => p.Id == entityId, ct)
                      ?? throw new NotFoundException("ContractorPayment", entityId);
        if (payment.Status == TransactionStatus.Posted) return;

        ContractWork? work = null;
        if (payment.ContractWorkId is { } wid)
        {
            work = await db.ContractWorks.FirstOrDefaultAsync(w => w.Id == wid, ct);
            if (work is not null && payment.Amount > work.Balance && !decision.AllowOverride)
                throw new AppException(
                    $"Payment {payment.Amount:0.00} exceeds contract balance {work.Balance:0.00}. Approve with override to allow an advance/overpayment.", 409);
        }

        payment.Status = TransactionStatus.Posted;
        payment.ApprovedBy = decidedBy;
        payment.ApprovedAt = DateTimeOffset.UtcNow;

        if (work is not null)
        {
            work.TotalPaid += payment.Amount;
            work.Balance = work.ContractAmount - work.TotalPaid;
        }
        await db.SaveChangesAsync(ct);

        var contractorHead = await db.ExpenseHeads.Where(h => h.Name == "Miscellaneous").Select(h => (Guid?)h.Id).FirstOrDefaultAsync(ct);
        await costWriter.WriteAsync(payment.ProjectId, ProjectExpenseType.Contractor, payment.Amount, payment.Date,
            contractorHead, null, $"Contractor payment: {payment.TxnNumber}", "ContractorPayment", payment.Id, ct);
    }

    public async Task OnRejectedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var payment = await db.ContractorPayments.FirstOrDefaultAsync(p => p.Id == entityId, ct);
        if (payment is null) return;
        payment.Status = TransactionStatus.Rejected;
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Applies an approved change to a posted contractor payment. Runs inside the approval transaction,
/// so the payment, the contract's paid figure and the villa's cost row move together or not at all.
///
/// <para>The cost row is rewritten rather than a second one added. A villa's cost is the sum of its
/// expense rows and each payment owns exactly one; a correction written as a new row beside the old
/// one is how the same payment gets counted twice.</para>
/// </summary>
public class ContractorPaymentChangeApprovalHandler(IAppDbContext db) : IApprovalHandler
{
    public string EntityType => ApprovalEntityTypes.ContractorPaymentChange;

    public async Task OnApprovedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var change = await db.ContractorPaymentChanges.FirstOrDefaultAsync(c => c.Id == entityId, ct)
                     ?? throw new NotFoundException("ContractorPaymentChange", entityId);
        var payment = await db.ContractorPayments.FirstOrDefaultAsync(p => p.Id == change.ContractorPaymentId, ct)
                      ?? throw new NotFoundException("ContractorPayment", change.ContractorPaymentId);
        if (payment.Status != TransactionStatus.Posted)
            throw new AppException($"This payment is {payment.Status} and can no longer be changed.", 409);

        var cost = await db.ProjectExpenses.FirstOrDefaultAsync(
            e => e.SourceType == "ContractorPayment" && e.SourceId == payment.Id
                 && e.Status == TransactionStatus.Posted, ct);

        // Take the payment off the contract it is on, then put it back wherever it now belongs.
        if (await WorkAsync(payment.ContractWorkId, ct) is { } current)
        {
            current.TotalPaid -= payment.Amount;
            current.Balance = current.ContractAmount - current.TotalPaid;
        }

        if (change.Kind == ContractorPaymentChangeKind.Delete)
        {
            payment.Status = TransactionStatus.Cancelled;
            payment.Remarks = change.Reason;
            payment.Amount = 0m; // the row stays for audit; the figure it held is on the change
            if (cost is not null)
            {
                cost.Status = TransactionStatus.Cancelled;
                cost.Remarks = change.Reason;
                cost.Amount = 0m;
            }
        }
        else
        {
            if (await WorkAsync(change.ContractWorkId, ct) is { } target)
            {
                if (change.Amount > target.Balance && !decision.AllowOverride)
                    throw new AppException(
                        $"Payment {change.Amount:0.00} exceeds contract balance {target.Balance:0.00}. Approve with override to allow an advance/overpayment.", 409);
                target.TotalPaid += change.Amount;
                target.Balance = target.ContractAmount - target.TotalPaid;
            }

            payment.ContractWorkId = change.ContractWorkId; payment.Date = change.Date; payment.Amount = change.Amount;
            payment.PaymentMethodId = change.PaymentMethodId; payment.ReferenceNumber = change.ReferenceNumber;
            payment.Description = change.Description; payment.PaymentKind = change.PaymentKind;
            if (cost is not null)
            {
                cost.Amount = change.Amount;
                cost.Date = change.Date;
            }
        }

        change.Status = TransactionStatus.Posted;
        change.ApprovedBy = decidedBy;
        change.ApprovedAt = DateTimeOffset.UtcNow;
        change.Remarks = decision.Remarks;
        await db.SaveChangesAsync(ct);
    }

    public async Task OnRejectedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var change = await db.ContractorPaymentChanges.FirstOrDefaultAsync(c => c.Id == entityId, ct);
        if (change is null) return;
        change.Status = TransactionStatus.Rejected;
        change.Remarks = decision.Remarks;
        await db.SaveChangesAsync(ct);
    }

    // Found through the change tracker's identity map, so the same contract loaded twice is one
    // object and releasing and re-adding on it see each other's arithmetic.
    private async Task<ContractWork?> WorkAsync(Guid? id, CancellationToken ct)
        => id is { } wid ? await db.ContractWorks.FirstOrDefaultAsync(w => w.Id == wid, ct) : null;
}
