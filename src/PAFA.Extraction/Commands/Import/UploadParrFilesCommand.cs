using MediatR;

namespace PAFA.Extraction.Commands.Import;


public record UploadParrFilesCommand(
    string FileName,
    byte[] FileContent,

    int PeriodYear,
    int PeriodMonth,
    string UploadedBy,
    string SourceSystem = "MANUAL",
    string? BlobPath = null
) : IRequest<UploadParrFilesResult>;

public record UploadParrFilesResult(
    bool Success,
    Guid JobId,
    Guid FileId,
    string FileName,
    int RowsRead,      
    int RowsValid,    
    int RowsRejected,  
    string? ErrorMessage
);


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