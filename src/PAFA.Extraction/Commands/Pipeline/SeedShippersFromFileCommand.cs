using MediatR;

namespace PAFA.Extraction.Commands.Pipeline;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Seeds (upserts) the shipper master list from the "Anonymised Shipper List" Excel file.
///
/// The file is password-protected (OOXML encryption). The password must be
/// supplied at runtime (e.g. from a secret / environment variable, NOT hard-coded).
///
/// Typical invocation:
///   • One-time initial seed when deploying
///   • When a new version of the Anonymised Shipper List is received
/// </summary>
/// <param name="FileStream">Open, readable stream of the Excel file.</param>
/// <param name="Password">Workbook password used to decrypt the file.</param>
/// <param name="CorrelationId">Trace identifier for logging.</param>
public sealed record SeedShippersFromFileCommand(
    Stream      FileStream,
    string      Password,
    Guid        CorrelationId
) : IRequest<SeedShippersResult>;

// ── Result ────────────────────────────────────────────────────────────────────

public sealed record SeedShippersResult(
    bool   Success,
    int    TotalParsed,
    int    Inserted,
    int    Updated,
    string? ErrorMessage = null);
