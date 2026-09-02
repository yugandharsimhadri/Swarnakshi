using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Approvals;
using Swarnakshi.Application.Procurement;
using Swarnakshi.Application.Projects;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

public class CostFlowIntegrationTests
{
    [Fact]
    public async Task Purchase_to_consumption_flows_without_double_counting()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        // arrange: a site, a project, pick a seeded material + unit
        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        db.Sites.Add(site);
        var project = new Project { Code = "P1", Name = "Villa 1", Site = site, EstimatedCost = 1_000_000, ContractSaleValue = 2_000_000, Status = ProjectStatus.Active };
        db.Projects.Add(project);
        var supplier = new Supplier { Code = "SUP1", Name = "Supplier 1" };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var material = await db.Materials.Include(m => m.Unit).FirstAsync(m => m.Code == "MAT-CEM-OPC");

        var purchases = sp.GetRequiredService<IPurchaseService>();
        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var approvals = sp.GetRequiredService<IApprovalService>();
        var projects = sp.GetRequiredService<IProjectService>();

        // act 1: two purchases -> weighted average
        foreach (var (qty, rate) in new[] { (100m, 400m), (100m, 450m) })
        {
            var pur = await purchases.CreateAsync(new SavePurchaseRequest(
                supplier.Id, site.Id, null, null, null, DateOnly.FromDateTime(DateTime.UtcNow), 0, null,
                [new PurchaseItemInput(material.Id, material.UnitId, qty, rate, 0, 0)]));
            await sp.SubmitAndApproveAsync(pur.Id);
        }

        var balance = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.SiteId == site.Id && b.MaterialId == material.Id);
        balance.Quantity.Should().Be(200);
        balance.AverageRate.Should().Be(425);
        balance.Value.Should().Be(85_000);

        // act 2: request 50 -> submit -> approve -> issue
        var req = await requests.CreateAsync(new SaveMaterialRequestRequest(
            project.Id, MaterialRequestType.FromStock, DateOnly.FromDateTime(DateTime.UtcNow), null,
            [new MaterialRequestItemInput(material.Id, material.UnitId, 50, null, null)]));
        await requests.SubmitAsync(req.Id);

        var pending = await approvals.ListAsync(new Application.Common.PageQuery { PageSize = 50 }, ApprovalEntityTypes.MaterialRequest, true);
        pending.Items.Should().ContainSingle();
        await approvals.DecideAsync(pending.Items[0].Id, new ApprovalDecision(true, "ok", false));
        await requests.IssueAsync(req.Id, new IssueRequest(null));

        // assert: consumption cost + remaining inventory value == total purchase value
        var afterBalance = await db.InventoryBalances.AsNoTracking().SingleAsync(b => b.SiteId == site.Id && b.MaterialId == material.Id);
        afterBalance.Quantity.Should().Be(150);

        var summary = await projects.SummaryAsync(project.Id);
        summary.MaterialCost.Should().Be(50 * 425m); // 21_250
        (summary.MaterialCost + afterBalance.Value).Should().Be(85_000); // no double counting
        summary.TotalCost.Should().Be(summary.MaterialCost); // nothing else booked
    }

    [Fact]
    public async Task Material_cannot_be_issued_before_approval()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var project = new Project { Code = "P1", Name = "Villa 1", Site = site, Status = ProjectStatus.Active };
        db.AddRange(site, project);
        await db.SaveChangesAsync();
        var material = await db.Materials.FirstAsync(m => m.Code == "MAT-CEM-OPC");

        var requests = sp.GetRequiredService<IMaterialRequestService>();
        var req = await requests.CreateAsync(new SaveMaterialRequestRequest(
            project.Id, MaterialRequestType.FromStock, DateOnly.FromDateTime(DateTime.UtcNow), null,
            [new MaterialRequestItemInput(material.Id, material.UnitId, 10, null, null)]));
        await requests.SubmitAsync(req.Id);

        var act = () => requests.IssueAsync(req.Id, new IssueRequest(null));

        await act.Should().ThrowAsync<Application.Common.AppException>().WithMessage("*must be approved*");
    }
}
