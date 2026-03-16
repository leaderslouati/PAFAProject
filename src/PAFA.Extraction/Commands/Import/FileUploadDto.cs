using Microsoft.AspNetCore.Http;
namespace PAFA.Extraction.Commands.Import;

public record FileUploadDto(
    string FileName,
    byte[] FileContent,
    int PeriodYear,
    int PeriodMonth
);