using MediatR;

namespace PAFA.Extraction.Commands
{
    public record IngestFileCommand(string FileName) : IRequest<Guid>;
}