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
    /// Returns the parser that can handle the given file extension.
    /// Throws if no parser is registered for that extension.
    /// </summary>
    public IFileParser GetParser(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return _parsers.FirstOrDefault(p => p.CanHandle(ext))
            ?? throw new NotSupportedException(
                $"No parser registered for extension '{ext}'. File: {fileName}");
    }
}
