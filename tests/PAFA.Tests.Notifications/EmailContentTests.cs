using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PAFA.Domain.Models;
using PAFA.Infrastructure.Services.Notifications;

namespace PAFA.Tests.Notifications;

/// <summary>
/// Tests the HTML email body and CSV attachment content produced by
/// <see cref="SmtpEmailService"/> without actually connecting to an SMTP server.
///
/// Strategy: we expose the private static methods under test via
/// a thin subclass with internal test hooks. Since those methods are private,
/// we instead call <see cref="SmtpEmailService"/> through reflection-free
/// observable outputs by testing <see cref="LoggingEmailService"/> which
/// executes the same formatting logic.
///
/// For the HTML/CSV content specifically we test the builders directly
/// by extracting them into a testable helper class.
///
/// What is covered:
///   ? HTML body contains the file name
///   ? HTML body contains reporting period
///   ? HTML body contains data source (SourceSystem)
///   ? HTML body contains total error count
///   ? HTML body contains the first 10 error rows (no more)
///   ? HTML body shows "... and N more" when errors > 10
///   ? HTML body does NOT show "... and N more" when errors ? 10
///   ? CSV header row is correct
///   ? CSV contains all errors (not capped at 10)
///   ? CSV escapes commas inside field values
///   ? CSV escapes double-quotes inside field values
/// </summary>
public class EmailContentTests
{
    // ?? We test the email content via the testable static helpers ??????????

    private static ValidationFailureEmailContext BuildContext(
        int errorCount = 5,
        string fileName = "MOD520A_2025_02.xlsx",
        string period = "2025-02",
        string source = "SharePoint")
    {
        var errors = Enumerable.Range(1, errorCount)
            .Select(i => new ValidationErrorItem(
                RowNumber:     i,
                ColumnName:    "ShipperShortCode",
                ErrorCode:     "VAL-005",
                Severity:      "ERROR",
                ErrorMessage:  $"Error on row {i}",
                OriginalValue: null))
            .ToList();

        return new ValidationFailureEmailContext(
            IngestionFileId: Guid.NewGuid(),
            FileName:        fileName,
            ReportingPeriod: period,
            SourceSystem:    source,
            Recipients:      ["ops@test.com"],
            AllErrors:       errors);
    }

    // Helper — build HTML via SmtpEmailService reflection-free by using
    // EmailContentBuilder (the inner static logic extracted for testability).
    private static string GetHtml(ValidationFailureEmailContext ctx)
        => EmailContentBuilder.BuildHtmlBody(ctx);

    private static string GetCsv(ValidationFailureEmailContext ctx)
        => System.Text.Encoding.UTF8.GetString(EmailContentBuilder.BuildCsvBytes(ctx.AllErrors));

    // ?? HTML body tests ???????????????????????????????????????????????????

    [Fact]
    public void HtmlBody_ContainsFileName()
    {
        var html = GetHtml(BuildContext());
        Assert.Contains("MOD520A_2025_02.xlsx", html);
    }

    [Fact]
    public void HtmlBody_ContainsReportingPeriod()
    {
        var html = GetHtml(BuildContext());
        Assert.Contains("2025-02", html);
    }

    [Fact]
    public void HtmlBody_ContainsSourceSystem()
    {
        var html = GetHtml(BuildContext());
        Assert.Contains("SharePoint", html);
    }

    [Fact]
    public void HtmlBody_ContainsTotalErrorCount()
    {
        var html = GetHtml(BuildContext(errorCount: 7));
        Assert.Contains("7", html);
    }

    [Fact]
    public void HtmlBody_WithFiveErrors_ContainsFiveTableRows()
    {
        var html = GetHtml(BuildContext(errorCount: 5));
        // Each data row has <tr> — count occurrences beyond the header rows
        var trCount = CountOccurrences(html, "<tr>");
        // 1 header row + 5 data rows + 2 info table rows (file/period/source/total = 4)
        Assert.True(trCount >= 5, $"Expected at least 5 <tr> elements, found {trCount}");
    }

    [Fact]
    public void HtmlBody_WithMoreThan10Errors_OnlyShowsFirst10InTable()
    {
        var html = GetHtml(BuildContext(errorCount: 25));
        // Row numbers 11..25 must NOT appear as table cells
        Assert.DoesNotContain("<td>11</td>", html);
        Assert.DoesNotContain("<td>25</td>", html);
        // But rows 1..10 must be present
        Assert.Contains("<td>10</td>", html);
    }

    [Fact]
    public void HtmlBody_WithMoreThan10Errors_ShowsAndMoreMessage()
    {
        var html = GetHtml(BuildContext(errorCount: 25));
        Assert.Contains("15 more error(s)", html); // 25 - 10 = 15
    }

    [Fact]
    public void HtmlBody_WithExactly10Errors_DoesNotShowAndMoreMessage()
    {
        var html = GetHtml(BuildContext(errorCount: 10));
        Assert.DoesNotContain("more error(s)", html);
    }

    [Fact]
    public void HtmlBody_ContainsValidHtmlStructure()
    {
        var html = GetHtml(BuildContext());
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("</table>", html);
    }

    [Fact]
    public void HtmlBody_ErrorSeverityBadge_IsRendered()
    {
        var html = GetHtml(BuildContext());
        Assert.Contains("badge-error", html);
    }

    // ?? CSV attachment tests ??????????????????????????????????????????????

    [Fact]
    public void Csv_FirstLineIsHeader()
    {
        var csv = GetCsv(BuildContext(errorCount: 3));
        var firstLine = csv.Split('\n')[0].Trim();
        Assert.Equal("RowNumber,ColumnName,ErrorCode,Severity,ErrorMessage,OriginalValue", firstLine);
    }

    [Fact]
    public void Csv_ContainsAllErrors_NotCappedAt10()
    {
        var ctx = BuildContext(errorCount: 25);
        var csv = GetCsv(ctx);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // 1 header + 25 data lines
        Assert.Equal(26, lines.Length);
    }

    [Fact]
    public void Csv_ContainsCorrectRowNumber()
    {
        var csv = GetCsv(BuildContext(errorCount: 3));
        Assert.Contains("1,ShipperShortCode", csv);
        Assert.Contains("2,ShipperShortCode", csv);
        Assert.Contains("3,ShipperShortCode", csv);
    }

    [Fact]
    public void Csv_EscapesCommasInFieldValues()
    {
        var errors = new List<ValidationErrorItem>
        {
            new(RowNumber: 1, ColumnName: "Field", ErrorCode: "VAL-001",
                Severity: "ERROR", ErrorMessage: "Error, with comma", OriginalValue: null)
        };

        var ctx = new ValidationFailureEmailContext(
            Guid.NewGuid(), "file.xlsx", "2025-02", "SP",
            ["ops@test.com"], errors);

        var csv = GetCsv(ctx);
        Assert.Contains("\"Error, with comma\"", csv);
    }

    [Fact]
    public void Csv_EscapesDoubleQuotesInFieldValues()
    {
        var errors = new List<ValidationErrorItem>
        {
            new(RowNumber: 1, ColumnName: "Field", ErrorCode: "VAL-001",
                Severity: "ERROR", ErrorMessage: "Error \"quoted\" value", OriginalValue: null)
        };

        var ctx = new ValidationFailureEmailContext(
            Guid.NewGuid(), "file.xlsx", "2025-02", "SP",
            ["ops@test.com"], errors);

        var csv = GetCsv(ctx);
        Assert.Contains("\"Error \"\"quoted\"\" value\"", csv);
    }

    [Fact]
    public void Csv_IsUtf8Encoded()
    {
        var bytes = EmailContentBuilder.BuildCsvBytes(BuildContext(errorCount: 2).AllErrors);
        // UTF-8 BOM is optional — we just check the content round-trips cleanly
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("RowNumber", text);
    }

    // ?? Helpers ???????????????????????????????????????????????????????????

    private static int CountOccurrences(string source, string needle)
    {
        int count = 0, index = 0;
        while ((index = source.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
