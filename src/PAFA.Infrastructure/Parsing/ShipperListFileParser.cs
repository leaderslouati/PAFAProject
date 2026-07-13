using ExcelDataReader;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.Parsing;

/// <summary>
/// Parses the "Anonymised Shipper List" Excel workbook (password-protected) and
/// returns a flat list of <see cref="Shipper"/> records ready for database upsert.
///
/// Expected workbook columns (case-insensitive, first matching alias wins):
///   ShortCode   — "Shipper Short Code" | "SSC" | "Short Code" | "Code"
///   Name        — "Shipper Name"       | "Name" | "Full Name"
///   AliasCode   — "Alias"              | "Alias Code"          | "Anon Code"
///   LegalEntity — "Legal Entity"       | "Company"
///   IsActive    — "Active"             | "Status" (value "Active" / "Inactive")
///   Email       — "Email"              | "Contact Email"
///   PortfolioSize — "Portfolio Size"   | "Portfolio" | "Size"
/// </summary>
public sealed class ShipperListFileParser
{
    // ── Column header aliases ──────────────────────────────────────────────

    private static readonly string[] ShortCodeAliases   = ["Shipper Short Code", "SSC", "Short Code", "Code", "SRVC_PRVDR_CD"];
    private static readonly string[] NameAliases        = ["Shipper Name", "Name", "Full Name", "Company Name"];
    private static readonly string[] AliasCodeAliases   = ["Alias Code", "Alias", "Anon Code", "Anonymised Code"];
    private static readonly string[] LegalEntityAliases = ["Legal Entity", "Company", "Legal Name"];
    private static readonly string[] ActiveAliases      = ["Active", "Status", "Is Active"];
    private static readonly string[] EmailAliases       = ["Email", "Contact Email", "Email Address"];
    private static readonly string[] PortfolioAliases   = ["Portfolio Size", "Portfolio", "Size", "Portfolio Sz"];

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Opens <paramref name="stream"/> as a password-protected XLSX / XLS file using
    /// ExcelDataReader, reads the first worksheet, and maps each data row to a
    /// <see cref="Shipper"/>.  Rows with an empty ShortCode are silently skipped.
    /// </summary>
    /// <param name="stream">File stream (caller owns disposal).</param>
    /// <param name="password">Workbook password (e.g. "PAC_Cities").</param>
    /// <returns>Parsed shippers; may be empty if no valid rows are found.</returns>
    public IReadOnlyList<Shipper> Parse(Stream stream, string password)
    {
        // ExcelDataReader requires this encoding registration on .NET Core.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        var config = new ExcelReaderConfiguration { Password = password };

        using var reader = ExcelReaderFactory.CreateReader(stream, config);

        // Read first worksheet into a DataSet
        var dsConfig = new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        };

        var dataSet = reader.AsDataSet(dsConfig);
        if (dataSet.Tables.Count == 0)
            return [];

        var table = dataSet.Tables[0];

        // ── Map column indices ─────────────────────────────────────────
        var headers = table.Columns
            .Cast<System.Data.DataColumn>()
            .Select((c, i) => (c.ColumnName.Trim(), Index: i))
            .ToList();

        int? FindCol(string[] aliases) =>
            aliases
                .Select(a => headers
                    .FirstOrDefault(h => h.Item1.Equals(a, StringComparison.OrdinalIgnoreCase)).Index)
                .Cast<int?>()
                .FirstOrDefault(i => i.HasValue);

        var idxShortCode    = FindCol(ShortCodeAliases);
        var idxName         = FindCol(NameAliases);
        var idxAlias        = FindCol(AliasCodeAliases);
        var idxLegalEntity  = FindCol(LegalEntityAliases);
        var idxActive       = FindCol(ActiveAliases);
        var idxEmail        = FindCol(EmailAliases);
        var idxPortfolio    = FindCol(PortfolioAliases);

        // ── Parse rows ────────────────────────────────────────────────
        var result = new List<Shipper>();

        foreach (System.Data.DataRow row in table.Rows)
        {
            var shortCode = GetString(row, idxShortCode);
            if (string.IsNullOrWhiteSpace(shortCode))
                continue;

            var isActive = true;
            if (idxActive.HasValue)
            {
                var activeRaw = GetString(row, idxActive);
                isActive = !activeRaw.Equals("Inactive", StringComparison.OrdinalIgnoreCase)
                        && !activeRaw.Equals("No",       StringComparison.OrdinalIgnoreCase)
                        && !activeRaw.Equals("0",        StringComparison.OrdinalIgnoreCase)
                        && !activeRaw.Equals("false",    StringComparison.OrdinalIgnoreCase);
            }

            int? portfolioSize = null;
            if (idxPortfolio.HasValue)
            {
                var raw = GetString(row, idxPortfolio);
                if (int.TryParse(raw, System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out var ps))
                    portfolioSize = ps;
            }

            result.Add(new Shipper
            {
                Id          = Guid.NewGuid(),   // Will be overwritten on upsert if existing
                ShortCode   = shortCode,
                Name        = GetString(row, idxName),
                AliasCode   = GetString(row, idxAlias),
                LegalEntity = GetString(row, idxLegalEntity).NullIfEmpty(),
                Email       = GetString(row, idxEmail).NullIfEmpty(),
                IsActive    = isActive,
                PortfolioSize = portfolioSize,
            });
        }

        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string GetString(System.Data.DataRow row, int? colIndex)
    {
        if (colIndex is null || colIndex >= row.Table.Columns.Count)
            return string.Empty;

        var val = row[colIndex.Value];
        return val is DBNull || val is null ? string.Empty : val.ToString()!.Trim();
    }
}

internal static class StringExtensions
{
    internal static string? NullIfEmpty(this string s)
        => string.IsNullOrWhiteSpace(s) ? null : s;
}
