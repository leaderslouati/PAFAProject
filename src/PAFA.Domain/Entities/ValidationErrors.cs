// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/ValidationError.cs
//
// CORRECTIONS :
//   ✓ Renommée ValidationErrors → ValidationError (singulier)
//     Convention EF Core : nom de classe = nom de table au singulier
//   ✓ Id : long → Guid (cohérence avec tous les autres PK du projet)
//   ✓ Hérite de BaseEntity (audit fields + RowVersion)
//   ✓ Navigation IngestionFile déjà présente — OK
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Entities;

/// <summary>
/// Detailed validation finding for a single file and line.
/// Enables precise diagnostics and partial replay of failed imports.
/// </summary>
public class ValidationError : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → IngestionFile (the file that generated this error).</summary>
    public Guid IngestionFileId { get; set; }

    /// <summary>NULL = file-level error (structure, naming, encoding).</summary>
    public int? LineNumber { get; set; }

    public string? ColumnName { get; set; }

    /// <summary>
    /// Normalised error code matching VAL-xxx rules.
    /// E.g. VAL-001, VAL-009, MISSING_VALUE, INVALID_FORMAT, OUT_OF_RANGE.
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public string? OriginalValue { get; set; }

    /// <summary>ERROR | WARNING | INFO</summary>
    public string Severity { get; set; } = "ERROR";

    // ── Navigation ─────────────────────────────────────────────────
    public IngestionFile IngestionFile { get; set; } = null!;
}