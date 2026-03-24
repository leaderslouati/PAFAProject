using System.Globalization;
using PAFA.Domain.Constants;

namespace PAFA.Extraction.Validations;

// ?? Result types ??????????????????????????????????????????????????????????

/// <summary>A single finding produced by FileNameValidator.</summary>
public sealed record FileNameFinding(
    string RuleId,
    string Severity,   // "ERROR" | "WARNING"
    string Message);

/// <summary>Full result of a file name validation run.</summary>
public sealed record FileNameValidationResult(
    string FileName,
    bool IsValid,
    string? DetectedPrefix,
    string? DetectedMonthToken,
    IReadOnlyList<FileNameFinding> Findings);

// ?? Validator ?????????????????????????????????????????????????????????????

/// <summary>
/// Validates a PARR file name against the agreed naming convention:
///
///   {PREFIX}__{MonthToken}[YY[YY]][_vN].{ext}
///   e.g.  MOD520A__Feb25.xlsx
///         RPT_1364__07_v2.csv
///
/// Rules
///   NAME-001 — prohibited characters  ? ERROR  ? file skipped
///   NAME-002 — unknown prefix         ? ERROR  ? file skipped
///   NAME-003 — month token unreadable ? WARNING ? file processed but flagged
///   NAME-004 — disallowed extension   ? ERROR  ? file skipped
/// </summary>
public static class FileNameValidator
{
    public static FileNameValidationResult Validate(
        string fileName,
        IReadOnlyCollection<string> allowedPrefixes,
        IReadOnlyCollection<string> allowedExtensions)
    {
        var findings = new List<FileNameFinding>();

        // ?? NAME-001: prohibited characters ???????????????????????????????
        var prohibited = FileNamingConstants.ProhibitedChars
            .Where(c => fileName.Contains(c))
            .ToList();

        if (prohibited.Count != 0)
        {
            findings.Add(new FileNameFinding(
                "NAME-001", "ERROR",
                $"File name '{fileName}' contains prohibited character(s): " +
                $"[{string.Join(", ", prohibited)}]."));
        }

        // ?? NAME-004: extension ???????????????????????????????????????????
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            findings.Add(new FileNameFinding(
                "NAME-004", "ERROR",
                $"Extension '{ext}' is not allowed. Accepted: " +
                $"[{string.Join(", ", allowedExtensions)}]."));
        }

        // If prohibited chars or bad extension ? return early (cannot parse further)
        if (findings.Any(f => f.Severity == "ERROR"))
            return new FileNameValidationResult(fileName, false, null, null, findings);

        // ?? Parse name without extension ??????????????????????????????????
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // ?? NAME-002: prefix ?????????????????????????????????????????????
        // Convention: prefix is everything before the first "__" separator.
        var separatorIdx = nameWithoutExt.IndexOf("__", StringComparison.Ordinal);
        string? detectedPrefix = separatorIdx > 0
            ? nameWithoutExt[..separatorIdx]
            : nameWithoutExt;

        var prefixMatched = allowedPrefixes.Any(p =>
            detectedPrefix.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!prefixMatched)
        {
            findings.Add(new FileNameFinding(
                "NAME-002", "ERROR",
                $"Prefix '{detectedPrefix}' is not in the list of authorised prefixes: " +
                $"[{string.Join(", ", allowedPrefixes)}]."));
        }

        // ?? NAME-003: month token ?????????????????????????????????????????
        // Token is the text after "__" (stripping optional "_vN" version suffix).
        string? detectedMonthToken = null;
        if (separatorIdx > 0)
        {
            var afterPrefix = nameWithoutExt[(separatorIdx + 2)..]; // skip "__"

            // Strip optional version tag e.g. "_v2" at the end
            var versionIdx = afterPrefix.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
            var tokenRaw   = versionIdx > 0 ? afterPrefix[..versionIdx] : afterPrefix;

            // Try to extract a leading alphabetic or numeric month indicator
            // e.g. "Feb25" ? try "Feb", "02", "February"
            detectedMonthToken = ExtractMonthToken(tokenRaw);

            if (detectedMonthToken is null)
            {
                findings.Add(new FileNameFinding(
                    "NAME-003", "WARNING",
                    $"Could not identify a valid month token in '{tokenRaw}'. " +
                    $"Expected formats: MMM (e.g. Feb) or MM (e.g. 02)."));
            }
        }

        var isValid = !findings.Any(f => f.Severity == "ERROR");
        return new FileNameValidationResult(
            fileName, isValid, detectedPrefix, detectedMonthToken, findings);
    }

    // ?? Helpers ???????????????????????????????????????????????????????????

    /// <summary>
    /// Tries to extract a recognisable month token from the raw token string.
    /// Accepts:  "Feb25" ? "Feb" | "0225" ? "02" | "February25" ? "February"
    /// Returns null if no month is recognised.
    /// </summary>
    private static string? ExtractMonthToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 1. Try 3-letter month abbreviation at the start  (e.g. "Feb")
        if (raw.Length >= 3)
        {
            var threeChar = raw[..3];
            if (TryParseMonthAbbreviation(threeChar, out _))
                return threeChar;
        }

        // 2. Try 2-digit month at the start  (e.g. "02")
        if (raw.Length >= 2 && int.TryParse(raw[..2], out var m) && m is >= 1 and <= 12)
            return raw[..2];

        // 3. Try full month name at the start  (e.g. "February")
        foreach (var culture in new[] { CultureInfo.InvariantCulture })
        {
            foreach (var monthName in culture.DateTimeFormat.MonthNames
                         .Where(n => !string.IsNullOrEmpty(n)))
            {
                if (raw.StartsWith(monthName, StringComparison.OrdinalIgnoreCase))
                    return monthName;
            }
        }

        return null;
    }

    private static bool TryParseMonthAbbreviation(string token, out int month)
    {
        month = 0;
        return DateOnly.TryParseExact(
            token, "MMM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var d) && (month = d.Month) > 0;
    }
}
