using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Customers;

public record CustomerPaymentDto(Guid Id, string TxnNumber, Guid ProjectId, string ProjectName, Guid CustomerId,
    string CustomerName, DateOnly Date, decimal Amount, Guid PaymentMethodId, string PaymentMethodName,
    string? Reference, string? Description, TransactionStatus Status);

public record SaveCustomerPaymentRequest(Guid ProjectId, DateOnly Date, decimal Amount, Guid PaymentMethodId,
    string? Reference, string? Description);

public class SaveCustomerPaymentValidator : AbstractValidator<SaveCustomerPaymentRequest>
{
    public SaveCustomerPaymentValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public record CustomerLedgerRow(string Kind, string Ref, DateOnly Date, decimal Charged, decimal Received);
public record CustomerLedger(Guid CustomerId, string CustomerName, decimal TotalSaleValue,
    decimal TotalReceived, decimal Outstanding, IReadOnlyList<CustomerLedgerRow> Rows);

public interface ICustomerPaymentService
{
    Task<PagedResult<CustomerPaymentDto>> ListAsync(PageQuery page, Guid? projectId, Guid? customerId, CancellationToken ct = default);
    Task<CustomerPaymentDto> CreateAsync(SaveCustomerPaymentRequest req, CancellationToken ct = default);
    Task<CustomerPaymentDto> CancelAsync(Guid id, string reason, CancellationToken ct = default);
    Task<CustomerLedger> LedgerAsync(Guid customerId, CancellationToken ct = default);
}

public class CustomerPaymentService(
    IAppDbContext db, ICurrentUser currentUser, ITransactionSequenceService sequences,
    IValidator<SaveCustomerPaymentRequest> validator) : ICustomerPaymentService
{
    public async Task<PagedResult<CustomerPaymentDto>> ListAsync(PageQuery page, Guid? projectId, Guid? customerId, CancellationToken ct = default)
    {
        var q = db.CustomerPayments.AsNoTracking();
        if (projectId is not null) q = q.Where(p => p.ProjectId == projectId);
        if (customerId is not null) q = q.Where(p => p.CustomerId == customerId);
        return await q.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt)
            .Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<CustomerPaymentDto> CreateAsync(SaveCustomerPaymentRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.ProjectId, ct)
                      ?? throw new NotFoundException("Project", req.ProjectId);
        if (project.CustomerId is null)
            throw new AppException("This project has no customer. Assign one before recording receipts.", 409);
        if (!await db.PaymentMethods.AnyAsync(m => m.Id == req.PaymentMethodId, ct))
            throw new NotFoundException("PaymentMethod", req.PaymentMethodId);

        var payment = new CustomerPayment
        {
            TxnNumber = await sequences.NextAsync("CUSTPAY", ct),
            ProjectId = project.Id, CustomerId = project.CustomerId.Value, Date = req.Date, Amount = req.Amount,
            PaymentMethodId = req.PaymentMethodId, Reference = req.Reference, Description = req.Description,
            Status = TransactionStatus.Posted, ApprovedBy = currentUser.UserId, ApprovedAt = DateTimeOffset.UtcNow
        };
        db.CustomerPayments.Add(payment);
        await db.SaveChangesAsync(ct);
        return await db.CustomerPayments.AsNoTracking().Where(p => p.Id == payment.Id).Select(Projection).FirstAsync(ct);
    }

    public async Task<CustomerPaymentDto> CancelAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var payment = await db.CustomerPayments.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("CustomerPayment", id);
        if (payment.Status == TransactionStatus.Cancelled) throw new AppException("Already cancelled.", 409);
        payment.Status = TransactionStatus.Cancelled;
        payment.Remarks = reason;
        payment.Amount = 0m;
        await db.SaveChangesAsync(ct);
        return await db.CustomerPayments.AsNoTracking().Where(p => p.Id == id).Select(Projection).FirstAsync(ct);
    }

    public async Task<CustomerLedger> LedgerAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct)
                       ?? throw new NotFoundException("Customer", customerId);

        var projects = await db.Projects.AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .Select(p => new { p.Name, p.StartDate, p.ContractSaleValue })
            .ToListAsync(ct);
        var payments = await db.CustomerPayments.AsNoTracking()
            .Where(p => p.CustomerId == customerId && p.Status == TransactionStatus.Posted)
            .Select(p => new { p.TxnNumber, p.Date, p.Amount })
            .ToListAsync(ct);

        var rows = projects
            .Where(p => p.ContractSaleValue.HasValue)
            .Select(p => new CustomerLedgerRow("Sale", p.Name, p.StartDate ?? default, p.ContractSaleValue!.Value, 0m))
            .Concat(payments.Select(p => new CustomerLedgerRow("Receipt", p.TxnNumber, p.Date, 0m, p.Amount)))
            .OrderBy(r => r.Date).ToList();

        var sale = projects.Sum(p => p.ContractSaleValue ?? 0m);
        var received = payments.Sum(p => p.Amount);
        return new CustomerLedger(customer.Id, customer.Name, sale, received, sale - received, rows);
    }

    private static readonly Expression<Func<CustomerPayment, CustomerPaymentDto>> Projection = p => new CustomerPaymentDto(
        p.Id, p.TxnNumber, p.ProjectId, p.Project.Name, p.CustomerId, p.Customer.Name, p.Date, p.Amount,
        p.PaymentMethodId, p.PaymentMethod.Name, p.Reference, p.Description, p.Status);
}
