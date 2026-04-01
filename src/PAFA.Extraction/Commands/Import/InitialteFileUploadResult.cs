namespace PAFA.Extraction.Commands.Import; 
public record InitiateFileUploadResult(
    bool Success,
    Guid JobId,
    Guid FileId,
    string? ErrorMessage,
    string? BlobPath = null
);

