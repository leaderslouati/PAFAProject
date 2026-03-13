using Microsoft.AspNetCore.Http;
namespace PAFA.Extraction.Commands.Import;

/// <summary>
/// File upload DTO 
/// Decouples domain logic from ASP.NET Core infrastructure.
/// </summary>
  public record FileUploadDto(IFormFile File, int PeriodYear, int PeriodMonth);
