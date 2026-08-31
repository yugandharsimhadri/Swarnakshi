using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task Stale_update_to_an_auditable_entity_is_rejected()
    {
        await using var host = await TestHost.CreateAsync();

        Guid projectId;
        using (var scope = host.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
            var project = new Project { Code = "P1", Name = "Villa 1", Site = site, Status = ProjectStatus.Active };
            db.AddRange(site, project);
            var expense = new ProjectExpense
            {
                TxnNumber = "EXP-2026-00001", Project = project, Date = DateOnly.FromDateTime(DateTime.UtcNow),
                ExpenseHeadId = db.ExpenseHeads.First().Id, Amount = 1000,
                ExpenseType = ProjectExpenseType.Direct, Status = TransactionStatus.Posted
            };
            db.ProjectExpenses.Add(expense);
            await db.SaveChangesAsync();
            projectId = expense.Id;
        }

        // two independent contexts load the same row
        using var scopeA = host.Scope();
        using var scopeB = host.Scope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();

        var rowA = await dbA.ProjectExpenses.FirstAsync(e => e.Id == projectId);
        var rowB = await dbB.ProjectExpenses.FirstAsync(e => e.Id == projectId);

        rowA.Remarks = "edited by A";
        await dbA.SaveChangesAsync();

        rowB.Remarks = "edited by B (stale)";
        var act = () => dbB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
