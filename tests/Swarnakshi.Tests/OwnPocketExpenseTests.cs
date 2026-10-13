using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Abstractions;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Attachments;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Expenses;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// The engineer pays for something out of his own pocket and the company pays him back.
///
/// <para>Two sums have to stay right through all of it. The villa is charged once — when the Owner
/// approves the claim — and paying the man back must not charge it again; and what the company owes
/// its people has to be the claims it has approved and not yet settled, no more and no less. Every
/// test here is about one of those two.</para>
/// </summary>
public class OwnPocketExpenseTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Yard(Guid SiteId, Guid ProjectId, Guid HeadId, Guid EngineerId, Guid OwnerId, Guid MethodId);

    private static async Task<Yard> ArrangeAsync(AppDbContext db, IPasswordHasher hasher)
    {
        var site = new Site { Code = "S1", Name = "Green Meadows", Status = SiteStatus.Active };
        var project = new Project
        {
            Code = "P1", Name = "Villa 12", Site = site,
            EstimatedCost = 2_000_000, Status = ProjectStatus.Active
        };
        var engineer = new User
        {
            Name = "Ramesh", Username = "ramesh", Role = UserRole.Engineer,
            PasswordHash = hasher.Hash("Secure@123")
        };
        db.AddRange(site, project, engineer);
        await db.SaveChangesAsync();

        var head = await db.ExpenseHeads.AsNoTracking().FirstAsync();
        var method = await db.PaymentMethods.AsNoTracking().FirstAsync();
        var owner = await db.Users.AsNoTracking().FirstAsync(u => u.Role == UserRole.Owner);
        return new Yard(site.Id, project.Id, head.Id, engineer.Id, owner.Id, method.Id);
    }

    /// <summary>
    /// The whole claim, the way it really happens: the engineer records it and attaches the bill,
    /// the Owner approves it. Who is signed in matters — the claim is made by whoever is acting, and
    /// a test that forgot to switch would quietly file the Owner's own claim instead.
    /// </summary>
    private static async Task<Guid> ApprovedClaimAsync(TestHost host, IServiceProvider sp, Yard y, decimal amount)
    {
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        host.ActAs(y.EngineerId, UserRole.Engineer, Permissions.ForRole(UserRole.Engineer));
        var claim = await expenses.CreateAsync(Claim(y, amount));
        await AttachBillAsync(sp, claim.Id);
        await expenses.SubmitAsync(claim.Id);
        host.ActAs(y.OwnerId);
        await sp.ApproveAsync(ApprovalEntityTypes.ProjectExpense, claim.Id);
        return claim.Id;
    }

    private static SaveProjectExpenseRequest Claim(Yard y, decimal amount) =>
        new(y.ProjectId, Today, y.HeadId, null, "Cement, two bags", amount,
            ProjectExpenseType.Direct, PaymentStatus.Paid, null, PaidByMe: true);

    /// <summary>A 1x1 JPEG — a real one, because the service checks the first bytes.</summary>
    private static async Task AttachBillAsync(IServiceProvider sp, Guid expenseId, string name = "bill.jpg")
    {
        var attachments = sp.GetRequiredService<IAttachmentService>();
        using var jpeg = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9]);
        await attachments.UploadAsync(ApprovalEntityTypes.ProjectExpense, expenseId, name, "image/jpeg", jpeg);
    }

    private static Task<decimal> VillaCostAsync(AppDbContext db, Guid projectId) =>
        db.ProjectExpenses.AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.Status == TransactionStatus.Posted)
            .SumAsync(e => e.Amount);

    [Fact]
    public async Task A_claim_is_a_draft_until_its_bill_is_attached_and_it_is_sent_up()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var y = await ArrangeAsync(db, sp.GetRequiredService<IPasswordHasher>());
        host.ActAs(y.EngineerId, UserRole.Engineer, Permissions.ForRole(UserRole.Engineer));

        var claim = await expenses.CreateAsync(Claim(y, 4_250m));

        claim.Status.Should().Be(TransactionStatus.Draft);
        claim.PaidByUserId.Should().Be(y.EngineerId);
        claim.ReimbursementStatus.Should().Be(ReimbursementStatus.None);
        // Nothing is owed and nothing is charged while it is still a draft.
        (await VillaCostAsync(db, y.ProjectId)).Should().Be(0m);

        var sendWithNoBill = () => expenses.SubmitAsync(claim.Id);
        (await sendWithNoBill.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("photo of the bill");

        await AttachBillAsync(sp, claim.Id);
        var sent = await expenses.SubmitAsync(claim.Id);
        sent.Status.Should().Be(TransactionStatus.PendingApproval);
    }

    [Fact]
    public async Task An_own_pocket_claim_goes_to_the_owner_even_when_it_is_under_the_auto_approve_limit()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var y = await ArrangeAsync(db, sp.GetRequiredService<IPasswordHasher>());
        await sp.SetAutoApproveLimitAsync(5_000m);
        host.ActAs(y.EngineerId, UserRole.Engineer, Permissions.ForRole(UserRole.Engineer));

        // A company-paid expense of the same amount posts itself: that is the limit working.
        var company = await expenses.CreateAsync(new SaveProjectExpenseRequest(
            y.ProjectId, Today, y.HeadId, null, "Company card", 4_999m,
            ProjectExpenseType.Direct, PaymentStatus.Paid, y.MethodId));
        company.Status.Should().Be(TransactionStatus.Posted);

        // The same money out of his own pocket does not. Paying a person back is never automatic.
        var claim = await expenses.CreateAsync(Claim(y, 4_999m));
        await AttachBillAsync(sp, claim.Id);
        var sent = await expenses.SubmitAsync(claim.Id);

        sent.Status.Should().Be(TransactionStatus.PendingApproval);
        var request = await db.ApprovalRequests.AsNoTracking()
            .FirstAsync(a => a.EntityType == ApprovalEntityTypes.ProjectExpense && a.EntityId == claim.Id);
        request.CurrentStatus.Should().Be(TransactionStatus.PendingApproval);
        request.DecidedAt.Should().BeNull();
    }

    [Fact]
    public async Task Approving_a_claim_charges_the_villa_once_and_makes_the_money_owed()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var settlements = sp.GetRequiredService<IExpenseSettlementService>();
        var y = await ArrangeAsync(db, sp.GetRequiredService<IPasswordHasher>());

        await ApprovedClaimAsync(host, sp, y, 4_250m);

        (await VillaCostAsync(db, y.ProjectId)).Should().Be(4_250m);
        var owed = await settlements.OwedAsync();
        owed.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { UserId = y.EngineerId, Due = 4_250m, Claims = 1 },
                o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Settling_pays_several_claims_at_once_and_does_not_touch_the_villa_cost()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var settlements = sp.GetRequiredService<IExpenseSettlementService>();
        var y = await ArrangeAsync(db, sp.GetRequiredService<IPasswordHasher>());

        var ids = new List<Guid>();
        foreach (var amount in new[] { 4_250m, 4_930m, 1_860m })
            ids.Add(await ApprovedClaimAsync(host, sp, y, amount));

        var costBefore = await VillaCostAsync(db, y.ProjectId);
        costBefore.Should().Be(11_040m);

        var settlement = await settlements.CreateAsync(new SaveSettlementRequest(
            y.EngineerId, Today, y.MethodId, "UPI 4471", ids));

        settlement.Amount.Should().Be(11_040m);
        settlement.Lines.Should().HaveCount(3);

        // The whole point. The villa was charged when each claim was approved; handing the money
        // over is not a second cost, and this figure moving would mean every villa an engineer ever
        // bought anything for is overstated.
        (await VillaCostAsync(db, y.ProjectId)).Should().Be(costBefore);
        (await settlements.OwedAsync()).Should().BeEmpty();
        db.ChangeTracker.Clear();
        var claims = await settlements.ClaimsAsync(ReimbursementStatus.Settled, y.EngineerId);
        claims.Should().HaveCount(3);
    }

    [Fact]
    public async Task A_claim_cannot_be_settled_twice_nor_before_it_is_approved()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var settlements = sp.GetRequiredService<IExpenseSettlementService>();
        var y = await ArrangeAsync(db, sp.GetRequiredService<IPasswordHasher>());

        host.ActAs(y.EngineerId, UserRole.Engineer, Permissions.ForRole(UserRole.Engineer));
        var waiting = await expenses.CreateAsync(Claim(y, 500m));
        await AttachBillAsync(sp, waiting.Id);
        await expenses.SubmitAsync(waiting.Id);
        host.ActAs(y.OwnerId);

        var tooEarly = () => settlements.CreateAsync(new SaveSettlementRequest(
            y.EngineerId, Today, null, null, [waiting.Id]));
        (await tooEarly.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("not approved yet");

        await sp.ApproveAsync(ApprovalEntityTypes.ProjectExpense, waiting.Id);
        await settlements.CreateAsync(new SaveSettlementRequest(y.EngineerId, Today, null, null, [waiting.Id]));

        var again = () => settlements.CreateAsync(new SaveSettlementRequest(
            y.EngineerId, Today, null, null, [waiting.Id]));
        (await again.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("already been settled");
    }

    [Fact]
    public async Task Voiding_a_settlement_puts_the_money_back_on_the_books_and_keeps_the_record()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var settlements = sp.GetRequiredService<IExpenseSettlementService>();
        var y = await ArrangeAsync(db, sp.GetRequiredService<IPasswordHasher>());

        var claimId = await ApprovedClaimAsync(host, sp, y, 4_250m);
        var settlement = await settlements.CreateAsync(new SaveSettlementRequest(
            y.EngineerId, Today, null, "wrong account", [claimId]));

        var voided = await settlements.VoidAsync(settlement.Id, "Paid to the wrong account.");

        voided.Status.Should().Be(TransactionStatus.Cancelled);
        // The lines stay: this is the record that money went out and came back.
        voided.Lines.Should().ContainSingle();
        (await settlements.OwedAsync()).Should().ContainSingle()
            .Which.Due.Should().Be(4_250m);

        // And it can be paid again, properly, which a unique index on the claim would have blocked.
        var second = await settlements.CreateAsync(new SaveSettlementRequest(
            y.EngineerId, Today, y.MethodId, "UPI 9920", [claimId]));
        second.Amount.Should().Be(4_250m);
        (await settlements.OwedAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task A_settled_claim_cannot_be_cancelled_out_from_under_the_payment()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var settlements = sp.GetRequiredService<IExpenseSettlementService>();
        var y = await ArrangeAsync(db, sp.GetRequiredService<IPasswordHasher>());

        var claimId = await ApprovedClaimAsync(host, sp, y, 4_250m);
        await settlements.CreateAsync(new SaveSettlementRequest(y.EngineerId, Today, null, null, [claimId]));

        var act = () => expenses.CancelAsync(claimId, "Changed my mind.");

        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("Void the settlement first");
        (await VillaCostAsync(db, y.ProjectId)).Should().Be(4_250m);
    }

    [Fact]
    public async Task One_persons_claims_cannot_be_paid_to_another_person()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var settlements = sp.GetRequiredService<IExpenseSettlementService>();
        var y = await ArrangeAsync(db, sp.GetRequiredService<IPasswordHasher>());

        var claimId = await ApprovedClaimAsync(host, sp, y, 4_250m);

        // Ramesh's claim, paid to the Owner. An honest mistake on a busy screen, and the kind that
        // leaves one man unpaid and another's name against money he never spent.
        var act = () => settlements.CreateAsync(new SaveSettlementRequest(
            y.OwnerId, Today, null, null, [claimId]));

        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("was not paid by this person");
    }

    [Fact]
    public async Task An_engineer_can_record_an_expense_at_all_but_cannot_settle_one()
    {
        Permissions.ForRole(UserRole.Engineer).Should().Contain(Permissions.ExpenseCreate);
        Permissions.ForRole(UserRole.Engineer).Should().NotContain(Permissions.ExpenseSettle);
        Permissions.ForRole(UserRole.Supervisor).Should().Contain(Permissions.ExpenseCreate);
        Permissions.ForRole(UserRole.Owner).Should().Contain(Permissions.ExpenseSettle);
        Permissions.ForRole(UserRole.SubOwner).Should().Contain(Permissions.ExpenseSettle);
        Permissions.ForRole(UserRole.Accountant).Should().Contain(Permissions.ExpenseSettle);
        await Task.CompletedTask;
    }
}

/// <summary>
/// What may be attached where. The bill photo rule exists because the Owner approves a claim by
/// looking at it on a phone, and because a site's uplink is one bar of 4G.
/// </summary>
public class BillAttachmentTests
{
    private static MemoryStream Jpeg(int padBytes = 0) =>
        new([.. new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 },
             .. new byte[padBytes]]);

    [Fact]
    public async Task A_bill_photo_must_be_an_image_whatever_the_file_is_called()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var id = Guid.NewGuid();

        var pdf = () => attachments.UploadAsync(ApprovalEntityTypes.ProjectExpense, id, "bill.pdf",
            "application/pdf", new MemoryStream([0x25, 0x50, 0x44, 0x46]));
        (await pdf.Should().ThrowAsync<AppException>()).Which.Message.Should().Contain("not a photo");

        // Named .jpg, but it is a PDF. The extension is what the uploader claims; the bytes are
        // what it is.
        var renamed = () => attachments.UploadAsync(ApprovalEntityTypes.ProjectExpense, id, "bill.jpg",
            "image/jpeg", new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A, 0x25, 0x00, 0x00]));
        (await renamed.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("not a JPG, PNG or WebP image");

        var ok = await attachments.UploadAsync(ApprovalEntityTypes.ProjectExpense, id, "bill.jpg",
            "image/jpeg", Jpeg());
        ok.FileName.Should().Be("bill.jpg");
    }

    [Fact]
    public async Task A_bill_photo_over_two_and_a_half_megabytes_is_refused_and_says_its_size()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var id = Guid.NewGuid();

        var tooBig = () => attachments.UploadAsync(ApprovalEntityTypes.ProjectExpense, id, "bill.jpg",
            "image/jpeg", Jpeg(2_700_000));

        (await tooBig.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("2.6 MB").And.Contain("limit is 2.5 MB");
    }

    [Fact]
    public async Task A_purchase_still_takes_the_suppliers_invoice_as_a_pdf()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentService>();

        var saved = await attachments.UploadAsync(ApprovalEntityTypes.Purchase, Guid.NewGuid(),
            "invoice.pdf", "application/pdf", new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31]));

        saved.FileName.Should().Be("invoice.pdf");
    }
}
