using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Extraction.Commands.Import;

namespace PAFA.Tests.Pipeline.Mocks;

/// <summary>
/// Mock data centralisé pour tous les tests du pipeline.
/// Représente un scénario réaliste : 3 fichiers PARR pour la période 2025-07.
///   - MOD520A_2025_07.xlsx  ? valide  (parse OK, validation OK)
///   - RPT_1364_2025_07.xlsx ? valide avec avertissements
///   - MOD700_2025_07.xlsx   ? invalide (erreurs bloquantes)
/// </summary>
public static class MockData
{
    public static readonly Guid JobId    = Guid.Parse("11111111-0000-0000-0000-000000000001");
    public static readonly Guid FileId1  = Guid.Parse("22222222-0000-0000-0000-000000000001");
    public static readonly Guid FileId2  = Guid.Parse("22222222-0000-0000-0000-000000000002");
    public static readonly Guid FileId3  = Guid.Parse("22222222-0000-0000-0000-000000000003");

    public const int Year  = 2025;
    public const int Month = 7;

    // ?? IngestionJob ??????????????????????????????????????????????????????????

    public static IngestionJob CreateJob() => new()
    {
        Id              = JobId,
        JobName         = $"MANUAL_{Year}_{Month:D2}",
        ReportingPeriod = new DateOnly(Year, Month, 1),
        Status          = IngestionJobStatus.Processing,
        FilesExpected   = 3,
        StartedAt       = DateTime.UtcNow,
        TriggeredBy     = JobTrigger.Manual
    };

    // ?? IngestionFiles ????????????????????????????????????????????????????????

    public static IngestionFile CreateFile1_Valid() => new()
    {
        Id               = FileId1,
        IngestionJobId   = JobId,
        FileName         = "MOD520A_2025_07.xlsx",
        BlobPath         = "landing-zone/2025/07/MOD520A_2025_07.xlsx",
        Status           = IngestionFileStatus.Downloaded,
        ValidationStatus = ValidationStatus.Pending,
        FileSizeBytes    = 512_000,
        DownloadedAt     = DateTime.UtcNow
    };

    public static IngestionFile CreateFile2_WithWarnings() => new()
    {
        Id               = FileId2,
        IngestionJobId   = JobId,
        FileName         = "RPT_1364_2025_07.xlsx",
        BlobPath         = "landing-zone/2025/07/RPT_1364_2025_07.xlsx",
        Status           = IngestionFileStatus.Downloaded,
        ValidationStatus = ValidationStatus.Pending,
        FileSizeBytes    = 320_000,
        DownloadedAt     = DateTime.UtcNow
    };

    public static IngestionFile CreateFile3_BlockingErrors() => new()
    {
        Id               = FileId3,
        IngestionJobId   = JobId,
        FileName         = "MOD700_2025_07.xlsx",
        BlobPath         = "landing-zone/2025/07/MOD700_2025_07.xlsx",
        Status           = IngestionFileStatus.Downloaded,
        ValidationStatus = ValidationStatus.Pending,
        FileSizeBytes    = 128_000,
        DownloadedAt     = DateTime.UtcNow
    };

    // ?? ParseFileResult mock results ??????????????????????????????????????????

    public static ParseFileResult ParseSuccess(Guid fileId, int rows = 250) =>
        new(Success: true, FileId: fileId, TotalRows: rows, ErrorMessage: null);

    public static ParseFileResult ParseFailure(Guid fileId, string error = "Format de fichier non reconnu") =>
        new(Success: false, FileId: fileId, TotalRows: 0, ErrorMessage: error);

    // ?? ValidateFileResult mock results ???????????????????????????????????????

    public static ValidateFileResult ValidateSuccess(Guid fileId, int validRows = 240, int rejectedRows = 0) =>
        new(Success: true, FileId: fileId, HasBlockingErrors: false,
            ValidRowCount: validRows, InvalidRowCount: rejectedRows, ErrorMessage: null);

    public static ValidateFileResult ValidateWithWarnings(Guid fileId) =>
        new(Success: true, FileId: fileId, HasBlockingErrors: false,
            ValidRowCount: 190, InvalidRowCount: 5, ErrorMessage: null);

    public static ValidateFileResult ValidateBlocking(Guid fileId) =>
        new(Success: true, FileId: fileId, HasBlockingErrors: true,
            ValidRowCount: 0, InvalidRowCount: 80, ErrorMessage: "Erreurs bloquantes détectées (VAL-005, VAL-008)");

    // ?? PersistFileResult mock results ????????????????????????????????????????

    public static PersistFileResult PersistSuccess(Guid fileId, int metrics = 720) =>
        new(Success: true, FileId: fileId, MetricsInserted: metrics,
            FinalBlobPath: $"processed/2025/07/file_{fileId}.xlsx", ErrorMessage: null);

    public static PersistFileResult PersistFailure(Guid fileId, string error = "Erreur base de données") =>
        new(Success: false, FileId: fileId, MetricsInserted: 0, FinalBlobPath: null, ErrorMessage: error);
}
