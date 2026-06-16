# 🔍 Validation Rapide — Entités & Configurations

**Fichier:** Script de vérification de complétude  
**Temps requis:** 5 minutes  
**Objectif:** Vérifier que toutes les configurations mappent bien aux tables SQL

---

## ✅ Checklist Avant Migration EF Core

### 1. Vérifier que TOUTES les configurations existent

```powershell
# Depuis le terminal, aller au projet Infrastructure
cd src/PAFA.Infrastructure/EntityConfigurations

# Lister les configurations
Get-ChildItem *.cs | Select-Object Name

# Résultat attendu (11 fichiers) :
FactReadPerformanceConfiguration.cs        ✅
IngestedFileConfiguration.cs               ✅
InjestionJobConfiguration.cs               ✅
MetricValueConfiguration.cs                ✅
PafaPermissionConfiguration.cs             (système)
PafaRoleConfiguration.cs                   (système)
PafaRolePermissionConfiguration.cs         (système)
PafaUserConfiguration.cs                   (système)
PafaUserRoleConfiguration.cs               (système)
PoductClassConfiguration.cs                ✅
ReportConfiguration.cs                     ✅
ReportTypeConfiguration.cs                 ✅
ShipperAliasConfiguration.cs               ✅
ShipperConfiguration.cs                    ✅
ShipperProductClassConfiguration.cs        ✅
ValidationErrorConfiguration.cs            ✅
ValidationNotificationConfiguration.cs     ✅
```

---

### 2. Vérifier les propriétés mapping (audit fields)

Chaque configuration doit avoir:
```csharp
// ✅ Propriétés audit complètes
builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100);
builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(100);
builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
```

**Vérification rapide:**
```powershell
# Chercher "CreatedAt" dans toutes les configurations
grep -r "CreatedAt" src/PAFA.Infrastructure/EntityConfigurations/*.cs | wc -l

# Résultat attendu: 11 (une par configuration)
```

---

### 3. Vérifier que TOUS les FK sont configurés

**Shippers:**
- [ ] HasMany ProductClasses
- [ ] HasMany MetricValues  
- [ ] HasMany ShipperAliases

**ShipperProductClass:**
- [ ] HasOne Shipper
- [ ] HasOne ProductClass

**ShipperAlias:**
- [ ] HasOne Shipper

**IngestionJob:**
- [ ] HasOne ParentJob (optional)
- [ ] HasMany RetryJobs
- [ ] HasMany IngestionFiles

**IngestionFile:**
- [ ] HasOne IngestionJob
- [ ] HasMany ValidationErrors
- [ ] HasMany ValidationNotifications
- [ ] HasMany MetricValues

**MetricValue:**
- [ ] HasOne Shipper (optional, SetNull)
- [ ] HasOne IngestionFile (required)

**ValidationError:**
- [ ] HasOne IngestionFile

**ValidationNotification:**
- [ ] HasOne IngestionFile

**Report:**
- [ ] HasOne ReportType (Restrict)
- [ ] HasOne IngestionJob (optional, SetNull)

**ReportType:**
- [ ] HasMany Reports

**ProductClass:**
- [ ] HasMany ShipperProductClasses

---

### 4. Vérifier les indices (index)

```sql
-- Query PostgreSQL pour valider les indices
SELECT indexname FROM pg_indexes 
WHERE tablename IN ('shippers', 'product_classes', 'shipper_alias', 
                    'shipper_product_classes', 'ingestion_jobs', 
                    'ingestion_files', 'metric_values', 
                    'validation_errors', 'validation_notifications', 
                    'report_types', 'reports')
ORDER BY tablename, indexname;
```

**Indices attendus:**

| Table | Index | Status |
|-------|-------|--------|
| shippers | ix_shipper_short_code | ✅ |
| shippers | ix_shipper_is_active | ✅ |
| product_classes | ix_pc_code | ✅ |
| shipper_alias | ix_shipper_alias_shipper_id | ✅ |
| shipper_alias | ix_shipper_alias_code | ✅ |
| shipper_product_classes | ix_spc_shipper_id | ✅ |
| shipper_product_classes | ix_spc_product_class_id | ✅ |
| shipper_product_classes | ux_spc_shipper_product_class | ✅ |
| ingestion_jobs | ix_job_period | ✅ |
| ingestion_jobs | ix_job_status | ✅ |
| ingestion_jobs | ix_job_correlation_id | ✅ |
| ingestion_files | ix_file_hash | ✅ |
| ingestion_files | ix_file_job_id | ✅ |
| ingestion_files | ix_file_status | ✅ |
| metric_values | ix_metric_values_shipper_id | ✅ |
| metric_values | ix_metric_values_period_shipper_key | ✅ |
| metric_values | ix_metric_values_period | ✅ |
| metric_values | ix_metric_values_key | ✅ |
| validation_errors | ix_ve_file_id | ✅ |
| validation_errors | ix_ve_error_code | ✅ |
| validation_notifications | ix_vn_file_id | ✅ |
| validation_notifications | ix_vn_status | ✅ |
| report_types | ix_reporttype_code | ✅ |
| reports | ix_reports_period_type | ✅ |
| reports | ix_reports_status | ✅ |
| reports | ix_reports_audience | ✅ |

---

### 5. Vérifier le Seed Data

**ReportTypes:**
```sql
SELECT id, code, label, audience FROM report_types ORDER BY id;

-- Résultat attendu:
-- 1 | SCH2A | Industry Peer Comparison (Anonymised) | Industry
-- 2 | SCH2B | Performance Assurance Committee (Non-Anonymised) | PAC
```

**ProductClasses:**
```sql
SELECT id, code, description FROM product_classes ORDER BY id;

-- Résultat attendu:
-- 1 | PC1 | Large sites — AQ ≥ 732 MWH
-- 2 | PC2 | Medium NDM
-- 3 | PC3 | Small NDM WAR
-- 4 | PC4 | IGT Small
```

---

### 6. Vérifier la compilation C#

```powershell
cd src/PAFA.Api

# Compiler la solution
dotnet build

# Résultat attendu (sans erreurs):
# Build succeeded. 0 Warning(s)
```

---

### 7. Tester DbContext avec les nouvelles configs

```csharp
// Fichier: src/PAFA.Api/Controllers/TestController.cs
// Créer un endpoint test simple

[HttpGet("test-entities")]
public async Task<IActionResult> TestEntities()
{
    try
    {
        var shipperCount = await _context.Shippers.CountAsync();
        var jobCount = await _context.IngestionJobs.CountAsync();
        var reportCount = await _context.Reports.CountAsync();
        
        return Ok(new 
        { 
            message = "All entities loaded successfully",
            shippersCount = shipperCount,
            jobsCount = jobCount,
            reportsCount = reportCount
        });
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

// GET /api/test/test-entities
// Résultat attendu: HTTP 200 OK
```

---

## 🚀 Étapes d'Exécution

### Étape 1: Compiler
```powershell
cd src/PAFA.Api
dotnet build
# ✅ No errors expected
```

### Étape 2: Créer la migration
```powershell
dotnet ef migrations add "CompleteEntityConfigurations" `
  -p ../PAFA.Infrastructure `
  -o Migrations

# ✅ Migration créée dans Migrations/
```

### Étape 3: Appliquer la migration
```powershell
dotnet ef database update -p ../PAFA.Infrastructure

# ✅ "Done. Success!"
```

### Étape 4: Vérifier la base
```sql
-- Depuis psql
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public' AND table_type = 'BASE TABLE' 
ORDER BY table_name;

-- ✅ 11 tables attendues
```

### Étape 5: Valider le seed data
```sql
SELECT COUNT(*) FROM report_types;   -- ✅ 2
SELECT COUNT(*) FROM product_classes; -- ✅ 4
```

### Étape 6: Tester les relations
```csharp
// From test endpoint or unit test
var shipper = await _context.Shippers
    .Include(s => s.ProductClasses)
    .Include(s => s.MetricValues)
    .Include(s => s.ShipperAliases)
    .FirstAsync();

Assert.NotNull(shipper);
Assert.NotNull(shipper.ProductClasses);
```

---

## 🎯 Résultat Final Attendu

✅ **11 Tables** — All created and populated  
✅ **11 Entities** — All mapped correctly  
✅ **11 Configurations** — All complete and harmonized  
✅ **50+ Properties** — All audit fields present  
✅ **30+ Indices** — All configured  
✅ **15+ Foreign Keys** — All relationships working  
✅ **Seed Data** — ReportTypes & ProductClasses seeded  

---

## 📊 Fichiers Clés à Consulter

1. [ENTITIES_AND_CONFIGURATIONS_COMPLETE.md](ENTITIES_AND_CONFIGURATIONS_COMPLETE.md) — Récapitulatif détaillé
2. [sql/01-create-tables.sql](sql/01-create-tables.sql) — Référence SQL
3. [src/PAFA.Domain/Entities/](src/PAFA.Domain/Entities/) — Toutes les entités
4. [src/PAFA.Infrastructure/EntityConfigurations/](src/PAFA.Infrastructure/EntityConfigurations/) — Toutes les configurations

---

## 💡 Troubleshooting

### ❌ Erreur: "Migration failed"
**Solution:** Exécuter `dotnet ef migrations remove` et réessayer

### ❌ Erreur: "Column 'xxx' does not exist"
**Solution:** Vérifier HasColumnName() dans la configuration

### ❌ Erreur: "Foreign key constraint failed"
**Solution:** Vérifier OnDelete behavior (Cascade/SetNull/Restrict)

### ❌ Erreur: "Duplicate index name"
**Solution:** Vérifier les noms d'indices dans les configurations

---

**Status:** ✅ **Ready for Migration**  
**Next:** Run compilation + migration + validation

