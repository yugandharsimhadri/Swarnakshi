namespace Swarnakshi.Automation;

/// <summary>
/// The fixed points of the seeded dataset the UAT signs in against and asserts on. Kept in one place
/// so a change to the seed is a change here, not a hunt through every workflow.
///
/// These mirror MasterDataSeeder / DemoDataSeeder: the owner comes from the Seed section of
/// appsettings, the sites and projects from the Development-only demo seed.
/// </summary>
public static class DemoData
{
    public const string OwnerEmail = "owner@swarnakshi.local";
    public const string OwnerPassword = "Owner@123";
    public const string OwnerName = "Owner";

    /// <summary>Demo sites, from DemoDataSeeder.</summary>
    public const string PrimarySite = "Green Valley";
    public const string SecondarySite = "Sunrise Villas";

    /// <summary>A demo project on the primary site.</summary>
    public const string PrimaryProject = "Villa 101";

    /// <summary>A seeded material whose code is referenced by the backend tests too — stable by contract.</summary>
    public const string CementCode = "MAT-CEM-OPC";
    public const string CementName = "OPC 53 Grade Cement";

    /// <summary>A seeded material carrying specifications, used to show the spec summary rendering.</summary>
    public const string WireCode = "MAT-ELC-WIRE";
    public const string WireSpecSummary = "2.5 sq.mm";

    /// <summary>Categories the 50-category taxonomy guarantees, used for filter assertions.</summary>
    public const string CementCategory = "Cement";
    public const string ElectricalWireCategory = "Electrical Wire";
}
