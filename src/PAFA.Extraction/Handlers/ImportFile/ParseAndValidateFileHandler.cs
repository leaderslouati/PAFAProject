using MediatR;
using PAFA.Domain.Entities;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Infrastructure.Parsing;
using System.Globalization;

namespace PAFA.Extraction.Handlers.ImportFile
{
    public class ParseAndValidateFileHandler : IRequestHandler<ParseAndValidateFileCommand, ParseAndValidateFileResult>
    {
        private readonly IMetricValueRepository _metricValueRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly FileParserFactory _parserFactory;

        public ParseAndValidateFileHandler(
            IMetricValueRepository metricValueRepository,
            IUnitOfWork unitOfWork,
            FileParserFactory parserFactory)
        {
            _metricValueRepository = metricValueRepository;
            _unitOfWork            = unitOfWork;
            _parserFactory         = parserFactory;
        }

        public async Task<ParseAndValidateFileResult> Handle(
            ParseAndValidateFileCommand request,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(request.BlobPath))
                return new ParseAndValidateFileResult(request.FileId, false, "FAILED", 0, 0, 0, "Fichier introuvable.");

            // ── 1. Parse le fichier (Excel ou CSV selon l'extension) ──────────
            var parser = _parserFactory.GetParser(request.FileName);

            FileParseResult parseResult;
            await using (var stream = File.OpenRead(request.BlobPath))
            {
                parseResult = await parser.ParseAsync(stream, request.FileName, cancellationToken);
            }

            if (!parseResult.Success)
                return new ParseAndValidateFileResult(
                    request.FileId, false, "FAILED",
                    0, 0, 0, parseResult.ErrorMessage ?? "Parsing failed.");

            // ── 2. Mapper les RawDataRow → MetricValue ────────────────────────
            var metricsToInsert = new List<MetricValue>();
            int rowsRead = parseResult.TotalRows, rowsValid = 0, rowsRejected = 0;

            foreach (var row in parseResult.Rows)
            {
                //   "shippershortcode", "metrickey", "value"
                var shipperCode = row.Cells.GetValueOrDefault("shippershortcode")?.Trim();
                var metricKey   = row.Cells.GetValueOrDefault("metrickey")?.Trim();
                var valueStr    = row.Cells.GetValueOrDefault("value")?.Trim().Replace(",", ".");

                if (string.IsNullOrWhiteSpace(shipperCode) ||
                    string.IsNullOrWhiteSpace(metricKey)   ||
                    !decimal.TryParse(valueStr, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out decimal metricValue))
                {
                    rowsRejected++;
                    continue;
                }

                metricsToInsert.Add(new MetricValue
                {
                    Id               = Guid.NewGuid(),
                    IngestionFileId  = request.FileId,
                    ShipperShortCode = shipperCode,
                    MetricKey        = metricKey,
                    Value            = metricValue,
                    ReportingPeriod = DateOnly.FromDateTime(DateTime.UtcNow), // TODO: extraire de la ligne ou du nom de fichier
                    CreatedBy        = "POC_System",
                    CreatedAt        = DateTime.UtcNow
                });
                rowsValid++;
            }

            // ── 3. Persister en base ──────────────────────────────────────────
            if (metricsToInsert.Any())
            {
                await _metricValueRepository.AddRangeAsync(metricsToInsert, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new ParseAndValidateFileResult(
                request.FileId, true, "COMPLETED",
                rowsRead, rowsValid, rowsRejected, null);
        }
    }
}