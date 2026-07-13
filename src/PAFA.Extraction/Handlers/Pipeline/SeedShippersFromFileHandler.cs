using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.IRepository;
using PAFA.Extraction.Commands.Pipeline;
using PAFA.Infrastructure.Parsing;

namespace PAFA.Extraction.Handlers.Pipeline;

/// <summary>
/// Seeds (upserts) the shipper master list from the "Anonymised Shipper List" Excel
/// workbook.
///
/// The file is password-protected with OOXML encryption.
/// The password MUST be injected at runtime from a secure secret store — it must
/// NOT be hard-coded or stored in source control.
///
/// Processing steps:
///   1. <see cref="ShipperListFileParser"/> decrypts and parses the workbook.
///   2. <see cref="IShipperRepository.UpsertShippersAsync"/> inserts new shippers
///      and updates existing ones (keyed on ShortCode).
/// </summary>
public sealed class SeedShippersFromFileHandler
    : IRequestHandler<SeedShippersFromFileCommand, SeedShippersResult>
{
    private readonly IShipperRepository        _shipperRepo;
    private readonly ShipperListFileParser      _parser;
    private readonly ILogger<SeedShippersFromFileHandler> _log;

    public SeedShippersFromFileHandler(
        IShipperRepository        shipperRepo,
        ShipperListFileParser      parser,
        ILogger<SeedShippersFromFileHandler> log)
    {
        _shipperRepo = shipperRepo;
        _parser      = parser;
        _log         = log;
    }

    public async Task<SeedShippersResult> Handle(
        SeedShippersFromFileCommand cmd, CancellationToken ct)
    {
        _log.LogInformation(
            "Shipper seed starting — CorrelationId: {CorrelationId}",
            cmd.CorrelationId);

        IReadOnlyList<Domain.Entities.Referential.Shipper> shippers;

        try
        {
            shippers = _parser.Parse(cmd.FileStream, cmd.Password);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Failed to parse Shipper List file — CorrelationId: {CorrelationId}",
                cmd.CorrelationId);

            return new SeedShippersResult(
                Success: false,
                TotalParsed: 0,
                Inserted: 0,
                Updated: 0,
                ErrorMessage: $"Parse error: {ex.Message}");
        }

        if (shippers.Count == 0)
        {
            _log.LogWarning(
                "Shipper List file contained no valid rows — CorrelationId: {CorrelationId}",
                cmd.CorrelationId);

            return new SeedShippersResult(
                Success: true,
                TotalParsed: 0,
                Inserted: 0,
                Updated: 0,
                ErrorMessage: "No valid shipper rows found in the file.");
        }

        try
        {
            var (inserted, updated) = await _shipperRepo.UpsertShippersAsync(shippers, ct);

            _log.LogInformation(
                "Shipper seed completed — Parsed: {Total}, Inserted: {Inserted}, Updated: {Updated} — CorrelationId: {CorrelationId}",
                shippers.Count, inserted, updated, cmd.CorrelationId);

            return new SeedShippersResult(
                Success: true,
                TotalParsed: shippers.Count,
                Inserted: inserted,
                Updated: updated);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Failed to upsert shippers to database — CorrelationId: {CorrelationId}",
                cmd.CorrelationId);

            return new SeedShippersResult(
                Success: false,
                TotalParsed: shippers.Count,
                Inserted: 0,
                Updated: 0,
                ErrorMessage: $"Database upsert error: {ex.Message}");
        }
    }
}
