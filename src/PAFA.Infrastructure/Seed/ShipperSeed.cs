namespace PAFA.Infrastructure.Seed;

/// <summary>
/// Liste de seed pour les shippers et leurs alias d'anonymisation.
/// </summary>
public static class ShipperSeed
{
    public static readonly (string ShortCode, string Name, string AliasCode)[] Shippers = {
        ("AGA", "Total Energies Gas & Power Limited",    "Rome"),
        ("BRK", "Brook Green Trading Ltd",               "Manama"),
        ("BUS", "British Gas Trading Limited",           "Brazzaville"),
        ("NGS", "SEFE Energy Limited",                   "Gitega"),
        ("GLC", "Corona Energy Retail 4 Limited",        "Papeete"),
        ("SHE", "SSE Energy Supply Ltd",                 "Washington"),
        ("PSL", "P3P Energy Supply Limited",             "Bangui"),
        ("EUK", "Axpo UK Limited",                       "Valletta"),
        ("NGD", "NPower Commercial Gas Ltd",             "Thimphu"),
        ("SBP", "Sembcorp Utilities (UK) Limited",       "Canberra"),
        ("VOL", "ENGIE Gas Shipper Limited",             "Philipsburg"),
        ("HUD", "Shell Energy UK Limited",               "Lisbon"),
        ("SOG", "EDF Energy Customers PLC",              "Taipei"),
        ("BSH", "Barrow Shipping Limited",               "Marigot"),
        ("MRB", "Marble Power Ltd",                      "Warsaw"),
        ("RWE", "RWE Supply & Trading GMBH",             "Tehran"),
        ("TET", "Ceres Energy Limited",                  "Sarajevo"),
        ("VEC", "Corona Energy Retail 2 Limited",        "Ankara"),
        ("DRA", "Drax Power Limited",                    "Khartoum"),
        ("FLX", "Flexitricity Limited",                  "Monaco"),
        ("HEP", "Hartree Partners Power & Gas Co Ltd",   "Luanda"),
        // Shippers vus dans les fichiers source (à confirmer CDSP) :
        ("ACS", "ACS (à confirmer)",                     "ACS"),
        ("APE", "APE (à confirmer)",                     "APE"),
        ("BNG", "BNG (à confirmer)",                     "BNG"),
        ("OET", "OET (à confirmer)",                     "OET"),
        ("SLD", "SLD (à confirmer)",                     "SLD"),
    };
}