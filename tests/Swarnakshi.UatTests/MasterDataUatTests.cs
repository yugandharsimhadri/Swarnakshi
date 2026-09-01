using Swarnakshi.Automation;
using Xunit.Abstractions;

namespace Swarnakshi.UatTests;

/// <summary>
/// Finding one exact material among a catalogue of them. What a construction business buys is not
/// "wire" but Polycab 2.5 sq.mm wire, and the catalogue has to be searchable the way the site asks
/// for it — by company, and by specification, not only by name.
/// </summary>
public sealed class MaterialCatalogueUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Finding_a_material_among_many(Viewport viewport) => RunWorkflowAsync("MaterialCatalogue", viewport);
}

/// <summary>
/// Adding an exact material. The specification fields follow the subcategory chosen, so wire is
/// asked for its size and a tile for its dimensions and finish — rather than every material being
/// shown every field that any material might need.
/// </summary>
public sealed class AddMaterialUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Defining_a_material_with_its_specification(Viewport viewport) => RunWorkflowAsync("AddMaterial", viewport);
}

/// <summary>
/// Retiring a material without losing what it was used for. A material that has been bought and
/// consumed cannot be deleted — the history must keep resolving — so it is deactivated, which takes
/// it out of new transactions and leaves every past record standing.
/// </summary>
public sealed class MaterialLifecycleUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Retiring_and_restoring_a_material(Viewport viewport) => RunWorkflowAsync("MaterialLifecycle", viewport);
}

/// <summary>
/// The contractors the business buys labour from, held once with their tax and bank details so a
/// payment references a record rather than a name typed differently each time.
/// </summary>
public sealed class ContractorMasterUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Keeping_the_contractor_list(Viewport viewport) => RunWorkflowAsync("ContractorMaster", viewport);
}

/// <summary>
/// The customers the business sells to, and what already references them — which is precisely why a
/// customer is retired rather than deleted.
/// </summary>
public sealed class CustomerMasterUatTests(UatFixture fixture, ITestOutputHelper output) : UatTestBase(fixture, output)
{
    [Theory]
    [MemberData(nameof(BothViewports))]
    public Task Keeping_the_customer_list(Viewport viewport) => RunWorkflowAsync("CustomerMaster", viewport);
}
