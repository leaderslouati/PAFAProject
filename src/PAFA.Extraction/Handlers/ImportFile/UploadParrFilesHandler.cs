using System;
using MediatR;
using PAFA.Domain.Entities;
using PAFA.Domain.Entities.ETL;
using PAFA.Domain.Enums;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;

namespace PAFA.Extraction.Handlers.ImportFile
{
    public class UploadParrFilesHandler : IRequestHandler<UploadParrFilesCommand, UploadParrFilesResult>
    {
        private readonly IMediator _mediator;
        private readonly IUnitOfWork _unitOfWork;

        public UploadParrFilesHandler(IMediator mediator, IUnitOfWork unitOfWork)
        {
            _mediator = mediator;
            _unitOfWork = unitOfWork;
        }

        public async Task<UploadParrFilesResult> Handle(UploadParrFilesCommand request, CancellationToken cancellationToken)
        {
            // 1. Définir le chemin de sauvegarde local (dossier temporaire du serveur)
            var fileName = request.File.FileName;
            var filePath = Path.Combine(Path.GetTempPath(), fileName);

            // 2. Sauvegarder le fichier physiquement
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream, cancellationToken);
            }

            // 3. Générer les IDs pour la traçabilité
            var jobId = Guid.NewGuid();
            var fileId = Guid.NewGuid();

            // Créer l'enregistrement IngestionJob
            var job = new IngestionJob
            {
                Id = jobId,
                JobName = $"PARR_{request.PeriodYear}_{request.PeriodMonth:D2}",
                PeriodYear = request.PeriodYear,
                PeriodMonth = request.PeriodMonth,
                Status = IngestionJobStatus.Started,
                StartedAt = DateTime.UtcNow,
                TriggeredBy = JobTrigger.Manual,
                CreatedBy = request.UploadedBy
            };
            await _unitOfWork.IngestionJobs.AddAsync(job);

            // Créer l'enregistrement IngestionFile
            var file = new IngestionFile
            {
                Id = fileId,
                IngestionJobId = jobId,
                FileName = fileName,
                FileType = FileType.Xlsx,
                Status = IngestionFileStatus.Downloaded,
                BlobPath = filePath,
                CreatedBy = request.UploadedBy
            };
            await _unitOfWork.IngestionFiles.AddAsync(file);
            await _unitOfWork.SaveChangesAsync();

            // 4. DÉCLENCHEMENT SYNCHRONE DE LA PHASE 2 (Le Parsing)
            var parseCommand = new ParseAndValidateFileCommand(
                jobId,
                fileId,
                fileName,
                filePath,
                request.PeriodYear,
                request.PeriodMonth
            );

            // On attend que le fichier soit lu et inséré en base par ParseAndValidateFileHandler
            var parseResult = await _mediator.Send(parseCommand, cancellationToken);

            // 5. Retour du résultat à l'API avec les statistiques de lecture !
            if (!parseResult.Success)
            {
                return new UploadParrFilesResult(
                    false,
                    jobId,
                    fileId,
                    fileName,
                    parseResult.RowsRead,
                    parseResult.RowsValid,
                    parseResult.RowsRejected,
                    parseResult.ErrorMessage);
            }

            file.Status = parseResult.Success 
                ? IngestionFileStatus.Loaded 
                : IngestionFileStatus.Failed;
            file.RowsRead = parseResult.RowsRead;
            file.RowsValid = parseResult.RowsValid;
            file.RowsRejected = parseResult.RowsRejected;

            job.Status = parseResult.Success 
                ? IngestionJobStatus.Completed 
                : IngestionJobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();

            return new UploadParrFilesResult(
                true,
                jobId,
                fileId,
                fileName,
                parseResult.RowsRead,
                parseResult.RowsValid,
                parseResult.RowsRejected,
                null);
        }
    }
}