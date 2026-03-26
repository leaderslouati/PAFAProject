namespace PAFA.Extraction.Commands.Validation;  

public record JobValidationSummaryResponse(
Guid JobId,
int TotalFiles,
int TotalErrors,
List<FileValidationSummary> Files
);
