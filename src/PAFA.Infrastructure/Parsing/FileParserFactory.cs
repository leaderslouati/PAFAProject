using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.Parsing;

/// <summary>
/// Resolves the correct IFileParser implementation for a given file extension.
/// Registered parsers are injected via DI (IEnumerable&lt;IFileParser&gt;).
/// </summary>
public  class FileParserFactory
{
    private readonly IEnumerable<IFileParser> _parsers;

    public FileParserFactory(IEnumerable<IFileParser> parsers)
        => _parsers = parsers;

    /// <summary>
    /// Returns the first parser that declares it can handle the given file.
    /// Parsers may decide based on file name (prefix) or extension.
    /// Throws if no parser is registered for the file.
    /// </summary>
    public IFileParser GetParser(string fileName)
    {
        return _parsers.FirstOrDefault(p => p.CanHandle(fileName))
            ?? throw new NotSupportedException(
                $"No parser registered for file '{fileName}'.");
    }
}
