namespace PAFA.Domain.Entities;

/// <summary>
/// PARR report type: Schedule 2A (Industry, anonymized) or Schedule 2B (PAC, non-anonymized).
/// Reference table with 2 fixed records.
/// </summary>
public class ReportType
{
    public int  Id           { get; set; }

    /// <summary>SCH2A or SCH2B.</summary>
    public string Code       { get; set; } = string.Empty;

    public string ScheduleRef { get; set; } = string.Empty;

    public string Label      { get; set; } = string.Empty;

    /// <summary>True = Industry (anonymized), False = PAC (non-anonymized).</summary>
    public bool   IsAnonymised { get; set; } = true;


    /// <summary>Number of reports in this type (19 for 2A, 22 for 2B).</summary>
    public int    ReportCount  { get; set; } = 0;

    public bool   IsActive     { get; set; } = true;

    // ── Navigation ──────────────────────────────────────────────────────
    public ICollection<Report>           Reports           { get; set; } = new List<Report>();
}