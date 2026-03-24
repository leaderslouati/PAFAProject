#!/usr/bin/env pwsh
# =============================================================================
# generate-test-files.ps1
#
# Génère des fichiers Excel (.xlsx) de test au format Xoserve PARR
# et les dépose dans xoserve/upload/ pour tester le flux d'ingestion.
#
# Usage :
#   .\generate-test-files.ps1
#   .\generate-test-files.ps1 -Period "Mar25"
#   .\generate-test-files.ps1 -Period "Feb25" -ShipperCount 5
#
# Pré-requis :
#   dotnet tool install -g ClosedXML.SimpleSheets  (pas nécessaire — on utilise un mini .NET inline)
#   OU : le script utilise le SDK .NET déjà installé
# =============================================================================

param(
    [string]$Period = "Mar25",
    [int]$ShipperCount = 8,
    [string]$OutputDir = "xoserve/upload"
)

$ErrorActionPreference = "Stop"

# Créer le dossier si nécessaire
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ?? Créer un mini-projet .NET temporaire pour générer le Excel ??
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "pafa-test-gen-$(Get-Random)"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

try {
    # 1. Créer le csproj
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ClosedXML" Version="0.105.0" />
  </ItemGroup>
</Project>
"@ | Set-Content "$tempDir/GenTestFiles.csproj"

    # 2. Créer le Program.cs
    @"
using ClosedXML.Excel;

var period = args[0];           // "Mar25"
var shipperCount = int.Parse(args[1]);
var outputDir = args[2];

var rng = new Random(42);

var shippers = new[] { "SSE", "BGT", "OVO", "EON", "EDF", "SCO", "BUL", "SHE", "UTE", "IGT", "COR", "ESP" };
var usedShippers = shippers.Take(shipperCount).ToArray();

// ?? File 1 : MOD520A (Read Performance) ??????????????????????
GenerateFile(
    Path.Combine(outputDir, $"MOD520A_PAF_Reports_{period}_Non_Anonymised.xlsx"),
    period, usedShippers, rng, "MOD520A");

// ?? File 2 : RPT_1364 (AQ Corrections) ?????????????????????
GenerateFile(
    Path.Combine(outputDir, $"RPT_1364_AQ_Corrections_{period}.xlsx"),
    period, usedShippers, rng, "RPT_1364");

Console.WriteLine($"Generated 2 test files in {outputDir}/");
foreach (var f in Directory.GetFiles(outputDir, "*.xlsx"))
    Console.WriteLine($"  {Path.GetFileName(f)} ({new FileInfo(f).Length:N0} bytes)");

static void GenerateFile(string path, string period, string[] shippers, Random rng, string type)
{
    using var wb = new XLWorkbook();
    var ws = wb.Worksheets.Add("Data");

    // Headers
    var headers = new[] {
        "Shipper Short Code", "Reporting Period", "Product Class",
        "Read Performance Pct", "Estimated Read Pct", "Total Site Count",
        "Check Read Count", "No Reads 1yr", "No Reads 2yr",
        "Transfer Read Succ", "AQ Correction Count", "Class4 AQ Read Pct",
        "Energy Theft Count", "Invalid Read Count", "Data Flows Received"
    };

    for (int c = 0; c < headers.Length; c++)
        ws.Cell(1, c + 1).Value = headers[c];

    // Style headers
    var headerRange = ws.Range(1, 1, 1, headers.Length);
    headerRange.Style.Font.Bold = true;
    headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;

    // Data rows
    int row = 2;
    foreach (var ssc in shippers)
    {
        for (int pc = 1; pc <= 4; pc++)
        {
            var readPerf = pc == 1
                ? Math.Round(95.0 + rng.NextDouble() * 5.0, 2)   // 95-100%
                : Math.Round(85.0 + rng.NextDouble() * 15.0, 2); // 85-100%

            ws.Cell(row, 1).Value = ssc;
            ws.Cell(row, 2).Value = period.Insert(3, "-");  // "Mar25" ? "Mar-25"
            ws.Cell(row, 3).Value = pc.ToString();
            ws.Cell(row, 4).Value = readPerf;
            ws.Cell(row, 5).Value = Math.Round(rng.NextDouble() * 10, 2);
            ws.Cell(row, 6).Value = rng.Next(1000, 50000);
            ws.Cell(row, 7).Value = rng.Next(100, 5000);
            ws.Cell(row, 8).Value = rng.Next(0, 500);
            ws.Cell(row, 9).Value = rng.Next(0, 200);
            ws.Cell(row, 10).Value = Math.Round(80 + rng.NextDouble() * 20, 2);
            ws.Cell(row, 11).Value = rng.Next(0, 100);
            ws.Cell(row, 12).Value = Math.Round(90 + rng.NextDouble() * 10, 2);
            ws.Cell(row, 13).Value = rng.Next(0, 10);
            ws.Cell(row, 14).Value = rng.Next(0, 50);
            ws.Cell(row, 15).Value = rng.Next(500, 10000);
            row++;
        }
    }

    ws.Columns().AdjustToContents();
    wb.SaveAs(path);
    Console.WriteLine($"  Created: {Path.GetFileName(path)} ({shippers.Length * 4} rows)");
}
"@ | Set-Content "$tempDir/Program.cs"

    # 3. Résoudre le chemin absolu du output
    $absOutput = (Resolve-Path $OutputDir).Path

    # 4. Exécuter
    Write-Host "Generating test Excel files..." -ForegroundColor Cyan
    Push-Location $tempDir
    dotnet run -- $Period $ShipperCount $absOutput
    Pop-Location

    Write-Host ""
    Write-Host "Files ready in $OutputDir/:" -ForegroundColor Green
    Get-ChildItem $OutputDir -Filter *.xlsx | ForEach-Object {
        Write-Host "  $($_.Name) ($([math]::Round($_.Length/1024, 1)) KB)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Verify in SFTP:  docker exec pafa_sftp ls /home/xoserve/upload" -ForegroundColor White
    Write-Host "  2. Rebuild image:   docker build -t pafa-batch:local -f src/PAFA.BatchReports/Dockerfile ." -ForegroundColor White
    Write-Host "  3. Deploy CronJob:  kubectl apply -f src/PAFA.BatchReports/cronjob-local.yaml" -ForegroundColor White
    Write-Host "  4. Watch logs:      kubectl logs -f -l app=pafa-batch" -ForegroundColor White
}
finally {
    # Cleanup temp project
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}
