using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PAFA.Infrastructure.Parsing;

public sealed class EnergyTheftParser : IFileParser
{
    public bool CanHandle(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        return name.Contains("THEFT") || name.Contains("ENERGY_THEFT") || name.Contains("ENERGY THEFT");
    }

    public Task<FileParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var excel = new ExcelFileParser();
        return excel.ParseAsync(fileStream, fileName, ct);
    }
}
