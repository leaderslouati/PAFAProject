

namespace PAFA.Extraction.Commands.Validation;  

public record ValidationErrorsResponse(
 Guid FileId,
 string FileName,
 string ValidationStatus,
 int TotalRows,
 int ValidRows,
 int RejectedRows,
 int ErrorCount,
 List<ValidationErrorDto> Errors
);
