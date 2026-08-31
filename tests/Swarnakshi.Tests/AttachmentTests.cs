using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Swarnakshi.Application.Attachments;
using Swarnakshi.Application.Common;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;
using Swarnakshi.Infrastructure.Persistence;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>Attachment upload / list / download / delete round-trip against real file storage.</summary>
public class AttachmentTests
{
    private static Stream Content(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static async Task<Project> ArrangeProjectAsync(AppDbContext db)
    {
        var site = new Site { Code = "S1", Name = "Site 1", Status = SiteStatus.Active };
        var project = new Project { Code = "P1", Name = "Villa 1", Site = site, Status = ProjectStatus.Active };
        db.AddRange(site, project);
        await db.SaveChangesAsync();
        return project;
    }

    [Fact]
    public async Task Uploads_then_downloads_the_same_bytes()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var project = await ArrangeProjectAsync(db);

        var uploaded = await files.UploadAsync("Project", project.Id, "site-plan.txt", "text/plain",
            Content("foundation layout v1"));

        uploaded.FileName.Should().Be("site-plan.txt");
        uploaded.ContentType.Should().Be("text/plain");

        var (stream, name, contentType) = await files.DownloadAsync(uploaded.Id);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Should().Be("foundation layout v1");
        name.Should().Be("site-plan.txt");
        contentType.Should().Be("text/plain");
    }

    [Fact]
    public async Task Lists_only_the_attachments_of_the_requested_entity()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var project = await ArrangeProjectAsync(db);
        var otherId = Guid.NewGuid();

        await files.UploadAsync("Project", project.Id, "a.txt", "text/plain", Content("a"));
        await files.UploadAsync("Project", project.Id, "b.txt", "text/plain", Content("b"));
        await files.UploadAsync("Project", otherId, "elsewhere.txt", "text/plain", Content("c"));

        var mine = await files.ListAsync("Project", project.Id);

        mine.Should().HaveCount(2);
        mine.Select(a => a.FileName).Should().BeEquivalentTo(["a.txt", "b.txt"]);
    }

    [Fact]
    public async Task Attachments_are_scoped_by_entity_type_as_well_as_id()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var project = await ArrangeProjectAsync(db);

        // Same id, different entity type — must not leak across.
        await files.UploadAsync("Project", project.Id, "project.txt", "text/plain", Content("p"));
        await files.UploadAsync("Purchase", project.Id, "purchase.txt", "text/plain", Content("q"));

        (await files.ListAsync("Project", project.Id)).Should().ContainSingle(a => a.FileName == "project.txt");
        (await files.ListAsync("Purchase", project.Id)).Should().ContainSingle(a => a.FileName == "purchase.txt");
    }

    [Fact]
    public async Task Deleting_removes_the_record_and_the_download_stops_working()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<IAttachmentService>();
        var project = await ArrangeProjectAsync(db);

        var uploaded = await files.UploadAsync("Project", project.Id, "temp.txt", "text/plain", Content("scratch"));

        await files.DeleteAsync(uploaded.Id);

        (await files.ListAsync("Project", project.Id)).Should().BeEmpty();
        (await db.Attachments.AsNoTracking().AnyAsync(a => a.Id == uploaded.Id)).Should().BeFalse();

        var download = () => files.DownloadAsync(uploaded.Id);
        await download.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Downloading_an_unknown_attachment_is_a_not_found()
    {
        await using var host = await TestHost.CreateAsync();
        using var scope = host.Scope();
        var files = scope.ServiceProvider.GetRequiredService<IAttachmentService>();

        var act = () => files.DownloadAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
