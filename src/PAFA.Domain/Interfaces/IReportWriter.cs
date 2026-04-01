using PAFA.Domain.Enums;

namespace PAFA.Domain.Interfaces;

/// <summary>
/// Strategy interface for report writers.
/// Each implementation handles one ExportFormat (CSV, Excel, PDF…).
/// Open/Closed Principle: add a new format = new class, zero existing code changed.
/// Défini dans Domain pour respecter la Clean Architecture :
/// Reports ne doit pas dépendre d'Extraction.
/// </summary>
public interface IReportWriter
{
    /// <summary>Le format que cette implémentation gère.</summary>
    ExportFormat Format { get; }

    /// <summary>
    /// Sérialise <paramref name="data"/> dans un <see cref="Stream"/> mémoire.
    /// Le caller est propriétaire du stream (dispose après usage).
    /// Générique : accepte n'importe quel DTO — PowerBiCsvRowDto, DashboardSummaryDto, etc.
    /// </summary>
    Task<Stream> WriteAsync<TDto>(IEnumerable<TDto> data, CancellationToken ct = default);
}
