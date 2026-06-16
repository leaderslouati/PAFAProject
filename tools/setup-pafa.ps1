# ═══════════════════════════════════════════════════════════════════════════════
# PAFA Setup Automation Script
# Purpose: Automate database migration, configuration checks, and initial setup
# Usage: ./setup-pafa.ps1 -Environment "Development"
# ═══════════════════════════════════════════════════════════════════════════════

param(
    [Parameter(Mandatory=$true)]
    [string]$Environment = "Development",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseServer = "localhost",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "pafa",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipMigration = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipValidation = $false
)

# Colors for output
$Colors = @{
    Success = "Green"
    Error = "Red"
    Warning = "Yellow"
    Info = "Cyan"
}

function Log {
    param(
        [string]$Message,
        [string]$Level = "Info"
    )
    
    $color = $Colors[$Level]
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# ═══════════════════════════════════════════════════════════════════════════════
# Step 1: Validate prerequisites
# ═══════════════════════════════════════════════════════════════════════════════

function Test-Prerequisites {
    Log "🔍 Testing prerequisites..." "Info"
    
    $prerequisites = @{
        "dotnet" = "dotnet --version"
        "dotnet-ef" = "dotnet ef --version"
        "git" = "git --version"
        "psql" = "psql --version"
    }
    
    $allOk = $true
    
    foreach ($tool in $prerequisites.Keys) {
        try {
            $output = Invoke-Expression $prerequisites[$tool] 2>&1
            Log "✓ $tool: $($output[0])" "Success"
        }
        catch {
            Log "✗ $tool: NOT FOUND" "Error"
            $allOk = $false
        }
    }
    
    if (-not $allOk) {
        Log "❌ Please install missing prerequisites and try again" "Error"
        exit 1
    }
    
    Log "✅ All prerequisites OK" "Success"
    return $true
}

# ═══════════════════════════════════════════════════════════════════════════════
# Step 2: Verify database connectivity
# ═══════════════════════════════════════════════════════════════════════════════

function Test-DatabaseConnection {
    param(
        [string]$Server,
        [string]$Database
    )
    
    Log "🔗 Testing database connection to $Server/$Database..." "Info"
    
    try {
        $connectionString = "Server=$Server; Database=$Database; Integrated Security=true;"
        $testQuery = "SELECT version();"
        
        # Test via psql
        $result = psql -h $Server -d $Database -c $testQuery 2>&1
        
        if ($result -like "*PostgreSQL*") {
            Log "✓ Database connection successful" "Success"
            return $true
        }
        else {
            Log "✗ Database connection failed" "Error"
            return $false
        }
    }
    catch {
        Log "✗ Database connection test failed: $_" "Error"
        return $false
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Step 3: Apply EF Core migrations
# ═══════════════════════════════════════════════════════════════════════════════

function Apply-Migrations {
    param(
        [string]$ProjectPath = "src/PAFA.Infrastructure",
        [string]$StartupProject = "src/PAFA.Api"
    )
    
    if ($SkipMigration) {
        Log "⏭️  Skipping migrations (--SkipMigration flag set)" "Warning"
        return $true
    }
    
    Log "📦 Applying EF Core migrations..." "Info"
    
    try {
        $env:ASPNETCORE_ENVIRONMENT = $Environment
        
        # Get list of pending migrations
        $pendingMigrations = dotnet ef migrations list --project $ProjectPath --startup-project $StartupProject 2>&1 | Where-Object { $_ -like "*Pending*" }
        
        if ($pendingMigrations.Count -gt 0) {
            Log "Found pending migrations:" "Info"
            foreach ($migration in $pendingMigrations) {
                Log "  - $migration" "Info"
            }
            
            # Apply migrations
            Log "Applying migrations..." "Info"
            $result = dotnet ef database update --project $ProjectPath --startup-project $StartupProject --verbose
            
            if ($LASTEXITCODE -eq 0) {
                Log "✅ Migrations applied successfully" "Success"
                return $true
            }
            else {
                Log "❌ Migration failed: $result" "Error"
                return $false
            }
        }
        else {
            Log "✓ No pending migrations" "Info"
            return $true
        }
    }
    catch {
        Log "❌ Error applying migrations: $_" "Error"
        return $false
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Step 4: Validate schema
# ═══════════════════════════════════════════════════════════════════════════════

function Validate-Schema {
    param(
        [string]$Server,
        [string]$Database
    )
    
    Log "📋 Validating database schema..." "Info"
    
    try {
        # Check tables
        $tables = @(
            "shippers",
            "product_classes",
            "ingestion_jobs",
            "ingestion_files",
            "metric_values",
            "reports",
            "report_types"
        )
        
        foreach ($table in $tables) {
            $query = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '$table';"
            $result = psql -h $Server -d $Database -t -c $query 2>&1
            
            if ([int]$result -gt 0) {
                Log "✓ Table '$table' exists" "Success"
            }
            else {
                Log "✗ Table '$table' NOT FOUND" "Error"
                return $false
            }
        }
        
        # Check views
        $views = @(
            "vw_dim_date",
            "vw_dim_shipper",
            "fact_read_performance",
            "v_parr_industry",
            "v_parr_pac",
            "vw_2a1_leaderboard",
            "vw_2a1_distribution",
            "vw_2a2_no_meter"
        )
        
        foreach ($view in $views) {
            $query = "SELECT COUNT(*) FROM information_schema.views WHERE table_schema = 'public' AND table_name = '$view';"
            $result = psql -h $Server -d $Database -t -c $query 2>&1
            
            if ([int]$result -gt 0) {
                Log "✓ View '$view' exists" "Success"
            }
            else {
                Log "✗ View '$view' NOT FOUND" "Error"
                return $false
            }
        }
        
        Log "✅ Schema validation successful" "Success"
        return $true
    }
    catch {
        Log "❌ Schema validation failed: $_" "Error"
        return $false
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Step 5: Verify seed data
# ═══════════════════════════════════════════════════════════════════════════════

function Verify-SeedData {
    param(
        [string]$Server,
        [string]$Database
    )
    
    Log "🌱 Verifying seed data..." "Info"
    
    try {
        # Check ReportTypes
        $query = "SELECT COUNT(*) FROM report_types WHERE code IN ('SCH2A', 'SCH2B');"
        $result = psql -h $Server -d $Database -t -c $query 2>&1
        
        if ([int]$result -eq 2) {
            Log "✓ ReportTypes seeded (SCH2A, SCH2B)" "Success"
        }
        else {
            Log "⚠️  ReportTypes not fully seeded" "Warning"
        }
        
        # Check ProductClasses
        $query = "SELECT COUNT(*) FROM product_classes WHERE code IN ('PC1', 'PC2', 'PC3', 'PC4');"
        $result = psql -h $Server -d $Database -t -c $query 2>&1
        
        if ([int]$result -eq 4) {
            Log "✓ ProductClasses seeded (PC1-PC4)" "Success"
        }
        else {
            Log "⚠️  ProductClasses not fully seeded" "Warning"
        }
        
        Log "✅ Seed data verification complete" "Success"
        return $true
    }
    catch {
        Log "❌ Seed data verification failed: $_" "Error"
        return $false
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Step 6: Check configuration
# ═══════════════════════════════════════════════════════════════════════════════

function Check-Configuration {
    param(
        [string]$ConfigFile = "src/PAFA.Api/appsettings.$Environment.json"
    )
    
    Log "⚙️  Checking configuration..." "Info"
    
    if (-not (Test-Path $ConfigFile)) {
        Log "⚠️  Config file not found: $ConfigFile" "Warning"
        Log "Create this file with Power BI and Azure Storage settings" "Info"
        return $false
    }
    
    try {
        $config = Get-Content $ConfigFile | ConvertFrom-Json
        
        $checks = @{
            "PowerBi.TenantId" = $config.PowerBi.TenantId
            "PowerBi.ClientId" = $config.PowerBi.ClientId
            "PowerBi.WorkspaceId" = $config.PowerBi.WorkspaceId
            "AzureStorage.ConnectionString" = $config.AzureStorage.ConnectionString
        }
        
        foreach ($key in $checks.Keys) {
            if ($checks[$key]) {
                Log "✓ $key is configured" "Success"
            }
            else {
                Log "⚠️  $key is NOT configured" "Warning"
            }
        }
        
        return $true
    }
    catch {
        Log "❌ Configuration check failed: $_" "Error"
        return $false
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Main execution
# ═══════════════════════════════════════════════════════════════════════════════

function Main {
    Log "╔════════════════════════════════════════════════════════════╗" "Info"
    Log "║  PAFA Setup Script                                         ║" "Info"
    Log "║  Environment: $Environment" "Info"
    Log "╚════════════════════════════════════════════════════════════╝" "Info"
    
    # Step 1
    if (-not (Test-Prerequisites)) {
        exit 1
    }
    
    # Step 2
    if (-not (Test-DatabaseConnection -Server $DatabaseServer -Database $DatabaseName)) {
        if (-not $SkipValidation) {
            Log "Database connection required. Aborting." "Error"
            exit 1
        }
        else {
            Log "Skipping database validation" "Warning"
        }
    }
    
    # Step 3
    if (-not (Apply-Migrations)) {
        Log "Migration failed. Aborting." "Error"
        exit 1
    }
    
    # Step 4 & 5 (if validation enabled)
    if (-not $SkipValidation) {
        if (-not (Validate-Schema -Server $DatabaseServer -Database $DatabaseName)) {
            Log "Schema validation failed" "Error"
        }
        
        if (-not (Verify-SeedData -Server $DatabaseServer -Database $DatabaseName)) {
            Log "Seed data verification failed" "Warning"
        }
    }
    
    # Step 6
    Check-Configuration
    
    Log "╔════════════════════════════════════════════════════════════╗" "Success"
    Log "║  ✅ Setup Complete!                                        ║" "Success"
    Log "║  Next steps:                                              ║" "Success"
    Log "║  1. Configure appsettings.$Environment.json               ║" "Success"
    Log "║  2. Create Power BI reports (2A & 2B)                    ║" "Success"
    Log "║  3. Configure Service Principal in Azure AD              ║" "Success"
    Log "║  4. Test export: dotnet run (from BatchReports)          ║" "Success"
    Log "╚════════════════════════════════════════════════════════════╝" "Success"
}

# Run
Main
