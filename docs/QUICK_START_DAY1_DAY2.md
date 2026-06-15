# ⚡ QUICK START — PAFA Reports 2A & 2B (Jour 1-2)

## 🎯 Objectif Jour 1-2
Valider données source → Créer vues SQL → Tester Power BI import

---

## ✅ JOUR 1 — VALIDATION DES DONNÉES

### ⏰ Durée: 2-3 heures

### STEP 1.1: Analyser la structure des fichiers XLS

**Actions:**
1. Ouvrir les deux fichiers source:
   - `MOD520A__PAF_Reports_Apr26_Anonymised.xlsx`
   - `MOD520A__PAF_Reports_Apr26_Non Anonymised.xlsx`

2. Créer **Document d'Analyse** avec:
   ```
   📊 Fichier: MOD520A_Anonymised
   ✅ Feuilles: [liste les noms]
   ✅ Colonnes: [liste les en-têtes]
   ✅ Lignes de données: [count]
   ✅ Types: Numérique, Texte, Date, %
   ✅ Valeurs manquantes: [None/Some/Many]
   ```

3. Comparer avec CSV test: `PARR_2025_03_data.csv`
   - Mêmes colonnes ?
   - Mêmes shippers ?
   - Même format de données ?

**Expected Output:**
```
ANONYMISED (2A):
- Shipper Alias Code (anonyme) — e.g., "A001", "B005"
- Reporting Period — "2025-04"
- Product Class — "1", "2", "3", "4"
- Read Performance % — 89-99
- ... 20+ métriques

NON-ANONYMISED (2B):
- Shipper Short Code — "SSE", "BGT", "OVO"
- Shipper Full Name — "SSE Energy Solutions", ...
- Reporting Period — "2025-04"
- Product Class — "1", "2", "3", "4"
- Annual Quantity — 1000-50000
- ... 20+ métriques
```

### STEP 1.2: Vérifier les données en base

**SQL Query à exécuter:**

```sql
-- Check current data in PostgreSQL
SELECT COUNT(*) AS metric_count
FROM metric_values;

SELECT DISTINCT "ReportingPeriod" 
FROM metric_values 
ORDER BY "ReportingPeriod" DESC 
LIMIT 5;

SELECT COUNT(DISTINCT "ShipperShortCode") AS shipper_count
FROM metric_values;

SELECT DISTINCT "ProductClassCode" 
FROM metric_values;

-- Check shippers
SELECT short_code, name, is_active 
FROM shippers 
LIMIT 20;
```

**Expected Results:**
- ✅ 100K - 1M metric values rows
- ✅ Dates: 2025-03, 2025-04, 2025-05
- ✅ Shippers: 5-50+ codes (SSE, BGT, OVO, EON, NPW, ...)
- ✅ Product Classes: PC1, PC2, PC3, PC4

---

### STEP 1.3: Document Findings

**Create:** `docs/DATA_VALIDATION_REPORT.md`

```markdown
# Data Validation Report — Date: 2025-06-11

## Source Files
- ✅ Anonymised: 65,432 rows
- ✅ Non-Anonymised: 65,432 rows
- ✅ Columns match: YES

## Database Status
- ✅ metric_values: 1,245,678 rows
- ✅ Periods: Mar-25 to Apr-26
- ✅ Shippers: 47 active
- ✅ Classes: PC1-PC4

## Issues Found
- [ ] None
- [x] Data quality score column missing in 2B file
- [x] Some shipper aliases not in database

## Action Items
- [ ] Load data from Anonymised file
- [ ] Load data from Non-Anonymised file
- [ ] Validate shipper aliases
```

**Status:** ✅ JOUR 1 COMPLET

---

## ✅ JOUR 2 — CRÉER VUES SQL

### ⏰ Durée: 3-4 heures

### STEP 2.1: Ouvrir PostgreSQL

```bash
# Option 1: If Docker (local dev)
docker exec -it pafa-postgres psql -U pafa_user -d pafa_db

# Option 2: Direct connection
psql -h localhost -U postgres -d pafa_db
```

### STEP 2.2: Exécuter script de création des vues

**File:** `docs/powerbi/SQL_VIEWS_CREATION.md`

```sql
-- Copy each VIEW from SQL_VIEWS_CREATION.md
-- Paste in PostgreSQL console

-- ✅ 1. Create vw_dim_date
CREATE OR REPLACE VIEW vw_dim_date AS
SELECT
    DISTINCT
    m."ReportingPeriod" AS date_id,
    ...
FROM metric_values m
ORDER BY m."ReportingPeriod" DESC;

-- ✅ 2. Create vw_2a1_leaderboard
CREATE OR REPLACE VIEW vw_2a1_leaderboard AS
...

-- ✅ 3. Create vw_2a1_distribution
CREATE OR REPLACE VIEW vw_2a1_distribution AS
...

-- ✅ 4. Create vw_2a2_no_meter
CREATE OR REPLACE VIEW vw_2a2_no_meter AS
...
```

### STEP 2.3: Vérifier les vues

```sql
-- List all views
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_type = 'VIEW'
AND (table_name LIKE 'vw_%' OR table_name LIKE 'v_%')
ORDER BY table_name;

-- Expected output (8 views):
-- fact_read_performance
-- v_parr_industry
-- v_parr_pac
-- vw_2a1_distribution
-- vw_2a1_leaderboard
-- vw_2a2_no_meter
-- vw_dim_date
-- vw_dim_shipper

-- Test each view
SELECT * FROM vw_dim_date LIMIT 5;
SELECT * FROM vw_2a1_leaderboard LIMIT 10;
SELECT * FROM vw_2a1_distribution LIMIT 5;
SELECT * FROM vw_2a2_no_meter LIMIT 5;
```

### STEP 2.4: Performance Testing

```sql
-- Test 1: vw_dim_date (should be instant)
\timing on
SELECT * FROM vw_dim_date;
-- Expected: < 100ms

-- Test 2: v_parr_industry (50K+ rows)
EXPLAIN ANALYZE
SELECT * FROM v_parr_industry LIMIT 1000;
-- Expected: < 500ms

-- Test 3: vw_2a1_leaderboard
EXPLAIN ANALYZE
SELECT * FROM vw_2a1_leaderboard 
WHERE report_date = '2025-04-30';
-- Expected: < 1000ms
```

**✅ If all queries are fast:** PROCEED TO DAY 3

**❌ If queries are slow:**
```sql
-- Create indexes for optimization
CREATE INDEX idx_mv_period ON metric_values("ReportingPeriod");
CREATE INDEX idx_mv_key ON metric_values("MetricKey");
CREATE INDEX idx_mv_shipper ON metric_values("ShipperShortCode");
```

**Status:** ✅ JOUR 2 COMPLET

---

## ⏳ JOUR 3 — POWER BI DESKTOP SETUP (Optional - préparation)

### STEP 3.1: Télécharger Power BI Desktop

- Accéder: https://powerbi.microsoft.com/desktop
- Installer version 64-bit
- License: Pro ou Premium

### STEP 3.2: Créer nouveau fichier PBIX

1. Ouvrir Power BI Desktop
2. **New Report** → Blank Report
3. Enregistrer as: `PAFA_Reports_2A_2B.pbix`

### STEP 3.3: Importer vues PostgreSQL

**Menu:** Home → Get data → PostgreSQL

```
Server: localhost  (ou votre URL)
Database: pafa_db
Username: pafa_user
Password: [encrypted]
```

**Select tables:**
- ✅ vw_dim_shipper
- ✅ vw_dim_date
- ✅ v_parr_industry (for 2A)
- ✅ v_parr_pac (for 2B)
- ✅ fact_read_performance
- ✅ vw_2a1_leaderboard
- ✅ vw_2a1_distribution
- ✅ vw_2a2_no_meter

**Mode:** DirectQuery or Import?
- For now: **Import** (1-2 GB data, refresh daily)

### STEP 3.4: Valider les données en Power BI

**Onglet:** Data View
- ✅ Vérifier colonnes
- ✅ Vérifier types (% formatted correctly)
- ✅ Vérifier sample data

**Onglet:** Model View
- ✅ Create relationships (shipper ← → fact)
- ✅ Create relationships (date ← → fact)
- ✅ Mark vw_dim_date as **Date Table**

**Save:** Ctrl+S

**Status:** ⏳ À FAIRE (optionnel avant fin semaine)

---

## 📋 CHECKLIST SEMAINE 1

```
Jour 1:
  ☐ Analyser fichiers XLS source
  ☐ Valider données en PostgreSQL
  ☐ Créer DATA_VALIDATION_REPORT.md
  
Jour 2:
  ☐ Créer 4 nouvelles vues SQL
  ☐ Tester performance
  ☐ Vérifier données
  ☐ Créer indexes si nécessaire
  
Jour 3:
  ☐ Power BI Desktop setup (optionnel)
  ☐ Importer vues PostgreSQL
  ☐ Valider données en PBI
  ☐ Créer relationships
  
Fin Semaine 1:
  ☐ Documentation: IMPLEMENTATION_PLAN_2A_2B.md ✅
  ☐ Documentation: SQL_VIEWS_CREATION.md ✅
  ☐ Documentation: DAX_MEASURES.md ✅
  ☐ Documentation: API_GUIDE.md ✅
  ☐ SQL Views: 4/4 créées
  ☐ PowerBI Desktop: 1/1 fichier prêt
  ☐ Data validation: ✅ Complète
```

---

## 🚀 SEMAINE 2+ ROADMAP

### Semaine 2: Power BI Model & DAX Measures
- [ ] Importer 20+ mesures DAX
- [ ] Tester chaque mesure
- [ ] Créer KPI cards
- [ ] Build first visuals

### Semaine 3: Design Reports 2A & 2B
- [ ] Create Report 2A pages (5 pages)
- [ ] Create Report 2B pages (5 pages)
- [ ] Add drill-through
- [ ] Format colors & fonts

### Semaine 4: Dashboard PPTX
- [ ] Report Builder setup
- [ ] Create PPTX template
- [ ] Add executive summary
- [ ] Add charts & tables

### Semaine 5: Publish to Power BI Service
- [ ] Create Premium workspace
- [ ] Upload PBIX files
- [ ] Configure RLS roles
- [ ] Schedule daily refresh

### Semaine 6-7: API & Blob Storage
- [ ] Implement 8 API endpoints
- [ ] Configure Azure Blob storage
- [ ] Generate SAS URLs
- [ ] Test downloads

### Semaine 8-9: Testing & UAT
- [ ] Performance testing
- [ ] Security audit
- [ ] User acceptance testing
- [ ] Bug fixes

### Semaine 10: Go-Live
- [ ] Final deployment
- [ ] User training
- [ ] Documentation handover
- [ ] Production monitoring

---

## 📞 CONTACTS & RESSOURCES

| Rôle | Contact | Disponibilité |
|------|---------|---------------|
| **Tech Lead** | [Your Name] | 24/7 |
| **Database Team** | [DBA Email] | 09:00-17:00 |
| **Power BI Expert** | [PBI Email] | 09:00-17:00 |
| **API Developer** | [Dev Email] | 09:00-17:00 |

---

## 📚 DOCUMENTATION CLÉS

| Document | Status | Link |
|----------|--------|------|
| Plan d'implémentation | ✅ | [IMPLEMENTATION_PLAN_2A_2B.md](IMPLEMENTATION_PLAN_2A_2B.md) |
| Vues SQL | ✅ | [SQL_VIEWS_CREATION.md](powerbi/SQL_VIEWS_CREATION.md) |
| DAX Measures | ✅ | [DAX_MEASURES.md](powerbi/DAX_MEASURES.md) |
| API Guide | ✅ | [API_GUIDE.md](API_GUIDE.md) |
| Data Validation | ⏳ | [DATA_VALIDATION_REPORT.md](DATA_VALIDATION_REPORT.md) |
| User Guide | ⏳ | [USER_GUIDE.md](USER_GUIDE.md) |
| Troubleshooting | ⏳ | [TROUBLESHOOTING.md](TROUBLESHOOTING.md) |

---

## 🎯 SUCCESS CRITERIA

By end of Week 1:
- [ ] 4 SQL views created & tested
- [ ] 1 Power BI Desktop file with data imported
- [ ] 20+ DAX measures ready
- [ ] 8 API endpoints designed
- [ ] Documentation complete

By end of Week 2:
- [ ] Power BI model finalized
- [ ] Reports 2A & 2B designed
- [ ] Dashboard PPTX template created

By end of Week 4:
- [ ] All published to Power BI Service
- [ ] APIs implemented & tested
- [ ] UAT passed

By end of Week 5:
- [ ] **Production Go-Live** 🚀

---

**Prêt ? Commencez par:** `STEP 1.1 — Analyser les fichiers XLS`

