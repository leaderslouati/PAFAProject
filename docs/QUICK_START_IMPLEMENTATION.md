# 🚀 Quick Start: Mise en œuvre de l'Export XLSX/PPTX via Power BI

## 📋 Étapes d'exécution (Day 1)

### 1️⃣ Appliquer les migrations EF Core (5 min)

```powershell
# Depuis la racine du repository
cd c:\Users\hlouati\Desktop\PAFAProject

# Installer dotnet-ef si nécessaire
dotnet tool install --global dotnet-ef

# Appliquer la migration
dotnet ef database update --project src/PAFA.Infrastructure --startup-project src/PAFA.Api
```

**Résultat attendu:**
```
Build started...
Build completed.
Executing migrations...
Applying migration 'AddReportingAndExportTables'...
Done. Success!
```

---

### 2️⃣ Vérifier les tables & vues SQL (5 min)

**Depuis pgAdmin ou psql:**

```sql
-- Vérifier les tables
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name IN ('report_types', 'reports', 'ingestion_jobs');

-- Vérifier les vues
SELECT table_name FROM information_schema.views 
WHERE table_schema = 'public' 
AND table_name LIKE 'vw_%' OR table_name LIKE 'v_%' OR table_name LIKE 'fact_%';

-- Vérifier les seed data
SELECT * FROM report_types;
```

**Expected output:**
```
 id |  code |              label              | audience | report_count
----+-------+---------------------------------+----------+--------------
  1 | SCH2A | Industry Peer Comparison...    | Industry |           19
  2 | SCH2B | Performance Assurance Committee | PAC      |           22
```

---

### 3️⃣ Ajouter les fichiers C# (PowerBiExportService) (30 min)

**Créer le service:**
```powershell
# Copier le code du PowerBiExportService depuis docs/EXPORT_REPORTS_COMPLETE_GUIDE.md
# vers src/PAFA.Api/Services/PowerBiExportService.cs
```

**Ajouter les NuGet packages:**
```powershell
dotnet add package Microsoft.PowerBI.Api --version 1.47.0
dotnet add package Azure.Identity --version 1.13.0
dotnet add package Azure.Storage.Blobs --version 12.19.0
dotnet add package Microsoft.Identity.Client --version 4.60.0
```

**Configurer Program.cs:**
```csharp
// Ajouter dans Program.cs
builder.Services.AddScoped<IPowerBiExportService, PowerBiExportService>();

// Azure Blob client
var blobUri = new Uri($"https://{storageAccountName}.blob.core.windows.net/exports");
var blobContainerClient = new BlobContainerClient(blobUri, new DefaultAzureCredential());
builder.Services.AddSingleton(blobContainerClient);
```

---

### 4️⃣ Configurer appsettings.json (10 min)

**Ajouter les paramètres Power BI:**
```json
{
  "PowerBi": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientSecret": "YOUR_SECRET_HERE",
    "WorkspaceId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Report2AId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Report2BId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Dataset2AId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "Dataset2BId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  },
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
    "ContainerName": "exports",
    "AccountName": "your-storage-account"
  }
}
```

---

### 5️⃣ Créer les rapports Power BI (60 min)

**Dans Power BI Desktop:**

1. **Créer 2A_Industry_Comparison.pbix:**
   - Importer `fact_read_performance`, `vw_dim_date`, `v_parr_industry`
   - Créer les visuals pour Schedule 2A (19 slides)
   - Appliquer les mesures DAX depuis `docs/powerbi/DAX_MEASURES.md`

2. **Créer 2B_PAC_Performance.pbix:**
   - Importer `fact_read_performance`, `vw_dim_date`, `v_parr_pac`
   - Implémenter RLS (Row-Level Security) pour les shippers
   - Créer les visuals pour Schedule 2B (22 slides)

3. **Publier à Power BI Service:**
   ```
   File > Publish > Workspace: "PAFA-Reports"
   ```

4. **Récupérer les IDs:**
   - URL: `app.powerbi.com/groups/{workspace-id}/reports/{report-id}`
   - Copier Workspace ID et Report IDs dans `appsettings.json`

---

### 6️⃣ Configurer Service Principal (30 min)

**Créer l'application Azure AD:**
```powershell
# PowerShell
Connect-AzureAD
$app = New-AzureADApplication -DisplayName "PAFA-PowerBI-Exporter"
$secret = New-AzureADApplicationPasswordCredential -ObjectId $app.ObjectId

# Sauvegarder ces valeurs:
# - App ID: $app.AppId
# - Tenant ID: (vérifier dans Azure AD portal)
# - Secret: $secret.Value
```

**Donner les permissions:**
1. Aller à **Power BI Admin Portal** > **Tenant Settings**
2. Activer: "Allow service principals to use read-only Power BI admin APIs"
3. Aller à **Workspaces** > **PAFA-Reports** > **Access**
4. Ajouter le Service Principal avec rôle **Admin**

---

### 7️⃣ Tester l'export (20 min)

**Via Postman ou C# console:**

```powershell
# Option 1: Exécuter le batch job
cd src/PAFA.BatchReports
dotnet run

# Logs attendus:
# [INFO] Refreshing dataset 2A...
# [INFO] Exporting report 2A to PPTX...
# [INFO] Uploading to blob...
# [INFO] Generated SAS URL...
```

**Vérifier les résultats:**
```sql
SELECT id, reporting_period, status, file_path_pptx, published_at 
FROM reports 
WHERE reporting_period = CURRENT_DATE 
ORDER BY published_at DESC;
```

---

## 📁 Fichiers créés/modifiés

| Fichier | Description |
|---------|-------------|
| `sql/01-create-tables.sql` | ✅ Créé — Schéma PostgreSQL |
| `sql/02-create-views-powerbi.sql` | ✅ Créé — 8 vues optimisées |
| `src/PAFA.Infrastructure/Migrations/20260614000000_AddReportingAndExportTables.cs` | ✅ Créé — Migration EF Core |
| `src/PAFA.Api/Services/PowerBiExportService.cs` | À créer — Service export |
| `docs/EXPORT_REPORTS_COMPLETE_GUIDE.md` | ✅ Créé — Guide complet |
| `appsettings.json` | À mettre à jour — Secrets & config |
| `src/PAFA.Api/Program.cs` | À mettre à jour — DI setup |

---

## 🔍 Vérification finale

```powershell
# 1. Migration appliquée
dotnet ef migrations list --project src/PAFA.Infrastructure

# 2. Vues créées
psql -d pafa -c "SELECT COUNT(*) FROM information_schema.views WHERE table_schema = 'public';"

# 3. Service Principal fonctionnel
# Exécuter dotnet run et vérifier les logs

# 4. Blobs uploadés
az storage blob list --container-name exports --account-name pafast

# 5. Base de données mise à jour
psql -d pafa -c "SELECT COUNT(*) FROM reports;"
```

---

## ⚠️ Points critiques

- **Secrets:** Utiliser Azure Key Vault en production (pas en appsettings)
- **Permissions:** Service Principal doit avoir **Admin** access au workspace
- **Tenant settings:** Vérifier que "Allow service principals..." est activé
- **SAS token:** Générer avec expiration appropriée (30 jours par défaut)
- **Dataset refresh:** Prendre ~5 min avant d'exporter (attendre le refresh)

---

## 📞 Support

- Migration EF Core: `dotnet ef database update --verbose`
- Power BI API: https://learn.microsoft.com/en-us/rest/api/power-bi/
- Azure Storage: https://learn.microsoft.com/en-us/azure/storage/blobs/

---

**✅ Résumé:**
- Étapes 1-2: 10 min (migrations SQL)
- Étapes 3-4: 40 min (configuration C#)
- Étapes 5-6: 90 min (rapports Power BI + Service Principal)
- Étape 7: 20 min (test)
- **Total: 2-3 heures**

