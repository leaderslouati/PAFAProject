using MediatR;
using Microsoft.AspNetCore.Http;

namespace PAFA.Extraction.Commands.Import;

// ════════════════════════════════════════════════════════════════════════
//  COMMAND 1 : Upload (Point d'entrée API)
// ════════════════════════════════════════════════════════════════════════

public record UploadParrFilesCommand(
    IFormFile File, // Simplifié pour le POC : un seul fichier à la fois
    int PeriodYear,
    int PeriodMonth,
    string UploadedBy,
    string SourceSystem = "MANUAL"
) : IRequest<UploadParrFilesResult>;

public record UploadParrFilesResult(
    bool Success,
    Guid JobId,
    Guid FileId,
    string FileName,
    int RowsRead,      // Ajouté pour le debug
    int RowsValid,     // Ajouté pour le debug
    int RowsRejected,  // Ajouté pour le debug
    string? ErrorMessage
);

// ════════════════════════════════════════════════════════════════════════
//  COMMAND 2 : Parse + Validate + Persist (Point d'entrée Worker/RabbitMQ)
// ════════════════════════════════════════════════════════════════════════

/// <summary>
/// Déclenché de manière asynchrone (via RabbitMQ) pour lire le fichier,
/// faire une validation minimale, et persister directement dans MetricValues.
/// </summary>
public record ParseAndValidateFileCommand(
    Guid JobId,
    Guid FileId,
    string FileName,
    string BlobPath, // Chemin local ou Azure Blob où le fichier a été sauvegardé
    int PeriodYear,
    int PeriodMonth
) : IRequest<ParseAndValidateFileResult>;

public record ParseAndValidateFileResult(
    Guid FileId,
    bool Success,
    string Status, // COMPLETED | FAILED
    int RowsRead,
    int RowsValid,
    int RowsRejected,
    string? ErrorMessage
);