using PAFA.Domain.Enums;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic; // Ajouté pour IEnumerable

namespace PAFA.Extraction.Reports.Interfaces;

/// <summary>
/// Strategy interface for report writers.
/// Each implementation handles one ExportFormat (CSV, Excel, PDF…).
/// Open/Closed Principle: add a new format = new class, zero existing code changed.
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

