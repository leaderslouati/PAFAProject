using PAFA.Domain.Constants;

namespace PAFA.Extraction.Validations;

/// <summary>
/// Validates that a SharePoint remote path follows the strict
/// {BaseInboundPath}/{YYYY}/{MM} folder hierarchy.
///
/// Rule FOLD-001 — file found outside the expected Year/Month path.
/// Rule FOLD-002 — the constructed inbound path itself is structurally invalid.
/// </summary>
public static class FolderPathValidator
{
    /// <summary>
    /// Returns true when <paramref name="remotePath"/> ends with
    /// /{expectedYear}/{expectedMonth:D2} (ignoring trailing slashes).
    ///
    /// Example: "/PARR/2025/07" with year=2025, month=7 ? true.
    /// Example: "/PARR/2025"    with year=2025, month=7 ? false.
    /// </summary>
    public static bool IsValidYearMonthPath(string remotePath, int expectedYear, int expectedMonth)
    {
        if (string.IsNullOrWhiteSpace(remotePath)) return false;

        var segments = remotePath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2) return false;

        var yearSegment  = segments[^2]; // second-to-last
        var monthSegment = segments[^1]; // last

        return int.TryParse(yearSegment,  out var year)  && year  == expectedYear
            && int.TryParse(monthSegment, out var month) && month == expectedMonth;
    }

    /// <summary>
    /// Returns true when the last two segments of <paramref name="remotePath"/> are
    /// a structurally valid year (MinYear–MaxYear) and a valid month (1–12),
    /// regardless of what specific values are expected.
    ///
    /// Used by SharePointFileSource to log FOLD-002 before listing starts.
    /// </summary>
    public static bool HasValidYearMonthStructure(string remotePath)
    {
        if (string.IsNullOrWhiteSpace(remotePath)) return false;

        var segments = remotePath.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2) return false;

        return int.TryParse(segments[^2], out var year)  
               && year >= FileNamingConstants.MinYear 
               && year <= FileNamingConstants.MaxYear
            && int.TryParse(segments[^1], out var month) 
               && month is >= 1 and <= 12;
    }
}
