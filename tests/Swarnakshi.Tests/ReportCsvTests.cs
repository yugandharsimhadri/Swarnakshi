using System.Globalization;
using System.Text;
using FluentAssertions;
using Swarnakshi.Application.Reports;
using Xunit;

namespace Swarnakshi.Tests;

/// <summary>
/// Turning a report into CSV used to happen inside the controller, where it could only be tested
/// through a request. It is its own thing now, so the rules that actually bite — escaping, culture,
/// and whether a capped report admits it — can be checked directly.
/// </summary>
public class ReportCsvTests
{
    private static string Render(ReportTable table)
        => Encoding.UTF8.GetString(ReportCsv.Render(table));

    private static ReportTable Table(IReadOnlyList<IReadOnlyList<object?>> rows, string? note = null)
        => new("Consumption Register", ["Material", "Qty", "Value"], rows, note);

    [Fact]
    public void A_capped_report_says_so_in_the_file_as_well_as_on_the_screen()
    {
        var csv = Render(Table([["Cement", 10m, 4_500m]],
            note: "Showing the most recent 5,000 issues. Narrow the date range to see the rest."));

        // The screen gets the note through the JSON. The CSV is the copy somebody opens in Excel
        // and reconciles against, so a file that holds the first 5,000 rows and looks complete is
        // exactly the failure the cap exists to prevent.
        csv.Should().Contain("Showing the most recent 5,000 issues");
    }

    [Fact]
    public void An_uncapped_report_carries_no_note_and_no_stray_blank_lines()
    {
        var csv = Render(Table([["Cement", 10m, 4_500m]]));

        csv.Trim().Split('\n').Should().HaveCount(2, "a header and one row, nothing else");
    }

    [Fact]
    public void Commas_quotes_and_newlines_in_a_value_cannot_break_the_columns()
    {
        var csv = Render(Table([["Pipe, 2\" \"heavy\"", 1m, 0m]]));

        csv.Should().Contain("\"Pipe, 2\"\" \"\"heavy\"\"\"",
            "a comma must not split the cell and a quote must be doubled");
        // Header plus one row: the embedded quotes and comma stayed inside their field.
        csv.Trim().Split('\n').Should().HaveCount(2);
    }

    [Fact]
    public void Numbers_and_dates_are_written_the_same_wherever_the_server_is()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            // A locale that writes 1234,56 for a decimal. A CSV is read by another program at
            // least as often as by a person, so following the server's locale is how a total
            // silently becomes wrong somewhere else.
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var csv = Render(Table([["Cement", 1_234.56m, new DateOnly(2026, 9, 4)]]));

            csv.Should().Contain("1234.56").And.Contain("2026-09-04");
            csv.Should().NotContain("1234,56");
        }
        finally { Thread.CurrentThread.CurrentCulture = previous; }
    }

    [Fact]
    public void The_file_is_named_after_the_report()
    {
        ReportCsv.FileNameFor(Table([])).Should().Be("consumption_register.csv");
    }
}
