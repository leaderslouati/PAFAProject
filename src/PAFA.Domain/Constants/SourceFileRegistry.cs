using System.Text.RegularExpressions;

namespace PAFA.Domain.Constants;

// ── Enums ──────────────────────────────────────────────────────────────────────

/// <summary>Which upstream system produced this file.</summary>
public enum FileSourceSystem { CDSP, DDP }

/// <summary>Whether the filename itself carries a monthly date token.</summary>
public enum FileNamingType
{
    /// <summary>
    /// The filename contains a date that changes every month
    /// (e.g. EUC09_Reporting_PAC_2026_04.xlsx → _2026_04 changes).
    /// Rule 1 compares only the structural base name (date stripped).
    /// </summary>
    DateBased,

    /// <summary>
    /// The filename stays constant month-to-month; only the data inside changes
    /// (e.g. Read Performance by Shipper (5).xlsx).
    /// Rule 1 compares the base name with the version counter stripped.
    /// </summary>
    Static
}

// ── Descriptor ─────────────────────────────────────────────────────────────────

/// <summary>
/// Metadata for a known PARR source file type.
/// Used by ParseAndValidateFilesHandler for Rules 1-4.
/// </summary>
public sealed record SourceFileDescriptor(
    /// <summary>Unique logical key for this file type (used for Rule-1 same-type matching).</summary>
    string FileKey,

    /// <summary>
    /// One or more leading substrings; a file matches this descriptor when its
    /// name-without-extension starts with ANY of these (case-insensitive).
    /// </summary>
    string[] NameMatchPrefixes,

    FileSourceSystem Source,
    FileNamingType   NamingType,

    /// <summary>
    /// Regex applied to the name-without-extension to REMOVE the date token for
    /// Rule-1 base-name comparison. Only used when NamingType = DateBased.
    /// </summary>
    string? DateStripRegex,

    /// <summary>Rule 3 — columns that MUST be present (case-insensitive).</summary>
    string[] RequiredColumns,

    /// <summary>
    /// Rule 4 — one or more column names that carry the shipper identifier.
    /// The handler searches for the first column name that exists in the workbook.
    /// </summary>
    string[] ShipperColumnAliases,

    /// <summary>
    /// Rule 2 — when true, generic sheet names (Sheet1 etc.) are accepted
    /// for this file type and will NOT raise a validation error.
    /// </summary>
    bool AllowGenericSheetNames = false
);

// ── Registry ───────────────────────────────────────────────────────────────────

/// <summary>
/// Central catalog of all known PARR source files.
///
/// Naming conventions observed in source files:
///   CDSP (date-based)   — EUC09_Reporting_PAC_YYYY_MM.xlsx
///                       — MOD520A__PAF_Reports_AprYY[_…].xlsx
///                       — Rpt_1364_PARR AQ report_YYYY-MM.xlsx
///   DDP (static name)   — Read Performance by Shipper (N).xlsx
///                       — Report N – ….xlsx   etc.
///   DDP (date-in-name)  — 202604_Apr2026_SupplyPointCounts.xlsx
///                       — 2B.21 Corrective … Rejections_Apr-26.xlsx
///                       — AQ at Risk MMM YYYY_For PAFA.xlsx
/// </summary>
public static class SourceFileRegistry
{
    // ── Known file descriptors ──────────────────────────────────────────────

    public static readonly IReadOnlyList<SourceFileDescriptor> KnownFiles =
    [
        // ── CDSP ─────────────────────────────────────────────────────────

        new(
            FileKey:             "EUC09",
            NameMatchPrefixes:   ["EUC09"],
            Source:              FileSourceSystem.CDSP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"_\d{4}_\d{2}$",
            RequiredColumns:     ["Shipper Short Code"],
            ShipperColumnAliases:["Shipper Short Code", "Shipper"]),

        new(
            FileKey:             "MOD520A",
            NameMatchPrefixes:   ["MOD520A"],
            Source:              FileSourceSystem.CDSP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"(?:__|_)[A-Za-z]{3}\d{2}(?:_.*)?$",
            RequiredColumns:     ["Shipper Short Code"],
            ShipperColumnAliases:["Shipper Short Code", "Shipper Sort Code"]),

        new(
            FileKey:             "RPT_1364",
            NameMatchPrefixes:   ["RPT_1364", "Rpt_1364"],
            Source:              FileSourceSystem.CDSP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"_\d{4}-\d{2}$",
            RequiredColumns:     ["Shipper Short Code"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "MOD700",
            NameMatchPrefixes:   ["MOD700"],
            Source:              FileSourceSystem.CDSP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"(?:__|_)[A-Za-z0-9_-]+$",
            RequiredColumns:     ["Shipper", "Period"],
            ShipperColumnAliases:["Shipper", "Shipper Short Code"]),

        new(
            FileKey:             "TRANSFER",
            NameMatchPrefixes:   ["TRANSFER"],
            Source:              FileSourceSystem.CDSP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"(?:__|_)[A-Za-z0-9_-]+$",
            RequiredColumns:     ["Shipper", "Period"],
            ShipperColumnAliases:["Shipper", "Shipper Short Code"]),

        new(
            FileKey:             "CLASS4AQ",
            NameMatchPrefixes:   ["CLASS4AQ"],
            Source:              FileSourceSystem.CDSP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"(?:__|_)[A-Za-z0-9_-]+$",
            RequiredColumns:     ["Shipper", "Period"],
            ShipperColumnAliases:["Shipper", "Shipper Short Code"]),

        // ── DDP — static name ─────────────────────────────────────────────

        new(
            FileKey:             "ReadPerformance",
            NameMatchPrefixes:   ["Read Performance by Shipper"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper Short Code", "Topic"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "TransferReadPerf",
            NameMatchPrefixes:   ["Shipper Transfer Read Performance"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper Short Code"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "SupplyPtsNotMet",
            NameMatchPrefixes:   ["Supply Points and AQ with Minimum Percentage Not met"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Year/Month", "SRVC_PRVDR_CD"],
            ShipperColumnAliases:["SRVC_PRVDR_CD"]),

        new(
            FileKey:             "SupplyPtsRequirement",
            NameMatchPrefixes:   ["Supply Points with Minimum Percentage Requirement"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Year/Month", "SRVC_PRVDR_CD"],
            ShipperColumnAliases:["SRVC_PRVDR_CD"]),

        new(
            FileKey:             "Report1_MPRN",
            NameMatchPrefixes:   ["Report 1 - Percentage MPRN removed from Must Read"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper Short Code", "MURD Reference Month Year"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "Report1A_Vacant",
            NameMatchPrefixes:   ["Report 1A"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper Short Code", "Year/Month"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "Report1B_VacantCount",
            NameMatchPrefixes:   ["Report 1B"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper Short Code", "Year/Month"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "Report2_AgeBucket",
            NameMatchPrefixes:   ["Report 2 - Percentage MPRN removed from Must Read age bucket"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper Short Code", "Year/Month"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "Report2_Vacant",
            NameMatchPrefixes:   ["Report 2 - Proportion of sites set as Vacant"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper Short Code", "Year/Month"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "Report3_Count",
            NameMatchPrefixes:   ["Report 3 - Count MPRN"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper Short Code", "MURD Reference Month Year"],
            ShipperColumnAliases:["Shipper Short Code"]),

        new(
            FileKey:             "Class3Conv_AQ",
            NameMatchPrefixes:   ["Class 3 conversion"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Year/Month", "SRVC_PRVDR_CD"],
            ShipperColumnAliases:["SRVC_PRVDR_CD"]),

        new(
            FileKey:             "EnergyTheftClaim_P106",
            NameMatchPrefixes:   ["Confirmed Energy Theft Claim objections_P106"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper"],
            ShipperColumnAliases:["Shipper"],
            AllowGenericSheetNames: true),

        new(
            FileKey:             "EnergyTheftClaim_P41",
            NameMatchPrefixes:   ["Confirmed Energy Theft Claim objections_P41"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     ["Shipper"],
            ShipperColumnAliases:["Shipper"],
            AllowGenericSheetNames: true),

        new(
            FileKey:             "EnergyTheftWithdrawal_P106",
            NameMatchPrefixes:   ["Confirmed Energy Theft Withdrawal objections_P106"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     [],
            ShipperColumnAliases:[],
            AllowGenericSheetNames: true),

        new(
            FileKey:             "EnergyTheftWithdrawal_P41",
            NameMatchPrefixes:   ["Confirmed Energy Theft Withdrawal objections_P41"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.Static,
            DateStripRegex:      null,
            RequiredColumns:     [],
            ShipperColumnAliases:[],
            AllowGenericSheetNames: true),

        // ── DDP — date in name ────────────────────────────────────────────

        new(
            FileKey:             "SupplyPointCounts",
            NameMatchPrefixes:   ["202"],           // 202YMM_ prefix pattern (2020s decade)
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"^\d{6}_[A-Za-z]+\d{4}_",
            RequiredColumns:     ["Gas Day", "SHP Service Provider"],
            ShipperColumnAliases:["SHP Service Provider"]),

        new(
            FileKey:             "CorrectiveMeterRejections",
            NameMatchPrefixes:   ["2B.21 Corrective Opening Meter Reading Rejections"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"_[A-Za-z]+-\d{2}$",
            RequiredColumns:     ["Shipper"],
            ShipperColumnAliases:["Shipper"],
            AllowGenericSheetNames: true),

        new(
            FileKey:             "AQAtRisk",
            NameMatchPrefixes:   ["AQ at Risk"],
            Source:              FileSourceSystem.DDP,
            NamingType:          FileNamingType.DateBased,
            DateStripRegex:      @"\s+[A-Za-z]+ \d{4}_For PAFA$",
            RequiredColumns:     ["GWh"],
            ShipperColumnAliases:[]),
    ];

    // ── Lookup helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first descriptor whose NameMatchPrefixes matches the given
    /// file name (case-insensitive prefix check).
    /// Returns null when the file type is unknown.
    /// </summary>
    public static SourceFileDescriptor? Match(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        return KnownFiles.FirstOrDefault(d =>
            d.NameMatchPrefixes.Any(p =>
                nameWithoutExt.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Returns the "structural base name" used for Rule-1 comparison:
    /// <list type="bullet">
    ///   <item>DateBased → applies <see cref="SourceFileDescriptor.DateStripRegex"/> to remove the date token.</item>
    ///   <item>Static    → removes a trailing SharePoint version counter like " (5)".</item>
    /// </list>
    /// </summary>
    public static string GetBaseNameForComparison(string fileName, SourceFileDescriptor descriptor)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        if (descriptor.NamingType == FileNamingType.DateBased
            && descriptor.DateStripRegex is not null)
        {
            return Regex
                .Replace(nameWithoutExt, descriptor.DateStripRegex,
                         string.Empty, RegexOptions.IgnoreCase)
                .Trim('_', '-', ' ');
        }

        // Static: strip trailing SharePoint auto-increment " (N)"
        return Regex.Replace(nameWithoutExt, @"\s*\(\d+\)\s*$", string.Empty).Trim();
    }

    /// <summary>
    /// Convenience: all DDP file name match prefixes for use in AllowedFilePrefixes config.
    /// </summary>
    public static IReadOnlyList<string> AllDdpPrefixes =>
        KnownFiles
            .Where(d => d.Source == FileSourceSystem.DDP)
            .SelectMany(d => d.NameMatchPrefixes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
