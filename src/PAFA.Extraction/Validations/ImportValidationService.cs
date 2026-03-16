using PAFA.Infrastructure.Parsing;

namespace PAFA.Extraction.Validation;

public enum ValidationSeverity { Error, Warning, Info }

public sealed record ValidationFinding(
    string RuleId,
    string FieldName,
    string? FieldValue,
    ValidationSeverity Severity,
    string ErrorMessage,
    int RowNumber,
    string SheetName = "");

public sealed record FileValidationResult(
    bool HasBlockingErrors,
    List<ValidationFinding> Findings,
    int ValidRowCount,
    int InvalidRowCount);

public sealed class ImportValidationService
{
    private readonly HashSet<string> _knownCodes;

    public ImportValidationService(IEnumerable<string>? knownCodes = null)
        => _knownCodes = knownCodes is not null
            ? new HashSet<string>(knownCodes, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // ── Point d'entrée ────────────────────────────────────────

    public FileValidationResult Validate(
        FileParseResult parseResult,
        string fileName,
        bool isAnonymised)
    {
        var findings = new List<ValidationFinding>();

        // VAL-002 — fichier non vide (bloquant)
        if (parseResult.TotalRows == 0)
        {
            findings.Add(new ValidationFinding(
                "VAL-002", "FileContent", null, ValidationSeverity.Error,
                "Le fichier est vide — aucune ligne de données détectée.", 0));

            return new FileValidationResult(true, findings, 0, 0);
        }

        // Règles ligne par ligne
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int valid = 0;
        int invalid = 0;

        foreach (var row in parseResult.Rows)
        {
            var rowFindings = new List<ValidationFinding>();

            ValidatePeriod(row, rowFindings);       // VAL-003 + VAL-004
            ValidateShipperCode(row, rowFindings);  // VAL-005
            ValidatePc1Threshold(row, rowFindings); // VAL-011
            ValidateDuplicate(row, seen, rowFindings); // VAL-013

            findings.AddRange(rowFindings);

            if (rowFindings.Any(f => f.Severity == ValidationSeverity.Error))
                invalid++;
            else
                valid++;
        }

        return new FileValidationResult(
            HasBlockingErrors: findings.Any(f => f.Severity == ValidationSeverity.Error),
            Findings: findings,
            ValidRowCount: valid,
            InvalidRowCount: invalid);
    }

    // ── Règles ────────────────────────────────────────────────

    /// <summary>
    /// VAL-003 : ReportingPeriod présent.
    /// VAL-004 : format valide (MMM-YY ou YYYY-MM).
    /// </summary>
    private static void ValidatePeriod(RawDataRow row, List<ValidationFinding> findings)
    {
        var value = GetCell(row, "reportingperiod", "period", "month");

        if (string.IsNullOrWhiteSpace(value))
        {
            findings.Add(RowErr(row, "VAL-003", "ReportingPeriod", null,
                "ReportingPeriod manquant — champ obligatoire."));
            return;
        }

        if (!TryParseDate(value, out _))
            findings.Add(RowErr(row, "VAL-004", "ReportingPeriod", value,
                $"Format invalide : '{value}'. Formats acceptés : MMM-YY (ex: Feb-25) ou YYYY-MM."));
    }

    /// <summary>
    /// VAL-005 : ShipperShortCode présent.
    /// </summary>
    private static void ValidateShipperCode(RawDataRow row, List<ValidationFinding> findings)
    {
        var value = GetCell(row, "shippershortcode", "ssc", "code");

        if (string.IsNullOrWhiteSpace(value))
            findings.Add(RowErr(row, "VAL-005", "ShipperShortCode", null,
                "ShipperShortCode manquant — champ obligatoire."));
    }

    /// <summary>
    /// VAL-011 : PC1 ReadPerformancePct &lt; 97.5% → shipper non-conforme.
    /// Severity : Info (non bloquant — ligne importée mais flaggée).
    /// Seule règle métier explicite du cahier des charges PAFA.
    /// </summary>
    private static void ValidatePc1Threshold(RawDataRow row, List<ValidationFinding> findings)
    {
        var pc = GetCell(row, "productclass", "pc", "class");
        if (pc != "1") return;

        var raw = GetCell(row, "readperformancepct", "readperformance", "readperf");
        if (raw is null) return;

        if (!TryParseDecimal(raw, out var pct)) return;

        if (pct < 97.5m)
            findings.Add(new ValidationFinding(
                "VAL-011", "ReadPerformancePct", raw, ValidationSeverity.Info,
                $"PC1 Read Performance = {pct:F2}% sous le seuil UNC de 97.5%. Shipper non-conforme.",
                row.RowNumber, row.SheetName));
    }

    /// <summary>
    /// VAL-013 : pas de doublon (SSC + Période) dans le même fichier.
    /// Severity : Error — la deuxième occurrence est rejetée.
    /// </summary>
    private static void ValidateDuplicate(
        RawDataRow row, HashSet<string> seen, List<ValidationFinding> findings)
    {
        var ssc = GetCell(row, "shippershortcode", "ssc", "code") ?? "?";
        var period = GetCell(row, "reportingperiod") ?? "?";
        var key = $"{ssc}|{period}".ToUpperInvariant();

        if (!seen.Add(key))
            findings.Add(RowErr(row, "VAL-013", "ShipperShortCode+Period", key,
                $"Doublon détecté ({key}). Seule la première occurrence est importée."));
    }

    // ── Helpers ───────────────────────────────────────────────

    private static string? GetCell(RawDataRow row, params string[] aliases)
    {
        foreach (var alias in aliases)
            if (row.Cells.TryGetValue(alias, out var v) && !string.IsNullOrWhiteSpace(v))
                return v.Trim();
        return null;
    }

    private static bool TryParseDate(string raw, out DateOnly date)
    {
        date = default;
        foreach (var fmt in new[] { "yyyy-MM-dd", "yyyy-MM", "MMM-yy", "MMM yy" })
            if (DateOnly.TryParseExact(raw.Trim(), fmt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out date))
                return true;
        return false;
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        value = 0;
        raw = raw.Replace("%", "").Trim();

        if (!decimal.TryParse(raw,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out value)) return false;

        // Fraction Excel (0.975) → pourcentage (97.5)
        if (value is > 0 and <= 1.0m)
            value *= 100m;

        return true;
    }

    private static ValidationFinding RowErr(
        RawDataRow r, string rule, string field, string? value, string msg)
        => new(rule, field, value, ValidationSeverity.Error, msg, r.RowNumber, r.SheetName);
}