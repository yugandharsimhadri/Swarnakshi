using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Contractors;
using Swarnakshi.Application.Expenses;
using Swarnakshi.Application.Projects;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

public class PaymentFlowTests
{
    private static async Task<(TestHost host, IServiceScope scope, AppDbContext db, Project project)> SetupAsync()
    {
        var host = await TestHost.CreateAsync();
        var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var project = new Project { Code = "P1", Name = "Villa 1", Site = site, EstimatedCost = 500_000, Status = ProjectStatus.Active };
        db.AddRange(site, project);
        await db.SaveChangesAsync();
        return (host, scope, db, project);
    }

    [Fact]
    public async Task Labour_entry_posts_project_cost_only_after_approval()
    {
        var (host, scope, db, project) = await SetupAsync();
        await using var _ = host;
        var sp = scope.ServiceProvider;
        var labour = sp.GetRequiredService<ILabourService>();
        var approvals = sp.GetRequiredService<IApprovalService>();
        var projects = sp.GetRequiredService<IProjectService>();
        var cat = await db.LabourCategories.FirstAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var entry = await labour.CreateAsync(new SaveLabourEntryRequest(
            project.Id, cat.Id, LabourPeriodType.Daily, today, today, 8_000, null, "Daily", null));
        await labour.SubmitAsync(entry.Id);

        (await projects.SummaryAsync(project.Id)).LabourCost.Should().Be(0);

        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 }, ApprovalEntityTypes.LabourEntry, true);
        await approvals.DecideAsync(pending.Items[0].Id, new ApprovalDecision(true, "ok", false));

        (await projects.SummaryAsync(project.Id)).LabourCost.Should().Be(8_000);
    }

    [Fact]
    public async Task Contractor_payment_over_balance_is_blocked_without_override()
    {
        var (host, scope, db, project) = await SetupAsync();
        await using var _ = host;
        var sp = scope.ServiceProvider;
        var contracts = sp.GetRequiredService<IContractWorkService>();
        var payments = sp.GetRequiredService<IContractorPaymentService>();
        var approvals = sp.GetRequiredService<IApprovalService>();

        var contractor = new Contractor { Code = "C1", Name = "ABC" };
        db.Contractors.Add(contractor);
        await db.SaveChangesAsync();
        var pm = await db.PaymentMethods.FirstAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var work = await contracts.CreateAsync(new SaveContractWorkRequest(
            project.Id, contractor.Id, "Plumbing", null, 90_000, 100_000, null, null, null, ContractWorkStatus.Active));

        var pay = await payments.CreateAsync(new SaveContractorPaymentRequest(
            contractor.Id, project.Id, work.Id, today, 150_000, pm.Id, null, null, ContractorPaymentKind.Partial));
        await payments.SubmitAsync(pay.Id);

        var pending = await approvals.ListAsync(new PageQuery { PageSize = 50 }, ApprovalEntityTypes.ContractorPayment, true);

        var block = () => approvals.DecideAsync(pending.Items[0].Id, new ApprovalDecision(true, "x", AllowOverride: false));
        await block.Should().ThrowAsync<AppException>().WithMessage("*exceeds contract balance*");

        // with override it posts and drives the balance negative (advance)
        await approvals.DecideAsync(pending.Items[0].Id, new ApprovalDecision(true, "advance", AllowOverride: true));
        var after = await contracts.GetAsync(work.Id);
        after.TotalPaid.Should().Be(150_000);
        after.Balance.Should().Be(-50_000);
    }

    [Fact]
    public async Task Customer_receipt_requires_a_customer_on_the_project()
    {
        var (host, scope, db, project) = await SetupAsync();
        await using var _ = host;
        var sp = scope.ServiceProvider;
        var payments = sp.GetRequiredService<Application.Customers.ICustomerPaymentService>();
        var pm = await db.PaymentMethods.FirstAsync();

        var act = () => payments.CreateAsync(new Application.Customers.SaveCustomerPaymentRequest(
            project.Id, DateOnly.FromDateTime(DateTime.UtcNow), 100_000, pm.Id, null, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*no customer*");
    }
}
