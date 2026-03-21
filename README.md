# ?? PAFA Project - Performance Assurance Framework Application

## ?? Vue d'ensemble

Application de gestion des rapports PARR (Performance Assurance Reporting Regime) pour le secteur gazier UK.

**Architecture** : Clean Architecture + CQRS + MediatR + Docker  
**Stack** : .NET 9, PostgreSQL 16, RabbitMQ, SFTP  
**Statut** : POC opérationnel à 78% (3/4 flux complets)

---

## ?? DÉMARRAGE RAPIDE (5 MINUTES)

### **Prérequis**

- ? Docker Desktop installé et démarré
- ? .NET 9 SDK installé
- ? PowerShell 5.1+ (Windows) ou PowerShell Core (Linux/Mac)

### **Installation**

```powershell
# 1. Cloner le repository
git clone https://github.com/leaderslouati/PAFAProject
cd PAFAProject

# 2. Initialiser Docker (AUTOMATIQUE)
.\fix-docker-env.ps1

# 3. Appliquer les migrations
cd src\PAFA.Api
dotnet ef database update --project ..\PAFA.Infrastructure

# 4. Démarrer l'API
dotnet run
```

**? Votre API est maintenant accessible sur** : http://localhost:5000/swagger

---

## ?? DÉPANNAGE

### ? **Erreur : "PostgreSQL ne répond pas"**

**Solution immédiate** :
```powershell
.\fix-docker-env.ps1
```

**Voir le guide complet** : [SOLUTION_EXPRESS.md](SOLUTION_EXPRESS.md)

---

### ? **Erreur : "Port 5432 already in use"**

**Solution** : PostgreSQL local tourne déjà
```powershell
Stop-Service postgresql-x64-15
# OU changez le port dans docker-compose.psql.dev.yml ? 5433:5432
```

---

### ? **Erreur : "docker network create failed"**

**Solution** :
```powershell
docker network rm pafa_network
docker network create pafa_network
docker-compose up -d
```

---

## ?? STRUCTURE DU PROJET

```
PAFAProject/
??? src/
?   ??? PAFA.Domain/          # Entités + Interfaces (contrats)
?   ??? PAFA.Infrastructure/  # Implémentations (EF Core, SFTP, Parsing)
?   ??? PAFA.Extraction/      # CQRS Handlers (orchestration métier)
?   ??? PAFA.Reports/         # Export & Dashboard
?   ??? PAFA.Api/             # API REST + BackgroundService
?   ??? PAFA.BatchReports/    # Service batch autonome
??? xoserve/                  # Dossiers SFTP locaux
?   ??? upload/               # Fichiers nouveaux
?   ??? processed/            # Fichiers traités
?   ??? failed/               # Fichiers en erreur
??? docker-compose.yml        # Services Docker
??? Scripts PowerShell        # Automation
```

---

## ?? FLUX PRINCIPAUX

### **1. SFTP ? Import ? PostgreSQL (100% ?)**

```
Xoserve SFTP ? Download ? Parsing ? Validation ? Mapping EAV ? PostgreSQL
```

**Endpoints** :
- `POST /api/sftp/ingest?year=2025&month=2` (manuel)
- `MonthlyIngestionService` (auto - 02:00 UTC)

---

### **2. Validation API (100% ?)**

```
ImportValidationService ? ValidationErrors (BDD) ? API REST
```

**Endpoints** :
- `GET /api/validation/{fileId}` (détails erreurs fichier)
- `GET /api/validation/job/{jobId}` (résumé job)

---

### **3. Export Power BI + Dashboard (100% ?)**

```
PostgreSQL ? Query Handlers ? Writers (CSV/Excel/PDF) ? API REST
```

**Endpoints** :
- `GET /api/reports/powerbi` (CSV Power BI)
- `GET /api/reports/export/excel` (Excel)
- `GET /api/reports/export/pdf` (PDF)
- `GET /api/dashboard/summary` (KPIs)

---

### **4. Batch Reports Schedule 2A/2B (50% ??)**

```
BatchReportOrchestrator ? ExcelGenerator ? PdfGenerator ? /app/output/reports
```

**Statut** : Structure présente, templates métier manquants (5-8 jours de dev)

---

## ?? ENDPOINTS API

| Méthode | Route | Description | Statut |
|---------|-------|-------------|--------|
| POST | `/api/sftp/ingest` | Import SFTP | ? |
| POST | `/api/import/upload` | Upload Web | ? |
| GET | `/api/validation/{fileId}` | Erreurs validation | ? |
| GET | `/api/validation/job/{jobId}` | Résumé job | ? |
| GET | `/api/dashboard/summary` | Dashboard KPIs | ? |
| GET | `/api/reports/powerbi` | Export CSV | ? |
| GET | `/api/reports/export/excel` | Export Excel | ? |
| GET | `/api/reports/export/pdf` | Export PDF | ? |
| POST | `/api/batch/trigger` | Batch reports | ?? Stub |

---

## ?? SERVICES DOCKER

| Service | Port | Statut | Accès |
|---------|------|--------|-------|
| PostgreSQL 16 | 5432 | ? | `psql -h localhost -U postgres -d pafadb` |
| SFTP (atmoz) | 2222 | ? | `sftp -P 2222 xoserve@localhost` |
| RabbitMQ | 5672, 15672 | ? | http://localhost:15672 (guest/guest) |
| Batch Reports | - | ?? | `docker exec pafa_batch_reports ...` |

---

## ?? TESTER LE FLUX COMPLET

### **Test automatique (recommandé)**

```powershell
.\test-sftp-flow.ps1
```

**Résultat attendu** :
```
? FLUX COMPLET VALIDÉ : SFTP ? Import ? PostgreSQL ? Export
```

---

### **Test manuel (Swagger)**

1. Ouvrir http://localhost:5000/swagger
2. Déposer un fichier : `copy test.xlsx xoserve\upload\`
3. Exécuter : `POST /api/sftp/ingest?year=2025&month=2`
4. Vérifier : `GET /api/validation/{fileId}`
5. Dashboard : `GET /api/dashboard/summary`
6. Export : `GET /api/reports/powerbi`

---

## ?? ÉTAT D'AVANCEMENT

| Flux | Câblage | Fonctionnel | Production |
|------|---------|-------------|------------|
| SFTP ? Import | 100% ? | 95% ? | ?? POC |
| Validation API | 100% ? | 90% ?? | ?? Test |
| Export Power BI | 100% ? | 95% ? | ?? POC |
| Batch Reports | 50% ?? | 30% ? | ? Non |

**Score global** : **86% câblé** | **78% fonctionnel**

---

## ?? DOCUMENTATION

| Document | Description |
|----------|-------------|
| [SOLUTION_EXPRESS.md](SOLUTION_EXPRESS.md) | ? Solution 2 minutes pour erreur PostgreSQL |
| [GUIDE_DEPANNAGE_POSTGRES.md](GUIDE_DEPANNAGE_POSTGRES.md) | ?? Guide complet dépannage |
| [RAPPORT_FINAL_CABLAGE.md](RAPPORT_FINAL_CABLAGE.md) | ?? Analyse exhaustive du câblage |
| [MAPPING_DIAGRAMME_CODE.md](MAPPING_DIAGRAMME_CODE.md) | ??? Mapping diagramme ? code |
| [REPONSE_DIRECTE.md](REPONSE_DIRECTE.md) | ?? Réponse concise aux questions |

---

## ??? SCRIPTS UTILES

| Script | Usage |
|--------|-------|
| `fix-docker-env.ps1` | Réparation automatique Docker (30s) |
| `init-docker-env.ps1` | Initialisation complète (1 min) |
| `diagnose-docker.ps1` | Diagnostic détaillé environnement |
| `test-sftp-flow.ps1` | Test end-to-end complet (2 min) |

---

## ?? CREDENTIALS PAR DÉFAUT

### **PostgreSQL**
- Host: `localhost:5432`
- User: `postgres`
- Password: `postgres`
- Database: `pafadb`

### **SFTP**
- Host: `localhost:2222`
- User: `xoserve`
- Password: `xoserve_pass`

### **RabbitMQ**
- Host: `localhost:5672`
- UI: http://localhost:15672
- User: `guest`
- Password: `guest`

---

## ??? BASE DE DONNÉES

### **Tables principales**

| Table | Rôle |
|-------|------|
| `metric_values` | ? Données EAV (cœur métier) - 100K-500K lignes/mois |
| `ingestion_jobs` | Tracking jobs mensuels |
| `ingestion_files` | Métadonnées fichiers |
| `validation_errors` | Erreurs de validation |
| `shippers` | Référentiel transporteurs (~50) |
| `product_classes` | PC1/PC2/PC3/PC4 (seed data) |
| `report_types` | Schedule 2A / 2B (seed data) |
| `reports` | Rapports générés |

### **Commandes utiles**

```powershell
# Voir les tables
docker exec pafa_postgres psql -U postgres -d pafadb -c "\dt"

# Compter les métriques
docker exec pafa_postgres psql -U postgres -d pafadb -c "SELECT COUNT(*) FROM metric_values;"

# Voir les dernières métriques
docker exec pafa_postgres psql -U postgres -d pafadb -c "SELECT * FROM metric_values LIMIT 10;"
```

---

## ?? PROCHAINES ÉTAPES

### **Pour finaliser le POC (5-8 jours)**

1. ?? **Batch Reports** : Implémenter 41 templates Schedule 2A/2B
2. ?? **FileHash** : Ajouter déduplication (1 jour)
3. ?? **Tests** : Créer projet xUnit (2-3 jours)

---

## ?? SUPPORT

### **Problème technique ?**

1. Exécutez : `.\diagnose-docker.ps1`
2. Consultez : [GUIDE_DEPANNAGE_POSTGRES.md](GUIDE_DEPANNAGE_POSTGRES.md)
3. Vérifiez les logs : `docker-compose logs`

### **Question architecture ?**

Consultez [RAPPORT_FINAL_CABLAGE.md](RAPPORT_FINAL_CABLAGE.md) pour l'analyse exhaustive.

---

## ?? ARCHITECTURE

**Patterns utilisés** :
- ? Clean Architecture (DDD)
- ? CQRS (MediatR)
- ? Repository Pattern + Unit of Work
- ? Strategy Pattern (Report Writers)
- ? Template Method (Report Generators)
- ? Dependency Injection

**Technologies** :
- ? .NET 9
- ? EF Core 9
- ? PostgreSQL 16
- ? Docker Compose
- ? SSH.NET (SFTP)
- ? ClosedXML (Excel)
- ? QuestPDF (PDF)

---

## ?? LICENSE

[À définir]

---

## ?? CONTRIBUTEURS

- **Hamza Louati** - Développement principal
- GitHub: https://github.com/leaderslouati/PAFAProject

---

**?? POC OPÉRATIONNEL À 78% - DÉMO POSSIBLE DÈS MAINTENANT !**

Si vous suivez les instructions ci-dessus, votre environnement sera opérationnel en **5 minutes**.

**Besoin d'aide ?** Consultez [SOLUTION_EXPRESS.md](SOLUTION_EXPRESS.md) ??
