

namespace PAFA.Extraction.Commands.Import; 
public record ProcessFileResult(
    bool Success,
    Guid FileId,
    int RowsRead,
    int RowsValid,
    int RowsRejected,
    string? ErrorMessage,
    string? FinalBlobPath = null
);
