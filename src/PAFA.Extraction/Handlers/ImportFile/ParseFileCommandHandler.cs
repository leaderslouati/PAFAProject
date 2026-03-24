using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Services;
using PAFA.Infrastructure.Parsing;

namespace PAFA.Extraction.Handlers.ImportFile;

/// <summary>
/// Step 1 of the ingestion pipeline.
/// Downloads the file from blob storage, parses it into RawDataRows,
/// caches the result for the subsequent ValidateFileCommand step,
/// and updates the IngestionFile status to Validating.
/// </summary>
public sealed class ParseFileCommandHandler
    : IRequestHandler<ParseFileCommand, ParseFileResult>
{
    private readonly IUnitOfWork _uow;
    private readonly FileParserFactory _factory;
    private readonly IBlobStorageService _blob;
    private readonly FilePipelineCache _cache;
    private readonly ILogger<ParseFileCommandHandler> _log;

    public ParseFileCommandHandler(
        IUnitOfWork uow,
        FileParserFactory factory,
        IBlobStorageService blob,
        FilePipelineCache cache,
        ILogger<ParseFileCommandHandler> log)
    {
        _uow = uow;
        _factory = factory;
        _blob = blob;
        _cache = cache;
        _log = log;
    }

    public async Task<ParseFileResult> Handle(ParseFileCommand cmd, CancellationToken ct)
    {
        // ?? 1. Load IngestionFile ?????????????????????????????????????
        var file = await _uow.IngestionFiles.GetByIdAsync(cmd.FileId, ct);
        if (file is null)
            return new ParseFileResult(false, cmd.FileId, 0, "Fichier introuvable en base de données.");

        if (string.IsNullOrWhiteSpace(file.BlobPath))
            return new ParseFileResult(false, file.Id, 0, "BlobPath manquant en base de données.");

        _log.LogInformation("[PARSE] Démarrage — {File} | {BlobPath}", file.FileName, file.BlobPath);

        // ?? 2. Resolve parser 
        IFileParser parser;
        try
        {
            parser = _factory.GetParser(file.FileName);
        }
        catch (NotSupportedException nse)
        {
            _log.LogWarning(nse, "[PARSE] Format non supporté: {File}", file.FileName);
            return await FailParse(file, nse.Message, ct);
        }

        // ?? 3. Download + parse from Blob ?????????????????????????????
        FileParseResult parsed;
        try
        {
            using var stream = await _blob.DownloadStreamAsync(file.BlobPath, ct);
            parsed = await parser.ParseAsync(stream, file.FileName, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[PARSE] Erreur lors du téléchargement/parsing — {File}", file.FileName);
            return await FailParse(file, ex.Message, ct);
        }

        if (!parsed.Success)
            return await FailParse(file, parsed.ErrorMessage ?? "Parsing échoué.", ct);

        // ?? 4. Cache rows for next step ???????????????????????????????
        _cache.StoreParseResult(file.Id, parsed.Rows, parsed.TotalRows);

        // ?? 5. Update file status ?????????????????????????????????????
        file.Status = IngestionFileStatus.Validating;
        file.RowsRead = parsed.TotalRows;
        _uow.IngestionFiles.Update(file);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation("[PARSE] OK — {File} | {Rows} lignes lues", file.FileName, parsed.TotalRows);

        return new ParseFileResult(true, file.Id, parsed.TotalRows, null, parsed.Rows);
    }

    private async Task<ParseFileResult> FailParse(
        PAFA.Domain.Entities.IngestionFile file, string err, CancellationToken ct)
    {
        file.Status = IngestionFileStatus.Failed;
        _uow.IngestionFiles.Update(file);
        await _uow.SaveChangesAsync(ct);
        return new ParseFileResult(false, file.Id, 0, err);
    }
}
