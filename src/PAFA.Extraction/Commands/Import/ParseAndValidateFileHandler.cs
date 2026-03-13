using MediatR;
using PAFA.Domain.Entities;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PAFA.Extraction.Handlers.ImportFile
{
    public class ParseAndValidateFileHandler : IRequestHandler<ParseAndValidateFileCommand, ParseAndValidateFileResult>
    {
        private readonly IMetricValueRepository _metricValueRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ParseAndValidateFileHandler(IMetricValueRepository metricValueRepository, IUnitOfWork unitOfWork)
        {
            _metricValueRepository = metricValueRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ParseAndValidateFileResult> Handle(ParseAndValidateFileCommand request, CancellationToken cancellationToken)
        {
            if (!File.Exists(request.BlobPath))
            {
                return new ParseAndValidateFileResult(request.FileId, false, "FAILED", 0, 0, 0, "Fichier introuvable.");
            }

            var metricsToInsert = new List<MetricValue>();
            int rowsRead = 0, rowsValid = 0, rowsRejected = 0;

            var lines = await File.ReadAllLinesAsync(request.BlobPath, cancellationToken);

            // On ignore la première ligne (les en-têtes)
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                rowsRead++;

                // 1. Gérer automatiquement le séparateur (, ou ;)
                char separator = line.Contains(";") ? ';' : ',';
                var columns = line.Split(separator);

                if (columns.Length >= 3 && !string.IsNullOrWhiteSpace(columns[0]))
                {
                    // 2. Nettoyer le nombre (remplacer les virgules par des points pour la culture Invariant)
                    string valueStr = columns[2].Trim().Replace(",", ".");

                    if (decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal metricValue))
                    {
                        metricsToInsert.Add(new MetricValue
                        {
                            Id = Guid.NewGuid(), // Générer un ID
                            IngestionFileId = request.FileId,
                            ShipperShortCode = columns[0].Trim(),
                            MetricKey = columns[1].Trim(),
                            Value = metricValue,
                            PeriodYear = request.PeriodYear,
                            PeriodMonth = request.PeriodMonth,
                            CreatedBy = "POC_System",
                            CreatedAt = DateTime.UtcNow
                        });
                        rowsValid++;
                    }
                    else
                    {
                        rowsRejected++; // Ce n'est pas un nombre valide
                    }
                }
                else
                {
                    rowsRejected++; // Ligne incomplète
                }
            }

            // Insertion en masse dans PostgreSQL
            if (metricsToInsert.Any())
            {
                await _metricValueRepository.AddRangeAsync(metricsToInsert, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new ParseAndValidateFileResult(
                request.FileId, true, "COMPLETED", rowsRead, rowsValid, rowsRejected, null);
        }
    }
}