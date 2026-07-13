param([string]$SourceDir = "Files\Source Files")

Set-Location "c:\Users\hlouati\Desktop\PAFAProject"

$allFiles = Get-ChildItem $SourceDir -Filter "*.xlsx" | Where-Object { $_.Name -notlike "Anony*" }

foreach ($f in $allFiles) {
    try {
        $z = [System.IO.Compression.ZipFile]::OpenRead($f.FullName)
        
        # Sheet names
        $wbEntry = ($z.Entries | Where-Object { $_.FullName -eq "xl/workbook.xml" })[0]
        $wbStream = $wbEntry.Open()
        [xml]$wbXml = (New-Object System.IO.StreamReader($wbStream)).ReadToEnd()
        $wbStream.Dispose()
        $sheets = $wbXml.workbook.sheets.sheet | ForEach-Object { $_.name }
        
        # First 20 shared strings (column headers likely)
        $ssEntry = ($z.Entries | Where-Object { $_.FullName -eq "xl/sharedStrings.xml" })[0]
        $headers = @()
        if ($ssEntry) {
            $ssStream = $ssEntry.Open()
            [xml]$ssXml = (New-Object System.IO.StreamReader($ssStream)).ReadToEnd()
            $ssStream.Dispose()
            $headers = $ssXml.sst.si | ForEach-Object { 
                if ($_.t) { $_.t }
                elseif ($_.r) { ($_.r | ForEach-Object { $_.t }) -join "" }
            } | Select-Object -First 30
        }
        
        $z.Dispose()
        
        Write-Host "=== $($f.Name) ===" -ForegroundColor Cyan
        Write-Host "  Sheets: $($sheets -join ' | ')" -ForegroundColor Yellow
        Write-Host "  First strings: $($headers -join ' | ')" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host "ERROR reading $($f.Name): $_" -ForegroundColor Red
    }
}
