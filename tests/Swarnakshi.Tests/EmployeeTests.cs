using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Common;
using Swarnakshi.Application.Employees;
using Swarnakshi.Application.Projects;
using Swarnakshi.Application.Sites;
using Swarnakshi.Domain.Enums;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>Employee master, salary and advances — including the advance balance arithmetic.</summary>
public class EmployeeTests
{
    private static readonly DateOnly Joined = new(2026, 1, 15);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static SaveEmployeeRequest Valid(string code = "EMP-001", string name = "Suresh Kumar") =>
        new(code, name, "9000000001", 25_000, Joined, null, "Site Supervisor", null, null, null, true);

    private static async Task<Guid> ApproveAsync(IServiceProvider sp, string txnNumber)
    {
        var approvals = sp.GetRequiredService<IApprovalService>();
        var pending = await approvals.ListAsync(new PageQuery { PageSize = 100 }, ApprovalEntityTypes.EmployeePayment, true);
        var item = pending.Items.Single(a => a.EntityRef == txnNumber);
        await approvals.DecideAsync(item.Id, new ApprovalDecision(true, "ok", false));
        return item.EntityId;
    }

    // ---- master ----------------------------------------------------------

    [Fact]
    public async Task Creates_an_employee_with_the_mandatory_details()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();

        var e = await employees.CreateAsync(Valid());

        e.Name.Should().Be("Suresh Kumar");
        e.Phone.Should().Be("9000000001");
        e.MonthlySalary.Should().Be(25_000);
        e.JoinDate.Should().Be(Joined);
        e.IsActive.Should().BeTrue();
        e.AdvanceOutstanding.Should().Be(0);
    }

    [Theory]
    [InlineData("", "9000000001", 25000, "name is required")]
    [InlineData("Suresh", "", 25000, "phone is required")]
    [InlineData("Suresh", "9000000001", 0, "salary is required")]
    public async Task Name_phone_and_salary_are_all_mandatory(string name, string phone, decimal salary, string why)
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();

        var act = () => employees.CreateAsync(
            new SaveEmployeeRequest("EMP-001", name, phone, salary, Joined, null, null, null, null, null, true));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>(why);
    }

    [Fact]
    public async Task A_missing_join_date_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();

        var act = () => employees.CreateAsync(
            new SaveEmployeeRequest("EMP-001", "Suresh", "9000000001", 25_000, default, null, null, null, null, null, true));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Employee_codes_are_unique_within_the_company()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();

        await employees.CreateAsync(Valid("EMP-001", "First"));
        var act = () => employees.CreateAsync(Valid("EMP-001", "Second"));

        await act.Should().ThrowAsync<AppException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Search_finds_an_employee_by_phone_number()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();

        await employees.CreateAsync(Valid("EMP-001", "Suresh Kumar"));
        await employees.CreateAsync(new SaveEmployeeRequest("EMP-002", "Ramesh", "9111111111", 20_000, Joined, null, null, null, null, null, true));

        var found = await employees.ListAsync(new PageQuery { Q = "9111111111", PageSize = 50 }, null, null);

        found.Items.Should().ContainSingle().Which.Name.Should().Be("Ramesh");
    }

    // ---- salary and advances --------------------------------------------

    [Fact]
    public async Task A_salary_payment_reaches_the_ledger_only_after_approval()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var employees = sp.GetRequiredService<IEmployeeService>();
        var payments = sp.GetRequiredService<IEmployeePaymentService>();

        var e = await employees.CreateAsync(Valid());
        var pay = await payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Salary, 25_000, 0,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null, null, null, null));

        (await payments.LedgerAsync(e.Id)).TotalPaid.Should().Be(0, "nothing is paid until it is approved");

        await payments.SubmitAsync(pay.Id);
        await ApproveAsync(sp, pay.TxnNumber);

        (await payments.LedgerAsync(e.Id)).TotalPaid.Should().Be(25_000);
    }

    [Fact]
    public async Task An_advance_is_outstanding_until_a_later_salary_recovers_it()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var employees = sp.GetRequiredService<IEmployeeService>();
        var payments = sp.GetRequiredService<IEmployeePaymentService>();

        var e = await employees.CreateAsync(Valid());

        var advance = await payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Advance, 10_000, 0, null, null, null, null, null, "festival advance"));
        await payments.SubmitAsync(advance.Id);
        await ApproveAsync(sp, advance.TxnNumber);

        (await payments.LedgerAsync(e.Id)).AdvanceOutstanding.Should().Be(10_000);

        // Next month's salary settles part of it: ₹25,000 due, ₹4,000 held back, ₹21,000 handed over.
        var salary = await payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Salary, 25_000, 4_000,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), null, null, null, null));
        salary.NetPaid.Should().Be(21_000);

        await payments.SubmitAsync(salary.Id);
        await ApproveAsync(sp, salary.TxnNumber);

        var ledger = await payments.LedgerAsync(e.Id);
        ledger.AdvancesGiven.Should().Be(10_000);
        ledger.AdvancesRecovered.Should().Be(4_000);
        ledger.AdvanceOutstanding.Should().Be(6_000);
        ledger.TotalPaid.Should().Be(10_000 + 21_000, "cash actually handed over");
    }

    [Fact]
    public async Task Recovering_more_advance_than_is_outstanding_is_refused()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var employees = sp.GetRequiredService<IEmployeeService>();
        var payments = sp.GetRequiredService<IEmployeePaymentService>();

        var e = await employees.CreateAsync(Valid());
        var advance = await payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Advance, 5_000, 0, null, null, null, null, null, null));
        await payments.SubmitAsync(advance.Id);
        await ApproveAsync(sp, advance.TxnNumber);

        var act = () => payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Salary, 25_000, 9_000, null, null, null, null, null, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*only 5000.00 of advance outstanding*");
    }

    [Fact]
    public async Task An_advance_cannot_itself_recover_an_advance()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var employees = sp.GetRequiredService<IEmployeeService>();
        var payments = sp.GetRequiredService<IEmployeePaymentService>();

        var e = await employees.CreateAsync(Valid());

        var act = () => payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Advance, 5_000, 1_000, null, null, null, null, null, null));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Recovering_more_than_the_payment_itself_is_refused()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var employees = sp.GetRequiredService<IEmployeeService>();
        var payments = sp.GetRequiredService<IEmployeePaymentService>();

        var e = await employees.CreateAsync(Valid());

        var act = () => payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Salary, 5_000, 6_000, null, null, null, null, null, null));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task A_payment_charged_to_a_project_becomes_that_projects_labour_cost()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var employees = sp.GetRequiredService<IEmployeeService>();
        var payments = sp.GetRequiredService<IEmployeePaymentService>();
        var sites = sp.GetRequiredService<ISiteService>();
        var projects = sp.GetRequiredService<IProjectService>();

        var site = await sites.CreateAsync(new SaveSiteRequest("S1", "Site 1", null, null, null, null, null, null, SiteStatus.Active, null));
        var project = await projects.CreateAsync(new SaveProjectRequest("P1", "Villa 1", null, site.Id, null, null, null, null, null, null, 500_000, null, ProjectStatus.Active, null));
        var e = await employees.CreateAsync(Valid());

        // Give an advance first, so the salary below genuinely recovers something.
        var advance = await payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Advance, 5_000, 0, null, null, null, null, null, null));
        await payments.SubmitAsync(advance.Id);
        await ApproveAsync(sp, advance.TxnNumber);

        var pay = await payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Salary, 25_000, 5_000, null, null, null, null, project.Id, null));
        await payments.SubmitAsync(pay.Id);
        await ApproveAsync(sp, pay.TxnNumber);

        var summary = await projects.SummaryAsync(project.Id);
        summary.LabourCost.Should().Be(25_000,
            "the gross salary is what the month cost the project — recovering an advance is the employee repaying the company, not a discount");
        summary.TotalCost.Should().Be(25_000);
    }

    [Fact]
    public async Task A_payment_with_no_project_stays_a_company_overhead()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var employees = sp.GetRequiredService<IEmployeeService>();
        var payments = sp.GetRequiredService<IEmployeePaymentService>();
        var sites = sp.GetRequiredService<ISiteService>();
        var projects = sp.GetRequiredService<IProjectService>();

        var site = await sites.CreateAsync(new SaveSiteRequest("S1", "Site 1", null, null, null, null, null, null, SiteStatus.Active, null));
        var project = await projects.CreateAsync(new SaveProjectRequest("P1", "Villa 1", null, site.Id, null, null, null, null, null, null, 500_000, null, ProjectStatus.Active, null));
        var e = await employees.CreateAsync(Valid("EMP-OFF", "Office Accountant"));

        var pay = await payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Salary, 30_000, 0, null, null, null, null, null, null));
        await payments.SubmitAsync(pay.Id);
        await ApproveAsync(sp, pay.TxnNumber);

        (await projects.SummaryAsync(project.Id)).LabourCost.Should().Be(0,
            "office salary must not be loaded onto whichever project happened to be open");
    }

    [Fact]
    public async Task Cannot_pay_an_inactive_employee()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var employees = sp.GetRequiredService<IEmployeeService>();
        var payments = sp.GetRequiredService<IEmployeePaymentService>();

        var e = await employees.CreateAsync(Valid());
        await employees.UpdateAsync(e.Id, Valid() with { IsActive = false });

        var act = () => payments.CreateAsync(new SaveEmployeePaymentRequest(
            e.Id, Today, EmployeePaymentKind.Salary, 25_000, 0, null, null, null, null, null, null));

        await act.Should().ThrowAsync<AppException>().WithMessage("*not an active employee*");
    }
}
