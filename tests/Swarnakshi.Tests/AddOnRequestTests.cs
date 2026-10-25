using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Expenses;
using Swarnakshi.Application.Security;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// The register of changes a customer asks for on their villa.
///
/// <para>Its whole reason for existing separately is that the expected amount must never reach the
/// villa's cost. What a change was thought to come to and what it actually cost are different
/// numbers arrived at by different people, and the second one turns up as ordinary expenses. The
/// first test here is the one that matters; the rest describe the register.</para>
/// </summary>
public class AddOnRequestTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Yard(Guid ProjectId, Guid HeadId, Guid OtherHeadId);

    private static async Task<Yard> ArrangeAsync(AppDbContext db)
    {
        var site = new Site { Code = "S1", Name = "Green Meadows", Status = SiteStatus.Active };
        var project = new Project
        {
            Code = "P1", Name = "Villa 12", Site = site,
            EstimatedCost = 2_000_000, Status = ProjectStatus.Active
        };
        db.AddRange(site, project);
        await db.SaveChangesAsync();

        var heads = await db.ExpenseHeads.AsNoTracking().OrderBy(h => h.SortOrder).Take(2).ToListAsync();
        return new Yard(project.Id, heads[0].Id, heads[1].Id);
    }

    private static SaveAddOnRequest Request(Yard y, decimal amount, string what = "Two extra points in the kitchen")
        => new(y.ProjectId, Today, y.HeadId, what, amount, null);

    private static Task<decimal> VillaCostAsync(AppDbContext db, Guid projectId) =>
        db.ProjectExpenses.AsNoTracking()
            .Where(e => e.ProjectId == projectId && e.Status == TransactionStatus.Posted)
            .SumAsync(e => e.Amount);

    [Fact]
    public async Task An_add_on_is_never_counted_as_a_cost_however_it_is_recorded()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var addOns = sp.GetRequiredService<IAddOnRequestService>();
        var expenses = sp.GetRequiredService<IProjectExpenseService>();
        var y = await ArrangeAsync(db);

        // A real expense on the villa, approved, so the cost is a number and not zero.
        var spent = await expenses.CreateAsync(new SaveProjectExpenseRequest(
            y.ProjectId, Today, y.HeadId, null, "Wiring for the extra points", 12_400m,
            ProjectExpenseType.Direct, PaymentStatus.Paid, null));
        await sp.ApproveIfPendingAsync(ApprovalEntityTypes.ProjectExpense, spent.Id);
        var costBefore = await VillaCostAsync(db, y.ProjectId);
        costBefore.Should().Be(12_400m);

        // Now the change request that work was for: asked, agreed, built. Three writes, each of
        // which is the moment somebody might have been tempted to post a cost.
        var addOn = await addOns.CreateAsync(Request(y, 50_000m));
        await addOns.SetStatusAsync(addOn.Id, AddOnStatus.Agreed, null);
        await addOns.SetStatusAsync(addOn.Id, AddOnStatus.Done, null);

        (await VillaCostAsync(db, y.ProjectId)).Should().Be(costBefore,
            "the expected amount is an estimate and the villa is charged by its expenses alone");
        (await db.ProjectExpenses.CountAsync(e => e.ProjectId == y.ProjectId)).Should().Be(1,
            "the register writes no expense rows of its own");
    }

    [Fact]
    public async Task A_request_is_recorded_with_its_type_its_words_and_an_estimate()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var addOns = sp.GetRequiredService<IAddOnRequestService>();
        var y = await ArrangeAsync(db);

        var addOn = await addOns.CreateAsync(Request(y, 50_000m));

        addOn.TxnNumber.Should().StartWith("ADD");
        addOn.ExpenseHeadName.Should().NotBeNullOrWhiteSpace();
        addOn.Description.Should().Be("Two extra points in the kitchen");
        addOn.ExpectedAmount.Should().Be(50_000m);
        addOn.Status.Should().Be(AddOnStatus.Requested);
    }

    [Fact]
    public async Task A_customer_often_asks_before_anybody_has_a_price()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var addOns = sp.GetRequiredService<IAddOnRequestService>();
        var y = await ArrangeAsync(db);

        // Zero is allowed: refusing the record until somebody invents a number loses the request.
        var addOn = await addOns.CreateAsync(Request(y, 0m, "Move the bedroom wall, price to follow"));
        addOn.ExpectedAmount.Should().Be(0m);

        // What is not allowed is a request nobody can read later.
        var blank = () => addOns.CreateAsync(new SaveAddOnRequest(y.ProjectId, Today, y.HeadId, "  ", 1000m, null));
        await blank.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Declining_a_change_has_to_say_why()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var addOns = sp.GetRequiredService<IAddOnRequestService>();
        var y = await ArrangeAsync(db);
        var addOn = await addOns.CreateAsync(Request(y, 50_000m));

        var silent = () => addOns.SetStatusAsync(addOn.Id, AddOnStatus.Declined, null);
        (await silent.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("why");

        var declined = await addOns.SetStatusAsync(addOn.Id, AddOnStatus.Declined, "Customer withdrew it.");
        declined.Status.Should().Be(AddOnStatus.Declined);
        declined.Remarks.Should().Be("Customer withdrew it.");
    }

    [Fact]
    public async Task The_summary_counts_the_estimates_by_what_was_agreed()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var addOns = sp.GetRequiredService<IAddOnRequestService>();
        var y = await ArrangeAsync(db);

        var asked = await addOns.CreateAsync(Request(y, 50_000m));
        var agreed = await addOns.CreateAsync(Request(y, 18_000m, "Granite instead of tile in the kitchen"));
        var built = await addOns.CreateAsync(Request(y, 7_500m, "Extra tap on the terrace"));
        await addOns.SetStatusAsync(agreed.Id, AddOnStatus.Agreed, null);
        await addOns.SetStatusAsync(built.Id, AddOnStatus.Done, null);

        var summary = await addOns.SummaryAsync(y.ProjectId);

        summary.Requested.Should().Be(1);
        summary.RequestedAmount.Should().Be(50_000m);
        summary.AgreedAmount.Should().Be(18_000m);
        summary.DoneAmount.Should().Be(7_500m);
        asked.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_change_cannot_be_moved_to_another_villa()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var addOns = sp.GetRequiredService<IAddOnRequestService>();
        var y = await ArrangeAsync(db);
        var other = new Project
        {
            Code = "P2", Name = "Villa 14",
            SiteId = await db.Sites.AsNoTracking().Select(s => s.Id).FirstAsync(),
            EstimatedCost = 1_000_000, Status = ProjectStatus.Active
        };
        db.Add(other);
        await db.SaveChangesAsync();
        var addOn = await addOns.CreateAsync(Request(y, 50_000m));

        var act = () => addOns.UpdateAsync(addOn.Id,
            new SaveAddOnRequest(other.Id, Today, y.HeadId, "Same words, different house", 50_000m, null));

        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("another villa");
    }

    [Fact]
    public void The_people_who_stand_in_the_room_can_record_what_was_asked()
    {
        Permissions.ForRole(UserRole.Owner).Should().Contain(Permissions.AddOnManage);
        Permissions.ForRole(UserRole.SubOwner).Should().Contain(Permissions.AddOnManage);
        Permissions.ForRole(UserRole.Engineer).Should().Contain(Permissions.AddOnManage);
        Permissions.ForRole(UserRole.Supervisor).Should().NotContain(Permissions.AddOnManage);
    }
}
