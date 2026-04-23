using System.Text;
using PAFA.Domain.Models;

namespace PAFA.Infrastructure.Services.Notifications;

/// <summary>
/// Extracts the pure HTML-body and CSV-attachment building logic from
/// <see cref="SmtpEmailService"/> into a stateless, dependency-free helper
/// so that unit tests can validate email content without an SMTP connection.
/// </summary>
public static class EmailContentBuilder
{
    public static string BuildHtmlBody(ValidationFailureEmailContext ctx)
    {
        var preview = ctx.AllErrors.Take(10).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8"/>
              <style>
                body { font-family: Calibri, Arial, sans-serif; font-size: 14px; color: #333; }
                h2   { color: #c0392b; }
                table { border-collapse: collapse; width: 100%; margin-top: 16px; }
                th { background-color: #2c3e50; color: #fff; padding: 8px 10px; text-align: left; }
                td { padding: 7px 10px; border-bottom: 1px solid #ddd; }
                tr:nth-child(even) { background-color: #f9f9f9; }
                .badge-error   { background:#e74c3c; color:#fff; border-radius:4px; padding:2px 6px; }
                .badge-warning { background:#f39c12; color:#fff; border-radius:4px; padding:2px 6px; }
                .badge-info    { background:#2980b9; color:#fff; border-radius:4px; padding:2px 6px; }
                .footer { margin-top: 24px; font-size: 11px; color: #888; }
              </style>
            </head>
            <body>
            """);

        sb.AppendLine($"<h2>? Validation Failure — {Encode(ctx.FileName)}</h2>");

        sb.AppendLine("<table style='border-collapse:collapse;width:auto;margin-bottom:16px'>");
        AppendInfoRow(sb, "File Name",        ctx.FileName);
        AppendInfoRow(sb, "Reporting Period", ctx.ReportingPeriod);
        AppendInfoRow(sb, "Data Source",      ctx.SourceSystem);
        AppendInfoRow(sb, "Total Errors",     ctx.AllErrors.Count.ToString());
        sb.AppendLine("</table>");

        sb.AppendLine($"<p>The file failed validation with <strong>{ctx.AllErrors.Count}</strong> error(s). " +
                       "Please review the attached CSV file for the complete list.<br/>" +
                       $"Below are the first {preview.Count} entries:</p>");

        sb.AppendLine("""
            <table>
              <thead>
                <tr>
                  <th>#</th><th>Row</th><th>Field</th><th>Rule</th>
                  <th>Severity</th><th>Error Message</th><th>Original Value</th>
                </tr>
              </thead>
              <tbody>
            """);

        for (int i = 0; i < preview.Count; i++)
        {
            var e = preview[i];
            var badge = e.Severity.ToUpperInvariant() switch
            {
                "WARNING" => "badge-warning",
                "INFO"    => "badge-info",
                _         => "badge-error"
            };

            sb.AppendLine($"""
                <tr>
                  <td>{i + 1}</td>
                  <td>{(e.RowNumber.HasValue ? e.RowNumber.ToString() : "—")}</td>
                  <td>{Encode(e.ColumnName ?? "—")}</td>
                  <td>{Encode(e.ErrorCode)}</td>
                  <td><span class="{badge}">{Encode(e.Severity)}</span></td>
                  <td>{Encode(e.ErrorMessage)}</td>
                  <td>{Encode(e.OriginalValue ?? "—")}</td>
                </tr>
                """);
        }

        sb.AppendLine("</tbody></table>");

        if (ctx.AllErrors.Count > 10)
            sb.AppendLine($"<p><em>... and {ctx.AllErrors.Count - 10} more error(s). See the attached CSV for the full list.</em></p>");

        sb.AppendLine("""
            <div class="footer">
              This is an automated message from the PAFA platform. Please do not reply to this email.
            </div>
            </body></html>
            """);

        return sb.ToString();
    }

    public static byte[] BuildCsvBytes(IReadOnlyList<ValidationErrorItem> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RowNumber,ColumnName,ErrorCode,Severity,ErrorMessage,OriginalValue");

        foreach (var e in errors)
        {
            sb.AppendLine(string.Join(",",
                Escape(e.RowNumber?.ToString() ?? ""),
                Escape(e.ColumnName ?? ""),
                Escape(e.ErrorCode),
                Escape(e.Severity),
                Escape(e.ErrorMessage),
                Escape(e.OriginalValue ?? "")));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static void AppendInfoRow(StringBuilder sb, string label, string value) =>
        sb.AppendLine($"""
            <tr>
              <td style='font-weight:bold;padding:4px 10px;width:160px'>{Encode(label)}</td>
              <td style='padding:4px 10px'>{Encode(value)}</td>
            </tr>
            """);

    private static string Encode(string v) => System.Net.WebUtility.HtmlEncode(v);

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
