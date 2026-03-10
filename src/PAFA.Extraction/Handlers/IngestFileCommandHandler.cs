using MassTransit;
using MediatR;
using PAFA.Domain.Entities;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands;
using PAFA.Messaging.Events;

namespace PAFA.Extraction.CQRS.Commands
{
    public class IngestFileCommandHandler : IRequestHandler<IngestFileCommand, Guid>
    {
        private readonly IIngestedFileRepository _repository;
        private readonly IPublishEndpoint _publishEndpoint;

        public IngestFileCommandHandler(IIngestedFileRepository repository, IPublishEndpoint publishEndpoint)
        {
            _repository = repository;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<Guid> Handle(IngestFileCommand request, CancellationToken cancellationToken)
        {
            var file = IngestedFile.Create(request.FileName);
            await _repository.AddAsync(file, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            await _publishEndpoint.Publish(new FileIngestedEvent
            {
                FileId = file.Id,
                FileName = file.FileName,
                IngestedAt = file.CreatedAt
            }, cancellationToken);

            return file.Id;
        }
    }
}