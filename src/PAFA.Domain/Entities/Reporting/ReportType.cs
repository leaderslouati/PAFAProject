// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/ReportType.cs
//
// CORRECTIONS :
//   ✓ Hérite désormais de BaseEntity
//   ✓ Ajout de la propriété Audience (enum ReportAudience)
//     pour remplacer le bool IsAnonymised ambigu
// ═══════════════════════════════════════════════════════════
using PAFA.Domain.Enums;

namespace PAFA.Domain.Entities;

/// <summary>
/// PARR report type: Schedule 2A (Industry, anonymised)
/// or Schedule 2B (PAC, non-anonymised).
/// Reference table with 2 fixed records.
/// </summary>
public class ReportType : BaseEntity
{
    public int Id { get; set; }

    /// <summary>SCH2A or SCH2B.</summary>
    public string Code { get; set; } = string.Empty;

    public string ScheduleRef { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Target audience for this schedule.
    /// Industry = anonymised (2A). PAC = non-anonymised (2B).
    /// </summary>
    public ReportAudience Audience { get; set; } = ReportAudience.Industry;

    /// <summary>Number of reports in this type (19 for 2A, 22 for 2B).</summary>
    public int ReportCount { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    // ── Navigation ─────────────────────────────────────────────────
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}