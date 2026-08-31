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
public record ContractWorkDto(Guid Id, Guid ProjectId, string ProjectName, Guid ContractorId, string ContractorName,
    string WorkCategory, string? Description, decimal EstimatedCost, decimal ContractAmount,
    DateOnly? StartDate, DateOnly? ExpectedCompletion, ContractWorkStatus WorkStatus,
    decimal TotalPaid, decimal Balance);

public record SaveContractWorkRequest(Guid ProjectId, Guid ContractorId, string WorkCategory, string? Description,
    decimal EstimatedCost, decimal ContractAmount, DateOnly? StartDate, DateOnly? ExpectedCompletion,
    string? PaymentTerms, ContractWorkStatus WorkStatus);

public class SaveContractWorkValidator : AbstractValidator<SaveContractWorkRequest>
{
    public SaveContractWorkValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ContractorId).NotEmpty();
        RuleFor(x => x.WorkCategory).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ContractAmount).GreaterThan(0);
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0);
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
            Description = req.Description, EstimatedCost = req.EstimatedCost, ContractAmount = req.ContractAmount,
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

        work.WorkCategory = req.WorkCategory; work.Description = req.Description; work.EstimatedCost = req.EstimatedCost;
        work.ContractAmount = req.ContractAmount; work.StartDate = req.StartDate;
        work.ExpectedCompletion = req.ExpectedCompletion; work.PaymentTerms = req.PaymentTerms; work.WorkStatus = req.WorkStatus;
        work.Balance = work.ContractAmount - work.TotalPaid;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static readonly Expression<Func<ContractWork, ContractWorkDto>> Projection = c => new ContractWorkDto(
        c.Id, c.ProjectId, c.Project.Name, c.ContractorId, c.Contractor.Name, c.WorkCategory, c.Description,
        c.EstimatedCost, c.ContractAmount, c.StartDate, c.ExpectedCompletion, c.WorkStatus, c.TotalPaid, c.Balance);
}

// ---- Contractor payments -------------------------------------------
public record ContractorPaymentDto(Guid Id, string TxnNumber, Guid ContractorId, string ContractorName,
    Guid ProjectId, string ProjectName, Guid? ContractWorkId, DateOnly Date, decimal Amount,
    Guid PaymentMethodId, string PaymentMethodName, string? ReferenceNumber, string? Description,
    ContractorPaymentKind PaymentKind, TransactionStatus Status);

public record SaveContractorPaymentRequest(Guid ContractorId, Guid ProjectId, Guid? ContractWorkId, DateOnly Date,
    decimal Amount, Guid PaymentMethodId, string? ReferenceNumber, string? Description, ContractorPaymentKind PaymentKind);

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
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct)
                      ?? throw new NotFoundException("Project", req.ProjectId);
        if (!await db.Contractors.AnyAsync(c => c.Id == req.ContractorId && c.IsActive, ct))
            throw new AppException("Contractor not found or inactive.", 400);
        if (req.ContractWorkId is { } wid)
        {
            var work = await db.ContractWorks.AsNoTracking().FirstOrDefaultAsync(w => w.Id == wid, ct)
                       ?? throw new NotFoundException("ContractWork", wid);
            if (work.ProjectId != req.ProjectId || work.ContractorId != req.ContractorId)
                throw new AppException("The selected contract does not match this project/contractor.", 409);
        }

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
        if (payment.Status != TransactionStatus.Draft) throw new AppException($"Payment is already {payment.Status}.", 409);
        payment.Status = TransactionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);
        await approvals.SubmitAsync(ApprovalEntityTypes.ContractorPayment, payment.Id, payment.TxnNumber,
            null, payment.ProjectId, payment.Amount, ct);
        return await GetAsync(id, ct);
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

    private static readonly Expression<Func<ContractorPayment, ContractorPaymentDto>> Projection = p => new ContractorPaymentDto(
        p.Id, p.TxnNumber, p.ContractorId, p.Contractor.Name, p.ProjectId, p.Project.Name, p.ContractWorkId,
        p.Date, p.Amount, p.PaymentMethodId, p.PaymentMethod.Name, p.ReferenceNumber, p.Description, p.PaymentKind, p.Status);
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
