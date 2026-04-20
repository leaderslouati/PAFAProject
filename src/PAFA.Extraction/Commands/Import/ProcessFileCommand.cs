using MediatR;

namespace PAFA.Extraction.Commands.Import;

public record ProcessFileCommand(Guid FileId) : IRequest<ProcessFileResult>;

