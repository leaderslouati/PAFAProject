using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Validations;


namespace PAFA.Extraction.Handlers.ImportFile; 
public class InitiateFileUploadHandler : IRequestHandler<InitiateFileUploadCommand, InitiateFileUploadResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IBlobStorageService _blobService; // Interface à créer pour Azure
    private readonly ILogger<InitiateFileUploadHandler> _log;

    // Reprise des réglages par défaut de votre handler initial
    private static readonly string[] AllowedPrefixes = ["MOD520A", "RPT_1364", "MOD700", "EUC09", "TRANSFER", "CLASS4AQ"];
    private static readonly string[] AllowedExtensions = [".xlsx", ".xls"];

    public InitiateFileUploadHandler(IUnitOfWork uow, IBlobStorageService blobService, ILogger<InitiateFileUploadHandler> log)
    {
        _uow = uow;
        _blobService = blobService;
        _log = log;
    }

    public async Task<InitiateFileUploadResult> Handle(InitiateFileUploadCommand cmd, CancellationToken ct)
    {
        // 1. Validation du nom (NAME-001..004)
        var nameValidation = FileNameValidator.Validate(cmd.FileName, AllowedPrefixes, AllowedExtensions);
        if (!nameValidation.IsValid)
            return new InitiateFileUploadResult(false, Guid.Empty, Guid.Empty, "Nom de fichier invalide.");

        // 2. Upload vers Azure Blob Storage
        // Le BlobPath contiendra l'URL ou le chemin unique dans Azure
        var blobPath = await _blobService.UploadAsync(cmd.FileName, cmd.FileStream, "landing-zone", cmd.PeriodYear, cmd.PeriodMonth, ct);

        // 3. Création des entités de suivi (Job + File)
        var job = new IngestionJob
        {
            JobName = $"UPLOAD_{cmd.PeriodYear}_{cmd.PeriodMonth:D2}",
            ReportingPeriod = new DateOnly(cmd.PeriodYear, cmd.PeriodMonth, 1),
            Status = IngestionJobStatus.Processing,
            StartedAt = DateTime.UtcNow
        };
        await _uow.IngestionJobs.AddAsync(job, ct);

        var file = new IngestionFile
        {
            IngestionJobId = job.Id,
            FileName = cmd.FileName,
            BlobPath = blobPath, // On stocke le lien vers Azure ici
            Status = IngestionFileStatus.Validating,
            DownloadedAt = DateTime.UtcNow
        };
        await _uow.IngestionFiles.AddAsync(file, ct);

        await _uow.SaveChangesAsync(ct);

        return new InitiateFileUploadResult(true, job.Id, file.Id, null, blobPath);
    }
}