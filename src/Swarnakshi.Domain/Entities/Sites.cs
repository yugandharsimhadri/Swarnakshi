using Swarnakshi.Domain.Common;
using Swarnakshi.Domain.Enums;

namespace Swarnakshi.Domain.Entities;

public class Site : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pin { get; set; }
    public Guid? SupervisorUserId { get; set; }
    public User? Supervisor { get; set; }
    public DateOnly? StartDate { get; set; }
    public SiteStatus Status { get; set; } = SiteStatus.Active;
    public string? Notes { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<InventoryBalance> InventoryBalances { get; set; } = new List<InventoryBalance>();
}

public class Project : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? VillaNumber { get; set; }

    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid? ProjectTypeId { get; set; }
    public ProjectType? ProjectType { get; set; }

    public string? Address { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? ExpectedCompletionDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }

    public decimal EstimatedCost { get; set; }
    public decimal? ContractSaleValue { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;
    public string? Notes { get; set; }
}
