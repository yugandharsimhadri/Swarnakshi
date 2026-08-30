using Microsoft.EntityFrameworkCore;
using Swarnakshi.Domain.Entities;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Infrastructure.Persistence.Seed;

/// <summary>Development-only demo data. Every row is tagged IsDemo=true so it can be purged safely.</summary>
public static class DemoDataSeeder
{
    public static async Task RunAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Sites.AnyAsync(s => s.IsDemo, ct)) return;

        var villaType = await db.ProjectTypes.FirstOrDefaultAsync(t => t.Name == "Villa", ct);

        var green = new Site { Code = "GV", Name = "Green Valley", City = "Hyderabad", State = "Telangana", Status = SiteStatus.Active, IsDemo = true };
        var sunrise = new Site { Code = "SR", Name = "Sunrise Villas", City = "Vijayawada", State = "Andhra Pradesh", Status = SiteStatus.Active, IsDemo = true };
        db.Sites.AddRange(green, sunrise);

        var cust = new Customer { Code = "CUST-001", Name = "Ramesh Kumar", Mobile = "9000000001", IsDemo = true };
        db.Customers.Add(cust);

        db.Projects.AddRange(
            new Project { Code = "GV-101", Name = "Villa 101", VillaNumber = "101", Site = green, Customer = cust, ProjectTypeId = villaType?.Id, EstimatedCost = 5_000_000, ContractSaleValue = 8_000_000, Status = ProjectStatus.Active, IsDemo = true },
            new Project { Code = "GV-102", Name = "Villa 102", VillaNumber = "102", Site = green, ProjectTypeId = villaType?.Id, EstimatedCost = 5_200_000, Status = ProjectStatus.Active, IsDemo = true },
            new Project { Code = "SR-103", Name = "Villa 103", VillaNumber = "103", Site = sunrise, ProjectTypeId = villaType?.Id, EstimatedCost = 4_800_000, Status = ProjectStatus.Planned, IsDemo = true });

        await db.SaveChangesAsync(ct);
    }
}
