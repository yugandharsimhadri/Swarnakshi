using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swarnakshi.Api.Common;
using Swarnakshi.Application.Dashboard;
using Swarnakshi.Application.Reports;
using Swarnakshi.Application.Security;

namespace Swarnakshi.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
[TenantOnly]
public class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.Envelope(await dashboard.GetAsync(ct));
}

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

    private async Task<IActionResult> Result(Task<ReportTable> task, string? format)
    {
        var table = await task;
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            return this.Envelope(table);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", table.Columns.Select(Csv)));
        foreach (var row in table.Rows)
            sb.AppendLine(string.Join(",", row.Select(FormatCell).Select(Csv)));

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var name = table.Title.Replace(' ', '_').ToLowerInvariant();
        return File(bytes, "text/csv", $"{name}.csv");
    }

    private static string FormatCell(object? v) => v switch
    {
        null => "",
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };

    private static string Csv(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
