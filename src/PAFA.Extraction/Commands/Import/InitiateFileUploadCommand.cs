using MediatR;


namespace PAFA.Extraction.Commands.Import; 
public record InitiateFileUploadCommand(
    string FileName,
    Stream FileStream,
    int PeriodYear,
    int PeriodMonth,
    string ContentType
) : IRequest<InitiateFileUploadResult>;