using System.Text.RegularExpressions;

namespace PAFA.Extraction.Mapping;

/// <summary>
/// Résout le ReportCode (ex: "2A.1", "2B.11a") d'une ligne de données en fonction
/// du nom du fichier source et du nom de l'onglet Excel.
///
/// Source officielle du mapping : Files/PARR Reports - Mapping.xlsx
///
/// Règles de résolution (par priorité) :
///   1. Le nom de l'onglet contient directement un code PARR  → on l'extrait par regex.
///   2. Le nom du fichier correspond à un préfixe connu qui produit UN seul report code.
///   3. Le nom du fichier + l'onglet sont combinés via les tables ci-dessous.
/// </summary>
public static class ReportCodeResolver
{
    // ── Regex pour extraire "2A.1", "2B.11a", "2A.12c" depuis un nom d'onglet ──
    private static readonly Regex SheetCodeRegex =
        new(@"\b(2[ABab]\.\d+[a-zA-Z]?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Mapping : préfixe de fichier → list de reportCodes
    // Quand un fichier produit plusieurs codes, la résolution fine se fait via
    // le nom de l'onglet (voir MOD520A ci-dessous).
    // Quand un fichier produit toujours UN seul code, on l'inscrit directement.
    private static readonly Dictionary<string, string[]> PrefixToReportCodes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ─────── CDSP / SharePoint ────────────────────────────────────────
            // MOD520A alimente 2A.1-2A.10 ET 2B.1-2B.10 (multi-onglets)
            // La résolution fine se fait par nom d'onglet (voir Mod520ASheetMap)
            ["MOD520A"]               = ["2A.1","2A.2","2A.3","2A.4","2A.5",
                                          "2A.6","2A.7","2A.8","2A.9","2A.10",
                                          "2B.1","2B.2","2B.3","2B.4","2B.5",
                                          "2B.6","2B.7","2B.8","2B.9","2B.10"],

            // EUC09 alimente 2A.11a, 2A.11b + 2B.14a, 2B.14b (multi-onglets)
            ["EUC09"]                 = ["2A.11a","2A.11b","2B.14a","2B.14b"],

            // RPT_1364 alimente 2B.11a à 2B.11h (multi-onglets)
            ["RPT_1364"]              = ["2B.11a","2B.11b","2B.11c","2B.11d",
                                          "2B.11e","2B.11f","2B.11g","2B.11h"],
            ["Rpt_1364"]              = ["2B.11a","2B.11b","2B.11c","2B.11d",
                                          "2B.11e","2B.11f","2B.11g","2B.11h"],

            // AQ at Risk → 2A.13 et 2B.16
            ["AQ at Risk"]            = ["2A.13","2B.16"],
            ["AQatRisk"]              = ["2A.13","2B.16"],

            // Transfer Read → 2A.4 / 2B.4
            ["Shipper Transfer Read"] = ["2A.4","2B.4"],
            ["Transfer Read"]         = ["2A.4","2B.4"],

            // Class 4 Read Performance (DDP) → 2A.12a/b/c, 2B.15a/b/c
            ["Read Performance by Shipper"] = ["2A.12a","2A.12b","2A.12c",
                                               "2B.15a","2B.15b","2B.15c"],
            ["Class 4 Read"]          = ["2A.12a","2A.12b","2A.12c",
                                          "2B.15a","2B.15b","2B.15c"],

            // Energy Theft objections → 2A.14 / 2B.17
            ["Confirmed Energy Theft Claim"]      = ["2A.14","2B.17"],
            ["Confirmed Energy Theft Withdrawal"] = ["2A.14","2B.17"],

            // Supply Points Reclassified → 2A.15 / 2B.18 (DDP)
            ["Supply Points with Minimum Percentage Requirement"] = ["2A.15","2B.18"],
            ["Supply Points with Minimum Threshold"]              = ["2A.15","2B.18"],

            // Supply Points Not Met → 2A.16 / 2B.19 (DDP)
            ["Supply Points and AQ with Minimum"] = ["2A.16","2B.19"],

            // Class 3 conversion (contexte 2A.15/2B.18)
            ["Class 3 conversion"]    = ["2A.15","2B.18"],

            // Report 1A/1B → Vacant Sites → 2A.19 / 2B.22
            ["Report 1A"]             = ["2A.19","2B.22"],
            ["Report 1B"]             = ["2A.19","2B.22"],

            // Report 1/2/3 MPRN → 2A.17 / 2B.20
            ["Report 1 -"]            = ["2A.17","2B.20"],
            ["Report 2 -"]            = ["2A.17","2B.20"],
            ["Report 3 -"]            = ["2A.17","2B.20"],

            // Supply Point Counts → complément pour 2A.2
            ["SupplyPointCount"]      = ["2A.2"],

            // COMR Rejections → 2A.18 / 2B.21
            ["2B.21 Corrective"]      = ["2A.18","2B.21"],
            ["Corrective Opening Meter Reading"] = ["2A.18","2B.21"],
        };

    // ── Mapping onglet MOD520A → ReportCode
    // Basé sur les noms d'onglets attendus dans le fichier MOD520A.
    // Stratégie : match insensible à la casse sur les mots-clés de chaque rapport.
    private static readonly (string Keyword, string ReportCode)[] Mod520ASheetKeywords =
    [
        // 2B (non-anonymisé) — priorité si l'onglet dit "Non Anon" ou "2B"
        ("2b.1",  "2B.1"),  ("2b.2",  "2B.2"),  ("2b.3",  "2B.3"),
        ("2b.4",  "2B.4"),  ("2b.5",  "2B.5"),  ("2b.6",  "2B.6"),
        ("2b.7",  "2B.7"),  ("2b.8",  "2B.8"),  ("2b.9",  "2B.9"),
        ("2b.10", "2B.10"),

        // 2A (anonymisé)
        ("2a.1",  "2A.1"),  ("2a.2",  "2A.2"),  ("2a.3",  "2A.3"),
        ("2a.4",  "2A.4"),  ("2a.5",  "2A.5"),  ("2a.6",  "2A.6"),
        ("2a.7",  "2A.7"),  ("2a.8",  "2A.8"),  ("2a.9",  "2A.9"),
        ("2a.10", "2A.10"),

        // Mots-clés du titre de l'onglet quand le code n'est pas dans le nom
        ("estimated",        "2A.1"),
        ("check read",       "2A.1"),
        ("no meter",         "2A.2"),
        ("data flow",        "2A.3"),
        ("transfer read",    "2A.4"),
        ("read performance", "2A.5"),
        ("meter read valid", "2A.6"),
        ("no read",          "2A.7"),
        ("aq correction",    "2A.8"),
        ("standard cf",      "2A.9"),
        ("replaced",         "2A.10"),
    ];

    // ── Mapping onglet EUC09 → ReportCode ────────────────────────────────────
    private static readonly (string Keyword, string ReportCode)[] Euc09SheetKeywords =
    [
        ("class 1 threshold",   "2A.11a"),
        ("above threshold",     "2A.11a"),
        ("reclassif",           "2A.11b"),
        ("2a.11a",              "2A.11a"),
        ("2a.11b",              "2A.11b"),
        ("2b.14a",              "2B.14a"),
        ("2b.14b",              "2B.14b"),
        ("14a",                 "2B.14a"),
        ("14b",                 "2B.14b"),
    ];

    // ── Mapping onglet RPT_1364 → ReportCode ─────────────────────────────────
    private static readonly (string Keyword, string ReportCode)[] Rpt1364SheetKeywords =
    [
        ("portfolio calculation failure", "2B.11h"),
        ("portfolio failure",             "2B.11h"),
        ("portfolio increase 12",         "2B.11f"),
        ("portfolio decrease 12",         "2B.11g"),
        ("portfolio increase",            "2B.11b"),
        ("portfolio decrease",            "2B.11c"),
        ("by frequency",                  "2B.11d"),
        ("by month",                      "2B.11e"),
        ("portfolio calculation",         "2B.11a"),  // fallback le plus générique en dernier
        ("2b.11h",  "2B.11h"), ("2b.11f",  "2B.11f"), ("2b.11g",  "2B.11g"),
        ("2b.11b",  "2B.11b"), ("2b.11c",  "2B.11c"), ("2b.11d",  "2B.11d"),
        ("2b.11e",  "2B.11e"), ("2b.11a",  "2B.11a"),
    ];

    /// <summary>
    /// Résout le ReportCode à partir du nom de fichier et du nom de l'onglet.
    /// Retourne null si aucun code ne peut être déterminé.
    /// </summary>
    public static string? Resolve(string fileName, string sheetName)
    {
        // ── Priorité 1 : le nom de l'onglet contient déjà le code PARR ─────
        var fromSheet = ExtractCodeFromSheetName(sheetName);
        if (fromSheet != null)
            return fromSheet;

        // ── Priorité 2 : résolution via le préfixe du fichier + onglet ──────
        var prefix = DetectFilePrefix(fileName);
        if (prefix == null)
            return null;

        return prefix switch
        {
            "MOD520A"  => ResolveByKeywords(sheetName, Mod520ASheetKeywords)  ?? "2A.1",
            "EUC09"    => ResolveByKeywords(sheetName, Euc09SheetKeywords)    ?? "2A.11a",
            "RPT_1364" => ResolveByKeywords(sheetName, Rpt1364SheetKeywords)  ?? "2B.11a",
            _          => ResolveByPrefix(prefix)
        };
    }

    /// <summary>
    /// Retourne le préfixe de fichier canonique (ex: "MOD520A", "EUC09", …)
    /// en cherchant dans les clés du dictionnaire dans l'ordre de longueur décroissante
    /// (les préfixes les plus spécifiques d'abord).
    /// </summary>
    public static string? DetectFilePrefix(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Tri par longueur décroissante pour éviter que "Report 1" matche avant "Report 1A"
        foreach (var key in PrefixToReportCodes.Keys
                     .OrderByDescending(k => k.Length))
        {
            if (fileName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return key;
        }
        return null;
    }

    /// <summary>
    /// Quand un préfixe de fichier mène à exactement UN report code, le retourne.
    /// Sinon retourne null (ambigüité → besoin du nom d'onglet).
    /// </summary>
    private static string? ResolveByPrefix(string prefix)
    {
        if (!PrefixToReportCodes.TryGetValue(prefix, out var codes))
            return null;

        return codes.Length == 1 ? codes[0] : null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string? ExtractCodeFromSheetName(string sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
            return null;

        var m = SheetCodeRegex.Match(sheetName);
        if (!m.Success)
            return null;

        // Normalise la casse : "2a.1" → "2A.1", "2b.11a" → "2B.11a"
        var raw = m.Value;
        var parts = raw.Split('.');
        if (parts.Length < 2)
            return raw.ToUpperInvariant();

        return parts[0].ToUpperInvariant() + "." + parts[1];
    }

    private static string? ResolveByKeywords(
        string sheetName,
        (string Keyword, string ReportCode)[] keywords)
    {
        var lower = sheetName.ToLowerInvariant();
        foreach (var (kw, code) in keywords)
        {
            if (lower.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return code;
        }
        return null;
    }
}
