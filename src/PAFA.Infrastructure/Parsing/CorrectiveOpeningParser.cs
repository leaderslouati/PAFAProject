using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PAFA.Infrastructure.Parsing;

public sealed class CorrectiveOpeningParser : IFileParser
{
    public bool CanHandle(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        return name.Contains("CORRECTIVE") || name.Contains("CORRECTIVE_OPENING") || name.Contains("CORRECTIVE OPENING");
    }

    public Task<FileParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var excel = new ExcelFileParser();
        return excel.ParseAsync(fileStream, fileName, ct);
    }
}
