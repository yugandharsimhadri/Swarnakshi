using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Inventory;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Application.Procurement;

// ---- DTOs ----------------------------------------------------------------
public record PurchaseItemInput(Guid MaterialId, Guid UnitId, decimal Quantity, decimal Rate,
    decimal Discount, decimal TaxAmount, Guid? DeliverToProjectId = null, Guid? ExpenseHeadId = null);

public record SavePurchaseRequest(Guid SupplierId, Guid SiteId, Guid? ProjectId, string? InvoiceNumber,
    DateOnly? InvoiceDate, DateOnly Date, decimal OtherCharges, string? Remarks, List<PurchaseItemInput> Items);

public record SupplierPaymentInput(decimal Amount, DateOnly Date, Guid? PaymentMethodId, string? Reference);

public record PurchaseItemDto(Guid Id, Guid MaterialId, string MaterialName, string UnitCode, decimal Quantity,
    decimal Rate, decimal Discount, decimal TaxAmount, decimal LineTotal,
    Guid? DeliverToProjectId, string? DeliverToProjectName, Guid? ExpenseHeadId);

public record PurchaseDto(Guid Id, string TxnNumber, Guid SupplierId, string SupplierName, Guid SiteId, string SiteName,
    Guid? ProjectId, string? InvoiceNumber, DateOnly? InvoiceDate, DateOnly Date, decimal SubTotal, decimal Discount,
    decimal TaxAmount, decimal OtherCharges, decimal TotalAmount, decimal PaidAmount, decimal BalanceAmount,
    PaymentStatus PaymentStatus, TransactionStatus Status, IReadOnlyList<PurchaseItemDto> Items);

public class SavePurchaseValidator : AbstractValidator<SavePurchaseRequest>
{
    public SavePurchaseValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Quantity).GreaterThan(0);
            i.RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
        });
    }
}

// ---- Poster (shared by service + approval handler) ---------------------
public class PurchasePoster(
    IAppDbContext db, IInventoryLedger ledger, IProjectCostWriter costWriter, IDateTimeProvider clock)
{
    public async Task PostAsync(Guid purchaseId, Guid actorId, CancellationToken ct)
    {
        var purchase = await db.PurchaseHeaders.Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == purchaseId, ct)
            ?? throw new NotFoundException("Purchase", purchaseId);

        if (purchase.Status == TransactionStatus.Posted) return;
        if (purchase.Status is TransactionStatus.Cancelled or TransactionStatus.Rejected)
            throw new AppException($"Purchase {purchase.TxnNumber} is {purchase.Status}.", 409);

        foreach (var item in purchase.Items)
        {
            var unitRate = item.Quantity == 0 ? 0 : Math.Round(item.LineTotal / item.Quantity, 4); // landed rate incl. tax/discount

            await ledger.ReceiveAsync(purchase.SiteId, item.MaterialId, item.UnitId, item.Quantity, unitRate,
                InventoryTransactionType.PurchaseReceipt, purchase.Date, ApprovalEntityTypes.Purchase, purchase.Id,
                purchase.TxnNumber, null, actorId, ct);

            if (item.DeliverToProjectId is { } projectId)
                await DeliverToProjectAsync(purchase, item, projectId, unitRate, actorId, ct);
        }

        purchase.Status = TransactionStatus.Posted;
        purchase.ApprovedBy = actorId;
        purchase.ApprovedAt = clock.Now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Material bought for one villa: received above, issued to that villa here, in the same post.
    ///
    /// It goes through inventory rather than around it so the stock ledger tells the whole story and
    /// purchases still reconcile against consumption. The issue uses THIS purchase's landed rate
    /// rather than the site's weighted average, which is both what the buyer expects — the villa is
    /// charged what was actually paid for its material — and the only rate that leaves the store
    /// untouched: receiving q at r and issuing q at r restores the quantity, the value and the
    /// average exactly, so material earmarked for one project cannot distort the pool's valuation.
    /// </summary>
    private async Task DeliverToProjectAsync(
        PurchaseHeader purchase, PurchaseItem item, Guid projectId, decimal unitRate, Guid actorId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Name, p.SiteId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Project", projectId);

        // Inventory is site-level, so a project on another site cannot consume from this one.
        if (project.SiteId != purchase.SiteId)
            throw new AppException(
                $"{project.Name} is not on the site this purchase was delivered to, so it cannot be issued from that store.", 409);

        var (txn, rate) = await ledger.IssueAsync(purchase.SiteId, item.MaterialId, item.UnitId, item.Quantity,
            InventoryTransactionType.ProjectConsumption, purchase.Date, ApprovalEntityTypes.Purchase, purchase.Id,
            purchase.TxnNumber, projectId, unitRate, actorId, ct);

        await costWriter.WriteMaterialCostAsync(projectId, Math.Round(item.Quantity * rate, 2), purchase.Date,
            item.ExpenseHeadId, null, "InventoryTransaction", txn.Id,
            $"Direct delivery: {purchase.TxnNumber}", ct);
    }
}

public interface IPurchaseService
{
    Task<PagedResult<PurchaseDto>> ListAsync(PageQuery page, Guid? siteId, TransactionStatus? status, CancellationToken ct = default);
    Task<PurchaseDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseDto> CreateAsync(SavePurchaseRequest req, CancellationToken ct = default);
    Task<PurchaseDto> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseDto> AddPaymentAsync(Guid id, SupplierPaymentInput input, CancellationToken ct = default);
}

public class PurchaseService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ISettingsService settings,
    IApprovalService approvals,
    PurchasePoster poster,
    ITransactionSequenceService sequences,
    IValidator<SavePurchaseRequest> validator) : IPurchaseService
{
    public async Task<PagedResult<PurchaseDto>> ListAsync(PageQuery page, Guid? siteId, TransactionStatus? status, CancellationToken ct = default)
    {
        var q = db.PurchaseHeaders.AsNoTracking();
        if (siteId is not null) q = q.Where(p => p.SiteId == siteId);
        if (status is not null) q = q.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(page.Q))
            q = q.Where(p => p.TxnNumber.Contains(page.Q) || p.Supplier.Name.Contains(page.Q) || (p.InvoiceNumber != null && p.InvoiceNumber.Contains(page.Q)));
        return await q.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt)
            .Select(Projection).ToPagedAsync(page, ct);
    }

    public async Task<PurchaseDto> GetAsync(Guid id, CancellationToken ct = default)
        => await db.PurchaseHeaders.AsNoTracking().Where(p => p.Id == id).Select(Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Purchase", id);

    public async Task<PurchaseDto> CreateAsync(SavePurchaseRequest req, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(req, ct);
        if (!await db.Suppliers.AnyAsync(s => s.Id == req.SupplierId && s.IsActive, ct))
            throw new AppException("Supplier not found or inactive.", 400);
        if (!await db.Sites.AnyAsync(s => s.Id == req.SiteId, ct))
            throw new NotFoundException("Site", req.SiteId);

        // Caught here as well as at post: telling someone their line is wrong while they are still
        // entering it beats failing days later when an approver presses the button.
        var directProjectIds = req.Items.Where(i => i.DeliverToProjectId.HasValue)
            .Select(i => i.DeliverToProjectId!.Value).Distinct().ToList();
        if (directProjectIds.Count > 0)
        {
            var onThisSite = await db.Projects.AsNoTracking()
                .Where(p => directProjectIds.Contains(p.Id) && p.SiteId == req.SiteId)
                .Select(p => p.Id).ToListAsync(ct);
            var stray = directProjectIds.Except(onThisSite).ToList();
            if (stray.Count > 0)
                throw new AppException(
                    "A line is set to deliver to a project that is not on this site. Inventory is site-level, "
                    + "so material can only be issued to a project of the same site.", 409);
        }

        var header = new PurchaseHeader
        {
            TxnNumber = await sequences.NextAsync("PUR", ct),
            SupplierId = req.SupplierId, SiteId = req.SiteId, ProjectId = req.ProjectId,
            InvoiceNumber = req.InvoiceNumber, InvoiceDate = req.InvoiceDate, Date = req.Date,
            OtherCharges = req.OtherCharges, Remarks = req.Remarks, Status = TransactionStatus.Draft
        };

        foreach (var i in req.Items)
        {
            var lineGross = i.Quantity * i.Rate;
            var lineTotal = lineGross - i.Discount + i.TaxAmount;
            header.Items.Add(new PurchaseItem
            {
                MaterialId = i.MaterialId, UnitId = i.UnitId, Quantity = i.Quantity, Rate = i.Rate,
                Discount = i.Discount, TaxAmount = i.TaxAmount, LineTotal = Math.Round(lineTotal, 2),
                DeliverToProjectId = i.DeliverToProjectId, ExpenseHeadId = i.ExpenseHeadId
            });
        }

        header.SubTotal = Math.Round(req.Items.Sum(i => i.Quantity * i.Rate), 2);
        header.Discount = Math.Round(req.Items.Sum(i => i.Discount), 2);
        header.TaxAmount = Math.Round(req.Items.Sum(i => i.TaxAmount), 2);
        header.TotalAmount = Math.Round(header.SubTotal - header.Discount + header.TaxAmount + header.OtherCharges, 2);
        header.BalanceAmount = header.TotalAmount;
        header.PaymentStatus = PaymentStatus.Unpaid;

        db.PurchaseHeaders.Add(header);
        await db.SaveChangesAsync(ct);
        return await GetAsync(header.Id, ct);
    }

    public async Task<PurchaseDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var header = await db.PurchaseHeaders.FirstOrDefaultAsync(p => p.Id == id, ct)
                     ?? throw new NotFoundException("Purchase", id);
        if (header.Status != TransactionStatus.Draft)
            throw new AppException($"Purchase is already {header.Status}.", 409);

        // Fallback is true: if the setting row is missing entirely, hold the purchase rather than
        // post it. An unapproved purchase that reached stock is far worse than one that waited.
        var needsApproval = await settings.GetBoolAsync(SettingKeys.PurchaseNeedsApproval, header.SiteId, true, ct);
        if (needsApproval)
        {
            header.Status = TransactionStatus.PendingApproval;
            await db.SaveChangesAsync(ct);
            await approvals.SubmitAsync(ApprovalEntityTypes.Purchase, header.Id, header.TxnNumber,
                header.SiteId, header.ProjectId, header.TotalAmount, ct);
        }
        else
        {
            await using var txn = await db.Database.BeginTransactionAsync(ct);
            await poster.PostAsync(header.Id, currentUser.UserId!.Value, ct);
            await txn.CommitAsync(ct);
        }
        return await GetAsync(id, ct);
    }

    public async Task<PurchaseDto> AddPaymentAsync(Guid id, SupplierPaymentInput input, CancellationToken ct = default)
    {
        if (input.Amount <= 0) throw new AppException("Payment amount must be positive.", 400);
        var header = await db.PurchaseHeaders.FirstOrDefaultAsync(p => p.Id == id, ct)
                     ?? throw new NotFoundException("Purchase", id);
        if (input.Amount > header.BalanceAmount + 0.01m)
            throw new AppException($"Payment exceeds outstanding balance ({header.BalanceAmount:0.00}).", 409);

        db.SupplierPayments.Add(new SupplierPayment
        {
            PurchaseHeaderId = header.Id, Amount = input.Amount, Date = input.Date,
            PaymentMethodId = input.PaymentMethodId, Reference = input.Reference
        });
        header.PaidAmount = Math.Round(header.PaidAmount + input.Amount, 2);
        header.BalanceAmount = Math.Round(header.TotalAmount - header.PaidAmount, 2);
        header.PaymentStatus = header.BalanceAmount <= 0.01m ? PaymentStatus.Paid
            : header.PaidAmount > 0 ? PaymentStatus.PartiallyPaid : PaymentStatus.Unpaid;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private static readonly Expression<Func<PurchaseHeader, PurchaseDto>> Projection = p => new PurchaseDto(
        p.Id, p.TxnNumber, p.SupplierId, p.Supplier.Name, p.SiteId, p.Site.Name, p.ProjectId,
        p.InvoiceNumber, p.InvoiceDate, p.Date, p.SubTotal, p.Discount, p.TaxAmount, p.OtherCharges,
        p.TotalAmount, p.PaidAmount, p.BalanceAmount, p.PaymentStatus, p.Status,
        p.Items.Select(i => new PurchaseItemDto(i.Id, i.MaterialId, i.Material.Name, i.Unit.Code,
            i.Quantity, i.Rate, i.Discount, i.TaxAmount, i.LineTotal,
            i.DeliverToProjectId, i.DeliverToProject != null ? i.DeliverToProject.Name : null,
            i.ExpenseHeadId)).ToList());
}

public class PurchaseApprovalHandler(PurchasePoster poster) : IApprovalHandler
{
    public string EntityType => ApprovalEntityTypes.Purchase;

    public Task OnApprovedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
        => poster.PostAsync(entityId, decidedBy, ct);

    public async Task OnRejectedAsync(Guid entityId, ApprovalDecision decision, Guid decidedBy, CancellationToken ct)
    {
        // handled by ApprovalService status; nothing to undo since nothing was posted.
        await Task.CompletedTask;
    }
}
