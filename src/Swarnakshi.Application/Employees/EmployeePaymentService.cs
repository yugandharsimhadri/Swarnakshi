using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Employees;

public record EmployeePaymentDto(Guid Id, string TxnNumber, Guid EmployeeId, string EmployeeName,
    DateOnly Date, EmployeePaymentKind Kind, decimal Amount, decimal AdvanceRecovered, decimal NetPaid,
    DateOnly? PeriodStart, DateOnly? PeriodEnd, Guid? PaymentMethodId, string? PaymentMethodName,
    string? Reference, Guid? ProjectId, string? ProjectName, TransactionStatus Status, string? Remarks);

public record SaveEmployeePaymentRequest(Guid EmployeeId, DateOnly Date, EmployeePaymentKind Kind,
    decimal Amount, decimal AdvanceRecovered, DateOnly? PeriodStart, DateOnly? PeriodEnd,
    Guid? PaymentMethodId, string? Reference, Guid? ProjectId, string? Remarks);

public record EmployeeLedgerRow(string Kind, string Ref, DateOnly Date, decimal Amount,
    decimal AdvanceRecovered, decimal NetPaid, string Status);

public record EmployeeLedger(Guid EmployeeId, string EmployeeName, string Phone, decimal MonthlySalary,
    decimal TotalPaid, decimal AdvancesGiven, decimal AdvancesRecovered, decimal AdvanceOutstanding,
    IReadOnlyList<EmployeeLedgerRow> Rows);

public class SaveEmployeePaymentValidator : AbstractValidator<SaveEmployeePaymentRequest>
{
    public SaveEmployeePaymentValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.AdvanceRecovered).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AdvanceRecovered).LessThanOrEqualTo(x => x.Amount)
            .WithMessage("Advance recovered cannot be more than the payment itself.");
        RuleFor(x => x.AdvanceRecovered).Equal(0)
            .When(x => x.Kind == EmployeePaymentKind.Advance)
            .WithMessage("An advance cannot recover an advance.");
        RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart!.Value)
            .When(x => x.PeriodStart.HasValue && x.PeriodEnd.HasValue);
    }
}

public interface IEmployeePaymentService
{
    Task<PagedResult<EmployeePaymentDto>> ListAsync(PageQuery page, Guid? employeeId, Guid? projectId,
        EmployeePaymentKind? kind, TransactionStatus? status, CancellationToken ct = default);
    Task<EmployeePaymentDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<EmployeePaymentDto> CreateAsync(SaveEmployeePaymentRequest req, CancellationToken ct = default);
    Task<EmployeePaymentDto> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<EmployeePaymentDto> CancelAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeLedger> LedgerAsync(Guid employeeId, CancellationToken ct = default);
}

public class EmployeePaymentService(
    IAppDbContext db,
    IApprovalService approvals,
    ITransactionSequenceService sequences,
    IValidator<SaveEmployeePaymentRequest> validator) : IEmployeePaymentService
{
    public async Task<PagedResult<EmployeePaymentDto>> ListAsync(PageQuery page, Guid? employeeId, Guid? projectId,
        EmployeePaymentKind? kind, TransactionStatus? status, CancellationToken ct = default)
    {
        var q = db.EmployeePayments.AsNoTracking();
        if (employeeId is not null) q = q.Where(p => p.EmployeeId == employeeId);
        if (projectId is not null) q = q.Where(p => p.ProjectId == projectId);
        if (kind is not null) q = q.Where(p => p.Kind == kind);
        if (status is not null) q = q.Where(p => p.Status == status);
        var term = page.Q?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(p => p.TxnNumber.Contains(term) || p.Employee.Name.Contains(term));

        return await q.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt)
            .Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<EmployeePaymentDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.EmployeePayments.AsNoTracking().Where(p => p.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("EmployeePayment", id);

    public async Task<EmployeePaymentDto> CreateAsync(SaveEmployeePaymentRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == req.EmployeeId, ct)
                       ?? throw new NotFoundException("Employee", req.EmployeeId);
        if (!employee.IsActive)
            throw new AppException($"{employee.Name} is not an active employee.", 409);
        if (req.ProjectId is { } pid && !await db.Projects.AnyAsync(p => p.Id == pid, ct))
            throw new NotFoundException("Project", pid);

        if (req.AdvanceRecovered > 0)
        {
            var outstanding = await AdvanceOutstandingAsync(req.EmployeeId, ct);
            if (req.AdvanceRecovered > outstanding)
                throw new AppException(
                    $"{employee.Name} has only {outstanding:0.00} of advance outstanding; cannot recover {req.AdvanceRecovered:0.00}.", 409);
        }

        var payment = new EmployeePayment
        {
            TxnNumber = await sequences.NextAsync("EMPPAY", ct),
            EmployeeId = req.EmployeeId, Date = req.Date, Kind = req.Kind, Amount = req.Amount,
            AdvanceRecovered = req.Kind == EmployeePaymentKind.Advance ? 0m : req.AdvanceRecovered,
            PeriodStart = req.PeriodStart, PeriodEnd = req.PeriodEnd,
            PaymentMethodId = req.PaymentMethodId, Reference = req.Reference,
            ProjectId = req.ProjectId, Remarks = req.Remarks,
            Status = TransactionStatus.Draft
        };
        db.EmployeePayments.Add(payment);
        await db.SaveChangesAsync(ct);
        return await GetAsync(payment.Id, ct);
    }

    public async Task<EmployeePaymentDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var payment = await db.EmployeePayments.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("EmployeePayment", id);
        if (payment.Status != TransactionStatus.Draft)
            throw new AppException($"This payment is already {payment.Status}.", 409);

        payment.Status = TransactionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);

        // Money leaving the company goes through the same Owner approval as contractor and labour
        // payments — a salary run should not be the one way to move cash unreviewed.
        await approvals.SubmitAsync(ApprovalEntityTypes.EmployeePayment, payment.Id, payment.TxnNumber,
            null, payment.ProjectId, payment.Amount, ct);
        return await GetAsync(id, ct);
    }

    public async Task<EmployeePaymentDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var payment = await db.EmployeePayments.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("EmployeePayment", id);
        if (payment.Status is TransactionStatus.Posted or TransactionStatus.Cancelled)
            throw new AppException($"Cannot cancel a {payment.Status} payment.", 409);
        payment.Status = TransactionStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<EmployeeLedger> LedgerAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
                       ?? throw new NotFoundException("Employee", employeeId);

        var payments = await db.EmployeePayments.AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.Status != TransactionStatus.Cancelled)
            .OrderByDescending(p => p.Date)
            .Select(p => new { p.TxnNumber, p.Date, p.Kind, p.Amount, p.AdvanceRecovered, p.Status })
            .ToListAsync(ct);

        var posted = payments.Where(p => p.Status == TransactionStatus.Posted).ToList();
        var given = posted.Where(p => p.Kind == EmployeePaymentKind.Advance).Sum(p => p.Amount);
        var recovered = posted.Sum(p => p.AdvanceRecovered);

        var rows = payments
            .Select(p => new EmployeeLedgerRow(p.Kind.ToString(), p.TxnNumber, p.Date, p.Amount,
                p.AdvanceRecovered, p.Amount - p.AdvanceRecovered, p.Status.ToString()))
            .ToList();

        return new EmployeeLedger(employee.Id, employee.Name, employee.Phone, employee.MonthlySalary,
            posted.Sum(p => p.Amount - p.AdvanceRecovered), given, recovered, given - recovered, rows);
    }

    private async Task<decimal> AdvanceOutstandingAsync(Guid employeeId, CancellationToken ct)
    {
        var posted = db.EmployeePayments.AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.Status == TransactionStatus.Posted);
        var given = await posted.Where(p => p.Kind == EmployeePaymentKind.Advance).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var recovered = await posted.SumAsync(p => (decimal?)p.AdvanceRecovered, ct) ?? 0m;
        return given - recovered;
    }

    private static readonly Expression<Func<EmployeePayment, EmployeePaymentDto>> Projection = p => new EmployeePaymentDto(
        p.Id, p.TxnNumber, p.EmployeeId, p.Employee.Name, p.Date, p.Kind, p.Amount, p.AdvanceRecovered,
        p.Amount - p.AdvanceRecovered, p.PeriodStart, p.PeriodEnd,
        p.PaymentMethodId, p.PaymentMethod != null ? p.PaymentMethod.Name : null,
        p.Reference, p.ProjectId, p.Project != null ? p.Project.Name : null, p.Status, p.Remarks);
}

/// <summary>
/// Posts an approved employee payment. Only a payment charged to a project reaches project cost —
/// office salary is a company overhead, and quietly loading it onto whichever project was open
/// would make that project look more expensive than it was.
/// </summary>
public class EmployeePaymentApprovalHandler(IAppDbContext db, IProjectCostWriter costWriter) : IApprovalHandler
{
    public string EntityType => ApprovalEntityTypes.EmployeePayment;

    public async Task OnApprovedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var payment = await db.EmployeePayments.Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == entityId, ct)
            ?? throw new NotFoundException("EmployeePayment", entityId);
        if (payment.Status == TransactionStatus.Posted) return;

        payment.Status = TransactionStatus.Posted;
        payment.ApprovedBy = decidedBy;
        payment.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (payment.ProjectId is not { } projectId) return;

        var labourHead = await db.ExpenseHeads.Where(h => h.Name == "Labour")
            .Select(h => (Guid?)h.Id).FirstOrDefaultAsync(ct);

        // The gross amount is the cost to the project. An advance recovery is the employee repaying
        // the company, not a reduction in what this month's work cost.
        await costWriter.WriteAsync(projectId, ProjectExpenseType.Labour, payment.Amount,
            payment.Date, labourHead, null,
            $"{payment.Kind}: {payment.Employee.Name} ({payment.TxnNumber})",
            nameof(EmployeePayment), payment.Id, ct);
    }

    public async Task OnRejectedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        var payment = await db.EmployeePayments.FirstOrDefaultAsync(p => p.Id == entityId, ct);
        if (payment is null) return;
        payment.Status = TransactionStatus.Rejected;
        await db.SaveChangesAsync(ct);
    }
}
