using System.Xml.Linq;

namespace PAFA.Infrastructure.Parsing;

/// <summary>
/// Parser pour les fichiers XML fournis par Xoserve sur le SFTP.
///
/// Supporte deux structures XML communes :
///
/// Structure 1 — éléments enfants comme colonnes (format "row-based") :
///   &lt;Report&gt;
///     &lt;Row&gt;
///       &lt;ShipperShortCode&gt;SSE&lt;/ShipperShortCode&gt;
///       &lt;ReportingPeriod&gt;Mar-25&lt;/ReportingPeriod&gt;
///       &lt;ReadPerformancePct&gt;97.82&lt;/ReadPerformancePct&gt;
///     &lt;/Row&gt;
///     &lt;Row&gt; ... &lt;/Row&gt;
///   &lt;/Report&gt;
///
/// Structure 2 — attributs comme colonnes (format "attribute-based") :
///   &lt;Report&gt;
///     &lt;Row ShipperShortCode="SSE" ReportingPeriod="Mar-25" ReadPerformancePct="97.82" /&gt;
///   &lt;/Report&gt;
///
/// Dans les deux cas, le résultat produit des RawDataRow normalisés
/// identiques à ceux produits par ExcelFileParser et CsvFileParser.
/// Le même MetricValueMapper et ImportValidationService s'appliquent sans modification.
/// </summary>
public sealed class XmlFileParser : IFileParser
{
    public bool CanHandle(string fileExtension)
        => fileExtension.ToLowerInvariant() is ".xml";

    public Task<FileParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            var doc = XDocument.Load(fileStream);
            var root = doc.Root;

            if (root is null)
                return Task.FromResult(Fail(fileName, "Fichier XML vide ou racine manquante."));

            // ?? Détecter les éléments "ligne" ??????????????????????????????
            // On prend les premiers enfants directs de la racine qui ont
            // eux-mêmes des enfants ou des attributs — ce sont nos lignes.
            var rowElements = root.Elements()
                .Where(e => e.HasElements || e.HasAttributes)
                .ToList();

            if (rowElements.Count == 0)
                return Task.FromResult(Fail(fileName,
                    "Aucune ligne de données trouvée dans le XML. " +
                    "Vérifiez la structure : éléments enfants ou attributs attendus."));

            var rows = new List<RawDataRow>();
            int rowNumber = 1;

            foreach (var element in rowElements)
            {
                var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                // Structure 1 : enfants comme colonnes
                // <Row><ShipperShortCode>SSE</ShipperShortCode></Row>
                foreach (var child in element.Elements())
                {
                    var key = Normalize(child.Name.LocalName);
                    var value = child.Value.Trim();
                    if (!string.IsNullOrEmpty(key))
                        cells[key] = string.IsNullOrWhiteSpace(value) ? null : value;
                }

                // Structure 2 : attributs comme colonnes
                // <Row ShipperShortCode="SSE" ReportingPeriod="Mar-25" />
                foreach (var attr in element.Attributes())
                {
                    // Ignorer les attributs de namespace XML
                    if (attr.IsNamespaceDeclaration) continue;
                    var key = Normalize(attr.Name.LocalName);
                    var value = attr.Value.Trim();
                    if (!string.IsNullOrEmpty(key))
                        cells.TryAdd(key, string.IsNullOrWhiteSpace(value) ? null : value);
                }

                // Ignorer les lignes complètement vides
                if (cells.Count == 0 || cells.Values.All(v => string.IsNullOrWhiteSpace(v)))
                    continue;

                rows.Add(new RawDataRow
                {
                    RowNumber = rowNumber++,
                    SheetName = root.Name.LocalName, // racine comme "sheet name"
                    Cells = cells
                });
            }

            if (rows.Count == 0)
                return Task.FromResult(Fail(fileName,
                    "Le fichier XML ne contient aucune ligne de données valide."));

            return Task.FromResult(new FileParseResult
            {
                Success = true,
                FileName = fileName,
                DetectedFileType = DetectFileType(fileName),
                Rows = rows,
                RowsPerSheet = new Dictionary<string, int>
                {
                    [root.Name.LocalName] = rows.Count
                }
            });
        }
        catch (System.Xml.XmlException ex)
        {
            return Task.FromResult(Fail(fileName,
                $"XML invalide (ligne {ex.LineNumber}, col {ex.LinePosition}) : {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(fileName, $"Erreur lecture XML : {ex.Message}"));
        }
    }

    // ?? Helpers ????????????????????????????????????????????????????????????

    /// <summary>
    /// Normalise un nom d'élément/attribut XML vers le format attendu par
    /// MetricValueMapper et ImportValidationService.
    /// Ex: "ShipperShortCode" ? "shippershortcode"
    ///     "Read_Performance_Pct" ? "readperformancepct"
    ///     "read-performance-pct" ? "readperformancepct"
    /// </summary>
    private static string Normalize(string raw)
        => raw.Trim()
              .ToLowerInvariant()
              .Replace(" ", "")
              .Replace("_", "")
              .Replace("-", "");

    private static string DetectFileType(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        return name switch
        {
            _ when name.Contains("MOD520")   => "MOD520A",
            _ when name.Contains("AQ")       => "AQ_REPORT",
            _ when name.Contains("NOREADS")  => "NO_READS",
            _ when name.Contains("VACANT")   => "VACANT_SITES",
            _ when name.Contains("EUC09")    => "EUC09",
            _ when name.Contains("RPT_1364") => "RPT_1364",
            _ when name.Contains("PARR")     => "PARR",
            _ => "UNKNOWN"
        };
    }

    private static FileParseResult Fail(string fileName, string error) => new()
    {
        Success = false,
        FileName = fileName,
        ErrorMessage = error,
        DetectedFileType = "UNKNOWN"
    };
}
