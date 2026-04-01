using PAFA.Domain.Interfaces;

namespace PAFA.Extraction.Services;

/// <summary>
/// Scoped in-memory cache that holds intermediate pipeline state for a file
/// across the Parse ? Validate ? Persist steps within a single HTTP request scope.
///
/// Not persisted to DB — exists only for the duration of a scoped DI lifetime.
/// </summary>
public sealed class FilePipelineCache
{
    private readonly Dictionary<Guid, ParsedFileEntry> _entries = new();

    public void StoreParseResult(Guid fileId, IReadOnlyList<RawDataRow> rows, int totalRows)
        => _entries[fileId] = new ParsedFileEntry(rows ?? Array.Empty<RawDataRow>(), totalRows);

    public bool TryGetParseResult(Guid fileId, out IReadOnlyList<RawDataRow> rows, out int totalRows)
    {
        if (_entries.TryGetValue(fileId, out var e))
        {
            rows = e.Rows ?? Array.Empty<RawDataRow>();
            totalRows = e.TotalRows;
            return true;
        }
        rows = Array.Empty<RawDataRow>();
        totalRows = 0;
        return false;
    }

    public void Remove(Guid fileId) => _entries.Remove(fileId);

    private sealed record ParsedFileEntry(IReadOnlyList<RawDataRow> Rows, int TotalRows);
}
