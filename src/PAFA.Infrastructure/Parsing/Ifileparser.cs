// Types moved to PAFA.Domain.Interfaces.IFileParser — kept as global aliases
// for backward compatibility inside Infrastructure parsers.
global using PAFA.Domain.Interfaces;
global using RawDataRow    = PAFA.Domain.Interfaces.RawDataRow;
global using FileParseResult = PAFA.Domain.Interfaces.FileParseResult;
global using IFileParser   = PAFA.Domain.Interfaces.IFileParser;

namespace PAFA.Infrastructure.Parsing;
// This file is intentionally empty — all contracts live in PAFA.Domain.Interfaces.IFileParser.