using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Reports;
using Swarnakshi.Application.Security;

namespace Swarnakshi.Api.Controllers;


[ApiController]
[Route("api/reports")]
[Authorize]
[TenantOnly]
[RequiresPermission(Permissions.ReportsView)]
public class ReportsController(IReportsService reports) : ControllerBase
{
    [HttpGet("inventory/stock")]
    public Task<IActionResult> Stock([FromQuery] Guid? siteId, [FromQuery] string? format, CancellationToken ct)
        => Result(reports.InventoryStockAsync(siteId, ct), format);

    [HttpGet("inventory/low-stock")]
    public Task<IActionResult> LowStock([FromQuery] string? format, CancellationToken ct)
        => Result(reports.LowStockAsync(ct), format);

    [HttpGet("inventory/purchase-register")]
    public Task<IActionResult> PurchaseRegister([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? siteId, [FromQuery] string? format, CancellationToken ct)
        => Result(reports.PurchaseRegisterAsync(from, to, siteId, ct), format);

    [HttpGet("inventory/consumption")]
    public Task<IActionResult> Consumption([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] Guid? projectId, [FromQuery] string? format, CancellationToken ct)
        => Result(reports.ConsumptionRegisterAsync(from, to, projectId, ct), format);

    [HttpGet("project/cost-summary")]
    public Task<IActionResult> ProjectCostSummary([FromQuery] string? format, CancellationToken ct)
        => Result(reports.ProjectCostSummaryAsync(ct), format);

    [HttpGet("contractor/outstanding")]
    public Task<IActionResult> ContractorOutstanding([FromQuery] string? format, CancellationToken ct)
        => Result(reports.ContractorOutstandingAsync(ct), format);

    [HttpGet("customer/outstanding")]
    public Task<IActionResult> CustomerOutstanding([FromQuery] string? format, CancellationToken ct)
        => Result(reports.CustomerOutstandingAsync(ct), format);

    [HttpGet("company/summary")]
    public Task<IActionResult> CompanySummary([FromQuery] string? format, CancellationToken ct)
        => Result(reports.CompanySummaryAsync(ct), format);

    [HttpGet("project/profitability")]
    public Task<IActionResult> VillaProfitability([FromQuery] string? format, CancellationToken ct)
        => Result(reports.VillaProfitabilityAsync(ct), format);

    [HttpGet("project/budget-burn")]
    public Task<IActionResult> BudgetBurn([FromQuery] string? format, CancellationToken ct)
        => Result(reports.BudgetBurnAsync(ct), format);

    [HttpGet("site/summary")]
    public Task<IActionResult> SiteSummary([FromQuery] string? format, CancellationToken ct)
        => Result(reports.SiteSummaryAsync(ct), format);

    [HttpGet("contractor/commitment")]
    public Task<IActionResult> ContractorCommitment([FromQuery] string? format, CancellationToken ct)
        => Result(reports.ContractorCommitmentAsync(ct), format);

    [HttpGet("supplier/outstanding")]
    public Task<IActionResult> SupplierOutstanding([FromQuery] string? format, CancellationToken ct)
        => Result(reports.SupplierOutstandingAsync(ct), format);

    /// <summary>
    /// The same report in whichever representation was asked for. The controller's whole job:
    /// choose between JSON and a file. How a table becomes CSV is ReportCsv's business.
    /// </summary>
    private async Task<IActionResult> Result(Task<ReportTable> task, string? format)
    {
        var table = await task;

        return string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
            ? File(ReportCsv.Render(table), "text/csv", ReportCsv.FileNameFor(table))
            : this.Envelope(table);
    }
}
