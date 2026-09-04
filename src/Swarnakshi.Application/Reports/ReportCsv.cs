using System.Globalization;
using System.Text;

namespace Swarnakshi.Application.Reports;

/// <summary>
/// Renders a <see cref="ReportTable"/> as CSV.
///
/// <para>Here rather than in the controller it used to live in: turning a table into text is about
/// the table, not about HTTP, and a controller that does it is doing two jobs. Kept pure — no
/// ASP.NET types — so the escaping rules can be tested directly instead of through a request.</para>
/// </summary>
public static class ReportCsv
{
    /// <summary>The file name a browser should save this report under.</summary>
    public static string FileNameFor(ReportTable table)
        => $"{table.Title.Replace(' ', '_').ToLowerInvariant()}.csv";

    public static byte[] Render(ReportTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", table.Columns.Select(Escape)));

        foreach (var row in table.Rows)
            sb.AppendLine(string.Join(",", row.Select(Cell).Select(Escape)));

        // A capped report has to say so here too. The note reaches the screen through the JSON, but
        // the CSV is the copy someone opens in Excel and reconciles against — and a file that
        // silently holds the first 5,000 rows while looking complete is the whole reason the cap
        // announces itself at all.
        if (!string.IsNullOrWhiteSpace(table.Note))
        {
            sb.AppendLine();
            sb.AppendLine(Escape(table.Note));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Invariant culture throughout: a CSV is read by another program as often as by a person, and
    /// a decimal separator that follows the server's locale is how a total silently becomes wrong
    /// somewhere else.
    /// </summary>
    private static string Cell(object? value) => value switch
    {
        null => "",
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static string Escape(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r')
            ? $"\"{s.Replace("\"", "\"\"")}\""
            : s;
}
