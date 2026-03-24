
namespace PAFA.Extraction.Commands.Validation;  

public record FileValidationSummary(
  Guid FileId,
  string FileName,
  string ValidationStatus,
  int TotalErrors,
  int ValidRows,
  int RejectedRows
);
