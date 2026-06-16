using CsvHelper;
using CsvHelper.Configuration;
using PAFA.Domain.Interfaces;
using System.Globalization;

namespace PAFA.Infrastructure.Parsing;

public sealed class CsvFileParser : IFileParser
{
    public bool CanHandle(string fileName)
        => (Path.GetExtension(fileName) ?? string.Empty).ToLowerInvariant() is ".csv";

    public async Task<FileParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            using var reader = new StreamReader(fileStream);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
                BadDataFound = null
            };

            using var csv = new CsvReader(reader, config);
            await csv.ReadAsync();
            csv.ReadHeader();

            var headers = csv.HeaderRecord?
                .Select(h => h.Trim().ToLowerInvariant().Replace(" ", ""))
                .ToArray() ?? [];

            var rows = new List<RawDataRow>();
            int rowNumber = 2;

            while (await csv.ReadAsync())
            {
                var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                    cells[header] = csv.GetField(header)?.Trim();

                if (cells.Values.All(v => string.IsNullOrWhiteSpace(v)))
                    continue;

                rows.Add(new RawDataRow
                {
                    RowNumber = rowNumber++,
                    SheetName = "",
                    Cells = cells
                });
            }

            return new FileParseResult
            {
                Success = true,
                FileName = fileName,
                DetectedFileType = "UNKNOWN",
                Rows = rows,
                RowsPerSheet = new Dictionary<string, int> { [""] = rows.Count }
            };
        }
        catch (Exception ex)
        {
            return new FileParseResult
            {
                Success = false,
                FileName = fileName,
                ErrorMessage = $"Erreur lecture CSV : {ex.Message}"
            };
        }
    }
}