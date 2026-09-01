using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Projects;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Progress tracking: how far along each project is, and how the book of work divides into
/// not-started, under-way and finished.
///
/// Progress is deliberately NOT derived from money spent. On a villa the two diverge constantly —
/// the material for a whole slab is bought on day one — so the percentage is entered by the people
/// on site, and these tests pin the rules that keep it consistent with the project's stage.
/// </summary>
public class ProjectProgressTests
{
    private static async Task<Guid> SiteAsync(TestHost host, string code = "GV", string name = "Green Valley")
    {
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var site = new Site { Code = code, Name = name, Status = SiteStatus.Active };
        db.Sites.Add(site);
        await db.SaveChangesAsync();
        return site.Id;
    }

    private static SaveProjectRequest Project(
        Guid siteId, string code, ProjectStatus status, int percent = 0) =>
        new(code, "Villa " + code, null, siteId, null, null, null, null, null, null,
            1_000_000m, 2_000_000m, status, percent, null);

    // ---- the counts ------------------------------------------------------

    [Fact]
    public async Task The_book_of_work_divides_into_not_started_under_way_and_finished()
    {
        await using var host = await TestHost.CreateAsync();
        var siteId = await SiteAsync(host);
        using var scope = host.Scope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();

        await projects.CreateAsync(Project(siteId, "P1", ProjectStatus.Planned));
        await projects.CreateAsync(Project(siteId, "P2", ProjectStatus.Planned));
        await projects.CreateAsync(Project(siteId, "P3", ProjectStatus.Active, 40));
        await projects.CreateAsync(Project(siteId, "P4", ProjectStatus.OnHold, 60));
        await projects.CreateAsync(Project(siteId, "P5", ProjectStatus.Completed));
        await projects.CreateAsync(Project(siteId, "P6", ProjectStatus.Cancelled));

        var progress = await projects.ProgressSummaryAsync(null);

        progress.Total.Should().Be(6);
        progress.NotStarted.Should().Be(2);
        progress.InProgress.Should().Be(2, "on hold is work that started and stopped, not work never begun");
        progress.OnHold.Should().Be(1, "and it is still reported on its own");
        progress.Completed.Should().Be(1);
        progress.Cancelled.Should().Be(1);
    }

    [Fact]
    public async Task A_cancelled_project_is_counted_apart_from_the_work_still_to_come()
    {
        await using var host = await TestHost.CreateAsync();
        var siteId = await SiteAsync(host);
        using var scope = host.Scope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();

        await projects.CreateAsync(Project(siteId, "P1", ProjectStatus.Cancelled));

        var progress = await projects.ProgressSummaryAsync(null);

        progress.Cancelled.Should().Be(1);
        progress.NotStarted.Should().Be(0,
            "counting a cancelled villa as not-started would overstate the work still to come");
        progress.InProgress.Should().Be(0);
    }

    [Fact]
    public async Task The_average_covers_only_the_work_actually_under_way()
    {
        await using var host = await TestHost.CreateAsync();
        var siteId = await SiteAsync(host);
        using var scope = host.Scope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();

        await projects.CreateAsync(Project(siteId, "P1", ProjectStatus.Active, 30));
        await projects.CreateAsync(Project(siteId, "P2", ProjectStatus.Active, 70));
        // Neither of these may drag the average: one has not begun, the other is finished.
        await projects.CreateAsync(Project(siteId, "P3", ProjectStatus.Planned));
        await projects.CreateAsync(Project(siteId, "P4", ProjectStatus.Completed));

        var progress = await projects.ProgressSummaryAsync(null);

        progress.AverageCompletionOfInProgress.Should().Be(50);
    }

    [Fact]
    public async Task Counts_can_be_narrowed_to_one_site()
    {
        await using var host = await TestHost.CreateAsync();
        var green = await SiteAsync(host);
        var sunrise = await SiteAsync(host, "SR", "Sunrise Villas");

        using var scope = host.Scope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();
        await projects.CreateAsync(Project(green, "G1", ProjectStatus.Active, 50));
        await projects.CreateAsync(Project(sunrise, "S1", ProjectStatus.Planned));
        await projects.CreateAsync(Project(sunrise, "S2", ProjectStatus.Completed));

        var only = await projects.ProgressSummaryAsync(sunrise);

        only.Total.Should().Be(2);
        only.NotStarted.Should().Be(1);
        only.Completed.Should().Be(1);
        only.InProgress.Should().Be(0);
    }

    [Fact]
    public async Task An_empty_book_of_work_reports_zeroes_rather_than_failing()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();

        var progress = await scope.ServiceProvider.GetRequiredService<IProjectService>()
            .ProgressSummaryAsync(null);

        progress.Total.Should().Be(0);
        progress.AverageCompletionOfInProgress.Should().Be(0, "averaging nothing must not throw");
    }

    // ---- keeping percentage and stage consistent -------------------------

    [Fact]
    public async Task Completing_a_project_settles_it_at_a_hundred_percent()
    {
        await using var host = await TestHost.CreateAsync();
        var siteId = await SiteAsync(host);
        using var scope = host.Scope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();

        var p = await projects.CreateAsync(Project(siteId, "P1", ProjectStatus.Active, 90));

        var done = await projects.UpdateAsync(p.Id, Project(siteId, "P1", ProjectStatus.Completed, 90));

        done.CompletionPercent.Should().Be(100,
            "a finished villa left reading 90% would skew the average of what is under way forever");
    }

    [Fact]
    public async Task A_project_that_has_not_started_cannot_report_progress()
    {
        await using var host = await TestHost.CreateAsync();
        var siteId = await SiteAsync(host);
        using var scope = host.Scope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();

        var act = () => projects.CreateAsync(Project(siteId, "P1", ProjectStatus.Planned, 25));

        await act.Should().ThrowAsync<ValidationException>(
            "if there is progress to report the project is under way, and its status should say so");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Progress_outside_zero_to_a_hundred_is_refused(int percent)
    {
        await using var host = await TestHost.CreateAsync();
        var siteId = await SiteAsync(host);
        using var scope = host.Scope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();

        var act = () => projects.CreateAsync(Project(siteId, "P1", ProjectStatus.Active, percent));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Progress_entered_on_site_survives_a_round_trip()
    {
        await using var host = await TestHost.CreateAsync();
        var siteId = await SiteAsync(host);
        using var scope = host.Scope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectService>();

        var created = await projects.CreateAsync(Project(siteId, "P1", ProjectStatus.Active, 35));
        created.CompletionPercent.Should().Be(35);

        var updated = await projects.UpdateAsync(created.Id, Project(siteId, "P1", ProjectStatus.Active, 65));
        updated.CompletionPercent.Should().Be(65);

        (await projects.GetAsync(created.Id)).CompletionPercent.Should().Be(65);
    }
}
