namespace PAFA.Infrastructure.Parsing;

/// <summary>
/// Représente une ligne brute extraite du fichier source.
/// Tous les champs sont des strings — la conversion de types
/// se fait dans le mapper (couche Application).
/// </summary>
public  record RawDataRow
{
    /// <summary>Numéro de ligne original (base 1) dans le fichier.</summary>
    public int RowNumber { get; init; }

    /// <summary>
    /// Toutes les valeurs indexées par nom de colonne normalisé
    /// (minuscules, sans espaces).
    /// Ex: { "shippershortcode": "ABN", "reportingperiod": "Feb-25" }
    /// </summary>
    public Dictionary<string, string?> Cells { get; init; } = new();

    /// <summary>Nom de l'onglet source.</summary>
    public string SheetName { get; init; } = string.Empty;
}

/// <summary>Résultat complet du parsing d'un fichier.</summary>
public  record FileParseResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string FileName { get; init; } = string.Empty;
    /// <summary>MOD520A | AQ_REPORT | NO_READS | VACANT_SITES | etc.</summary>
    public string DetectedFileType { get; init; } = string.Empty;
    public List<RawDataRow> Rows { get; init; } = new();
    public Dictionary<string, int> RowsPerSheet { get; init; } = new();
    public int TotalRows => Rows.Count;
}

/// <summary>
/// Contrat de parsing de fichier.
/// Implémenter pour chaque format (Excel, XML, CSV).
/// Enregistrer toutes les implémentations dans DI —
/// la factory résout par extension.
/// </summary>
public interface IFileParser
{
    /// <summary>Retourne true si ce parser gère cette extension.</summary>
    bool CanHandle(string fileExtension);

    /// <summary>
    /// Parse le stream et retourne les lignes brutes.
    /// NE fait PAS de validation métier — extraction structurelle uniquement.
    /// </summary>
    Task<FileParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default);
}