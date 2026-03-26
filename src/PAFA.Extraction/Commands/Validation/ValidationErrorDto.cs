namespace PAFA.Extraction.Commands.Validation;  

public record ValidationErrorDto(
 Guid Id,
 int? LineNumber,
 string? ColumnName,
 string ErrorCode,
 string ErrorMessage,
 string? OriginalValue,
 string Severity,
 DateTime CreatedAt
);
