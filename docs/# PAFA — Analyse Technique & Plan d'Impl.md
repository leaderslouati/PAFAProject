# PAFA — Analyse Technique & Plan d'Implémentation Power BI
**Rôle : Architecte Logiciel & Tech Lead — BI Reporting**
**Date : Juin 2026**

---

## Partie 0 — Diagnostic : Erreurs & Incohérences Identifiées

### 0.1 Problèmes côté codebase (API / backend PAFA)

**PafaUserRoleConfiguration.cs — doublon de HasKey + HasOne**
La clé composite `HasKey(ur => new { ur.UserId, ur.RoleId })` est déclarée deux fois, et la relation `HasOne(ur => ur.User).WithMany(...)` est configurée deux fois avec des FK différentes. EF Core silencieusement garde la dernière déclaration, ce qui peut produire une migration corrompue.

**UsersODATAController — GetAwaiter().GetResult() bloquant**
Le commentaire justifie ce choix par la contrainte OData qui exige `IQueryable<T>` synchrone. C'est un anti-pattern sérieux : le thread de requête est bloqué, ce qui annule le gain du pipeline async d'ASP.NET Core sous charge. La solution correcte est d'exposer un `IQueryable<T>` directement depuis EF Core, sans matérialiser en mémoire via `GetAllAsync`. Cela introduit aussi un risque N+1 car `GetUsersQueryableHandler` fait un `await GetAllAsync` (qui charge tout en RAM) puis fait un `.AsQueryable()` — ce n'est pas un vrai IQueryable EF.

**PafaClaimsTransformation + AuthenticationExtensions — double injection de rôles**
Les rôles sont injectés dans `OnTokenValidated` (via l'OID Entra) ET dans `PafaClaimsTransformation` (via l'email). Pour les tokens Azure AD, les deux chemins s'exécutent, produisant des claims dupliqués. Il faut unifier en un seul point.

**ActiveUserMiddleware — lookup par email, pas par OID**
Le middleware extrait `ClaimTypes.Email` du JWT interne, mais le JWT Azure AD n'expose pas `ClaimTypes.Email` directement (il peut contenir `preferred_username`). Le middleware peut silencieusement laisser passer des utilisateurs non trouvés (retour `await next(context)` si `email` est null).

**CreateUserHandler — RoleId == 0 comme magic value**
La logique `if (cmd.RoleId == 0 && cmd.Email.EndsWith("@talan.com"))` utilise 0 comme valeur sentinelle non documentée. Cela masque des erreurs de validation amont et crée une dépendance implicite sur le domaine email pour affecter un rôle.

**TokenService — fallback sur `user.Role` (string)**
La propriété `user.Role` (champ libre `HasMaxLength(150)`) est un fallback quand `UserRoles` est vide. Elle n'est pas synchronisée avec les `PafaRoles` constants, ce qui peut produire des claims invalides.

### 0.2 Problèmes côté données sources (Excel → BI)

**Schéma non normalisé dans les sources**
Les fichiers `MOD520A__PAF_Reports_Apr26_*.xlsx` ont des headers multi-lignes (row 0 = titre du rapport, row 1 = description UNC, row 2 = mesure). Il n'y a pas de ligne d'en-tête propre à la colonne 0 : les données commencent à des offsets variables selon la feuille. Toute ingestion naive avec `pd.read_excel(header=0)` produira des colonnes erronées.

**Dates stockées en format mixte**
Dans `Rpt_1364_PARR_AQ_report_202604.xlsx`, les mois sont stockés comme `2025-03-01 00:00:00` (datetime Python) et comme `Mar-26` (string). Power BI devra normaliser ces deux formats en une dimension calendrier unique.

**Shipper codes vs Shipper noms anonymisés**
Les fichiers non-anonymisés utilisent des codes courts (`AGA`, `BRK`, `NGS`...), les fichiers anonymisés utilisent des noms de villes (`Tehran`, `Monaco`, `Castries`...). Il n'existe pas de table de correspondance dans les fichiers sources. Cette table de mapping doit être créée et maintenue hors des sources.

**Feuilles à pivot imbriqué**
`Class_3_conversion__*.xlsx`, `Supply_Points_*.xlsx` ont chacun deux feuilles : `Data` (granulaire) et `Pivot Table` (agrégée). Power Query ne doit consommer que `Data` — les pivots sont des artefacts Excel qui seront recréés dans DAX.

**`AQ_at_Risk_Mar_2026_For_PAFA.xlsx` — mois de référence en titre**
Le nom du fichier contient `Mar_2026` mais le rapport est diffusé en avril 2026 pour les données de mars. Le pipeline d'ingestion doit extraire le mois de référence du nom de fichier, pas de la date de traitement.

---

## Partie 1 — Analyse des Sources & Schéma Cible Base de Données

### 1.1 Catalogue des sources Excel et leur contenu sémantique

| Fichier source | Type | Granularité | Dimensions clés | Métriques |
|---|---|---|---|---|
| `MOD520A__PAF_Reports_Apr26_Non_Anonymised.xlsx` | Rapport de synthèse (33 onglets) | Shipper × Product Class × Mois | Shipper (code), Product Class (1-4), EUC Band, Mois | % portefeuille, Counts, AQ (GWh) |
| `MOD520A__PAF_Reports_Apr26_Anonymised.xlsx` | Rapport de synthèse (25 onglets) | Shipper (anonymisé) × Product Class × Mois | Shipper (ville), Product Class, EUC Band, Mois | Mêmes métriques que ci-dessus |
| `Rpt_1364_PARR_AQ_report_202604.xlsx` | PARR AQ (8 rapports) | Shipper × EUC × Mois (12 rolling) | Shipper (code), EUC01-EUC09, Classe, Mois | % calculé, % augmenté, % diminué, fréquence |
| `AQ_at_Risk_Mar_2026_For_PAFA.xlsx` | AQ à risque | Shipper × Product Class | Shipper (code), Product Class | AQ (GWh), % en retard |
| `Read_Performance_by_Shipper_5.xlsx` | Read perf par Shipper | Shipper × Topic | Shipper (code), Topic | AQ Read Performance (%) |
| `Shipper_Transfer_Read_Performance_5.xlsx` | Transferts | Shipper | Shipper (code) | # transferts, % read perf |
| `Supply_Points_with_Minimum_Percentage_Requirement_4_PC2.xlsx` | Req. min PC2 | Shipper × Mois | SRVC_PRVDR_CD, Year/Month | Read submission 3 monthly (%) |
| `Supply_Points_with_Minimum_Percentage_Requirement_5__PC3.xlsx` | Req. min PC3 | Shipper × Mois | SRVC_PRVDR_CD, Year/Month | Read submission 3 monthly (%) |
| `Supply_Points_and_AQ_with_Minimum_Percentage_Not_met_4__PC2.xlsx` | Non-conformes PC2 | Shipper × Mois | SRVC_PRVDR_CD, Year/Month | MPRN Count, Rolling AQ |
| `Supply_Points_and_AQ_with_Minimum_Percentage_Not_met_5__PC3.xlsx` | Non-conformes PC3 | Shipper × Mois | SRVC_PRVDR_CD, Year/Month | MPRN Count, Rolling AQ |
| `Class_3_conversion__*.xlsx` | Reclassification Classe 3 | Shipper × Mois | SRVC_PRVDR_CD, Year/Month | MPRN Count reclassifié, AQ rolling |
| `EUC09_Reporting_PAC_2026_04.xlsx` | EUC09 met/not met | Shipper × EUC | Shipper, EUC | MET/NOT MET, Reclassifié par |
| `Confirmed_Energy_Theft_*.xlsx` (×4) | Energy Theft | Shipper × Protocol | Shipper, P41/P106 | Claims, Objections, Withdrawals |
| `2B_21_Corrective_Opening_Meter_Reading_Rejections_Apr26.xlsx` | Correctifs ouverture | Shipper × Mois | Shipper, Mois | Counts, % rejections |
| `202604_Apr2026_SupplyPointCounts.xlsx` | Comptage supply points | Gas Day × SHP × Classe × LDZ | Gas Day, Shipper, Class, EUC, Network Zone | Count MPRN, AQ_ROLL |
| `Report_1__Percentage_MPRN_removed_from_Must_Read.xlsx` | Must Read removal % | Shipper × MURD Reference Month | Shipper, MURD Month, Year/Month | % MPRN retiré |
| `Report_2__Percentage_MPRN_removed_from_Must_Read_age_bucket.xlsx` | Must Read par bucket d'âge | Shipper × Age Bucket | Shipper, Year/Month, Age Bucket | % MPRN retiré par bucket |
| `Report_2__Proportion_of_sites_set_as_Vacant_at_the_end_of_each_Month_3.xlsx` | Proportion Vacant | Shipper × Mois | Shipper, Year/Month | Proportion Vacant (%) |
| `Report_1A__Sites_set_to_Vacant_within_the_month.xlsx` | Sites passés Vacant | Shipper × Mois | Shipper, Year/Month | Count sites → Vacant |
| `Report_1B__Count_of_Vacant_sites_at_the_end_of_the_month_1.xlsx` | Sites Vacant fin de mois | Shipper × Mois | Shipper, Year/Month | Count sites Vacant |
| `Report_3__Count_MPRN_removed_from_Must_Read.xlsx` | Count MPRN Must Read | Shipper × MURD Reference Month | Shipper, MURD Month, Year/Month | Count MPRN retiré |

### 1.2 Schéma cible PostgreSQL (Fact + Dimension star schema)

**Principe** : schéma en étoile. Une table `dim_shipper` centrale avec flag `is_anonymised` contrôle l'accès. Toutes les tables de faits référencent `shipper_id` — les vues filtrées par ce flag gèrent l'anonymisation, pas des datasets séparés.

```sql
-- ═══════════════════════════════════════════════════════════
-- DIMENSIONS
-- ═══════════════════════════════════════════════════════════

CREATE TABLE dim_shipper (
    shipper_id        SERIAL PRIMARY KEY,
    shipper_code      VARCHAR(10) NOT NULL UNIQUE,   -- ex: AGA, BRK
    shipper_name      VARCHAR(100),                   -- nom réel (non-anonymisé)
    shipper_alias     VARCHAR(100),                   -- nom de ville (anonymisé)
    licence_active    BOOLEAN DEFAULT TRUE,
    created_at        TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE dim_product_class (
    class_id     SMALLINT PRIMARY KEY,               -- 1, 2, 3, 4
    class_name   VARCHAR(20) NOT NULL,               -- ex: 'Class 1 (PC1)'
    class_code   VARCHAR(5) NOT NULL                 -- ex: 'PC1'
);

CREATE TABLE dim_euc_band (
    euc_id       SMALLINT PRIMARY KEY,               -- 1..9
    euc_code     VARCHAR(10) NOT NULL,               -- ex: 'EUC01'
    description  VARCHAR(200)
);

CREATE TABLE dim_calendar (
    date_id         INTEGER PRIMARY KEY,             -- YYYYMMDD
    full_date       DATE NOT NULL,
    year            SMALLINT,
    month           SMALLINT,
    month_name      VARCHAR(20),
    year_month      VARCHAR(10),                     -- ex: '2026/03'
    quarter         SMALLINT,
    is_reporting_month BOOLEAN DEFAULT FALSE
);

CREATE TABLE dim_report_type (
    report_type_id   SERIAL PRIMARY KEY,
    report_code      VARCHAR(20) NOT NULL UNIQUE,    -- ex: '2A.1', '2B.5'
    report_name      VARCHAR(200),
    visibility       VARCHAR(15) CHECK (visibility IN ('anonymised','non_anonymised','both')),
    topic            VARCHAR(100),
    product_class_scope VARCHAR(50)                  -- ex: 'PC1,PC2' ou 'ALL'
);

CREATE TABLE dim_protocol (
    protocol_id   SMALLINT PRIMARY KEY,
    protocol_code VARCHAR(10) NOT NULL,              -- ex: 'P41', 'P106'
    description   VARCHAR(200)
);

-- ═══════════════════════════════════════════════════════════
-- TABLES DE FAITS
-- ═══════════════════════════════════════════════════════════

-- Fact principale : performance de lecture par Shipper/Classe/Mois
CREATE TABLE fact_read_performance (
    id                BIGSERIAL PRIMARY KEY,
    shipper_id        INTEGER REFERENCES dim_shipper(shipper_id),
    class_id          SMALLINT REFERENCES dim_product_class(class_id),
    euc_id            SMALLINT REFERENCES dim_euc_band(euc_id),
    reporting_date_id INTEGER REFERENCES dim_calendar(date_id),
    report_type_id    INTEGER REFERENCES dim_report_type(report_type_id),
    -- Métriques
    read_performance_pct   NUMERIC(10,6),
    estimated_reads_pct    NUMERIC(10,6),
    check_reads_not_done   INTEGER,
    no_reads_1yr_pct       NUMERIC(10,6),
    no_reads_2yr_pct       NUMERIC(10,6),
    no_reads_3yr_pct       NUMERIC(10,6),
    no_reads_4yr_pct       NUMERIC(10,6),
    mprn_count             INTEGER,
    aq_gwh                 NUMERIC(18,4),
    aq_rolling_kwh         BIGINT,
    source_file            VARCHAR(300),
    ingested_at            TIMESTAMPTZ DEFAULT NOW()
);

-- Fact AQ at Risk (Overdue)
CREATE TABLE fact_aq_at_risk (
    id                 BIGSERIAL PRIMARY KEY,
    shipper_id         INTEGER REFERENCES dim_shipper(shipper_id),
    class_id           SMALLINT REFERENCES dim_product_class(class_id),
    reporting_date_id  INTEGER REFERENCES dim_calendar(date_id),
    aq_at_risk_gwh     NUMERIC(18,4),
    pct_overdue        NUMERIC(10,6),
    source_file        VARCHAR(300),
    ingested_at        TIMESTAMPTZ DEFAULT NOW()
);

-- Fact Supply Points / MPRN Counts
CREATE TABLE fact_supply_point_counts (
    id                BIGSERIAL PRIMARY KEY,
    shipper_id        INTEGER REFERENCES dim_shipper(shipper_id),
    class_id          SMALLINT REFERENCES dim_product_class(class_id),
    euc_id            SMALLINT REFERENCES dim_euc_band(euc_id),
    gas_day_date_id   INTEGER REFERENCES dim_calendar(date_id),
    network_zone      VARCHAR(20),
    ldz_code          VARCHAR(10),
    mprn_count        INTEGER,
    aq_rolling_kwh    BIGINT,
    source_file       VARCHAR(300),
    ingested_at       TIMESTAMPTZ DEFAULT NOW()
);

-- Fact Shipper Transfer Read Performance
CREATE TABLE fact_transfer_read_performance (
    id                BIGSERIAL PRIMARY KEY,
    shipper_id        INTEGER REFERENCES dim_shipper(shipper_id),
    reporting_date_id INTEGER REFERENCES dim_calendar(date_id),
    transfer_count    INTEGER,
    read_perf_pct     NUMERIC(10,6),
    source_file       VARCHAR(300),
    ingested_at       TIMESTAMPTZ DEFAULT NOW()
);

-- Fact Energy Theft (Claims & Withdrawals)
CREATE TABLE fact_energy_theft (
    id                  BIGSERIAL PRIMARY KEY,
    shipper_id          INTEGER REFERENCES dim_shipper(shipper_id),
    protocol_id         SMALLINT REFERENCES dim_protocol(protocol_id),
    reporting_date_id   INTEGER REFERENCES dim_calendar(date_id),
    event_type          VARCHAR(20) CHECK (event_type IN ('claim','withdrawal')),
    submission_count    INTEGER,
    objection_count     INTEGER,
    source_file         VARCHAR(300),
    ingested_at         TIMESTAMPTZ DEFAULT NOW()
);

-- Fact Class 3 Reclassification
CREATE TABLE fact_class3_reclassification (
    id                   BIGSERIAL PRIMARY KEY,
    shipper_id           INTEGER REFERENCES dim_shipper(shipper_id),
    reporting_date_id    INTEGER REFERENCES dim_calendar(date_id),
    reclassified_mprn_count   INTEGER,
    reclassified_rolling_aq   BIGINT,
    reclassified_mprn_pct     NUMERIC(10,6),
    source_file          VARCHAR(300),
    ingested_at          TIMESTAMPTZ DEFAULT NOW()
);

-- Fact Must Read Process (IGT)
CREATE TABLE fact_must_read (
    id                BIGSERIAL PRIMARY KEY,
    shipper_id        INTEGER REFERENCES dim_shipper(shipper_id),
    reporting_date_id INTEGER REFERENCES dim_calendar(date_id),
    murd_reference_month_id INTEGER REFERENCES dim_calendar(date_id),
    mprn_removed_pct  NUMERIC(10,6),
    mprn_removed_count INTEGER,
    age_bucket        VARCHAR(30),
    source_file       VARCHAR(300),
    ingested_at       TIMESTAMPTZ DEFAULT NOW()
);

-- Fact Vacant Sites
CREATE TABLE fact_vacant_sites (
    id                BIGSERIAL PRIMARY KEY,
    shipper_id        INTEGER REFERENCES dim_shipper(shipper_id),
    reporting_date_id INTEGER REFERENCES dim_calendar(date_id),
    sites_set_vacant_in_month INTEGER,
    sites_vacant_at_end       INTEGER,
    proportion_vacant_pct     NUMERIC(10,6),
    source_file       VARCHAR(300),
    ingested_at       TIMESTAMPTZ DEFAULT NOW()
);

-- Fact AQ Portfolio (PARR AQ Report - 8 sub-reports)
CREATE TABLE fact_aq_portfolio (
    id                  BIGSERIAL PRIMARY KEY,
    shipper_id          INTEGER REFERENCES dim_shipper(shipper_id),
    class_id            SMALLINT REFERENCES dim_product_class(class_id),
    euc_id              SMALLINT REFERENCES dim_euc_band(euc_id),
    reporting_date_id   INTEGER REFERENCES dim_calendar(date_id),
    parr_sub_report     SMALLINT,                    -- 1..8 (Report 1..Report 8 du fichier PARR)
    pct_calculated      NUMERIC(10,6),
    pct_increased       NUMERIC(10,6),
    pct_decreased       NUMERIC(10,6),
    failure_count       INTEGER,
    rolling_months      SMALLINT,                    -- 1, 4, 12, 24, 36 mois
    source_file         VARCHAR(300),
    ingested_at         TIMESTAMPTZ DEFAULT NOW()
);

-- Table d'audit d'ingestion (traçabilité obligatoire per NFR)
CREATE TABLE audit_ingestion_log (
    log_id          BIGSERIAL PRIMARY KEY,
    source_file     VARCHAR(300) NOT NULL,
    file_hash       VARCHAR(64),                     -- SHA-256
    reporting_month DATE,
    rows_ingested   INTEGER,
    rows_rejected   INTEGER,
    status          VARCHAR(20) CHECK (status IN ('success','partial','failed')),
    error_detail    TEXT,
    triggered_by    VARCHAR(200),                    -- user_id ou 'system'
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ
);
```

---

## Partie 2 — Architecture & Modélisation du Dataset Power BI

### 2.1 Stratégie anonymisé vs non-anonymisé

**Recommandation : vues SQL dédiées + dataset Power BI unique avec RLS**

La cohabitation des deux rapports (2A anonymisé / 2B non-anonymisé) doit être résolue au niveau de la base de données, pas au niveau de Power BI. Voici pourquoi et comment.

**Approche retenue : deux vues SQL + un seul dataset Power BI avec Row-Level Security (RLS)**

Deux vues PostgreSQL qui projettent les mêmes tables de faits :

```sql
-- Vue anonymisée (expose shipper_alias)
CREATE VIEW v_read_performance_anonymised AS
SELECT
    f.*,
    s.shipper_alias   AS shipper_display_name,   -- ex: 'Tehran'
    s.licence_active,
    pc.class_name,
    ec.euc_code,
    cal.year_month,
    cal.full_date      AS reporting_date
FROM fact_read_performance f
JOIN dim_shipper s      ON f.shipper_id = s.shipper_id
JOIN dim_product_class pc ON f.class_id = pc.class_id
JOIN dim_euc_band ec   ON f.euc_id = ec.euc_id
JOIN dim_calendar cal  ON f.reporting_date_id = cal.date_id;

-- Vue non-anonymisée (expose shipper_code + shipper_name)
CREATE VIEW v_read_performance_non_anonymised AS
SELECT
    f.*,
    s.shipper_code     AS shipper_display_name,   -- ex: 'AGA'
    s.shipper_name,
    s.licence_active,
    pc.class_name,
    ec.euc_code,
    cal.year_month,
    cal.full_date      AS reporting_date
FROM fact_read_performance f
JOIN dim_shipper s      ON f.shipper_id = s.shipper_id
JOIN dim_product_class pc ON f.class_id = pc.class_id
JOIN dim_euc_band ec   ON f.euc_id = ec.euc_id
JOIN dim_calendar cal  ON f.reporting_date_id = cal.date_id;
```

**Dans Power BI** : un seul dataset importe les deux vues. Le RLS (Row-Level Security) filtre dynamiquement selon le rôle de l'utilisateur connecté :

- Rôle Power BI `PAC_Member` → accès uniquement aux tables `_anonymised`
- Rôle Power BI `PAFA_Admin` → accès aux deux vues
- Rôle Power BI `SuperPAFA_Admin` → accès complet

La règle DAX du RLS pour la table anonymisée :

```dax
-- Sur la table v_read_performance_anonymised
-- Autorise PAC_Member + Admin
USERPRINCIPALNAME() IN
    SELECTCOLUMNS(
        FILTER(dim_pafa_users, dim_pafa_users[role] IN {"PAC_Member","PAFA_Admin","SuperPAFA_Admin"}),
        "upn", dim_pafa_users[email]
    )
```

**Pourquoi pas deux datasets séparés ?**
Deux datasets = double maintenance des mesures DAX, double maintenance des relations, risque de divergence. Le RLS au niveau du dataset est la pratique Microsoft recommandée pour ce cas d'usage (un seul modèle, accès différencié par rôle).

### 2.2 Structure du dataset Power BI

**Import mode vs DirectQuery** : avec une licence Pro et un volume de données raisonnable (rapports mensuels), le mode **Import** est recommandé. Il garantit des performances sub-seconde pour les visuels et évite les limitations de DirectQuery avec PostgreSQL en Pro. Le refresh est programmé une fois par mois après ingestion.

**Tables à importer dans Power BI** :

```
Dimensions :
  dim_shipper
  dim_product_class
  dim_euc_band
  dim_calendar
  dim_report_type
  dim_protocol

Tables de faits :
  fact_read_performance        (via v_read_performance_anonymised OU v_read_performance_non_anonymised)
  fact_aq_at_risk
  fact_supply_point_counts
  fact_transfer_read_performance
  fact_energy_theft
  fact_class3_reclassification
  fact_must_read
  fact_vacant_sites
  fact_aq_portfolio
```

**Relations (modèle en étoile)** : toutes les tables de faits se connectent aux dimensions via leur clé étrangère (Many-to-One, direction de filtre unique de la dimension vers le fait).

**Mesures DAX clés à créer** :

```dax
-- Read Performance moyenne pondérée industrie
Industry Avg Read Perf % =
CALCULATE(
    AVERAGEX(dim_shipper, [Avg Read Performance Pct]),
    ALLEXCEPT(dim_calendar, dim_calendar[year_month])
)

-- Variation mensuelle
Monthly Change % =
VAR current = [Avg Read Performance Pct]
VAR previous = CALCULATE([Avg Read Performance Pct],
    DATEADD(dim_calendar[full_date], -1, MONTH))
RETURN DIVIDE(current - previous, previous)

-- Variation annuelle
Annual Change % =
VAR current = [Avg Read Performance Pct]
VAR previous12 = CALCULATE([Avg Read Performance Pct],
    DATEADD(dim_calendar[full_date], -12, MONTH))
RETURN DIVIDE(current - previous12, previous12)

-- Top 3 performers (pour les annotations slides)
Top 3 Shippers =
CONCATENATEX(
    TOPN(3, ALL(dim_shipper[shipper_display_name]), [Avg Read Performance Pct], ASC),
    dim_shipper[shipper_display_name] & " " &
    FORMAT([Avg Read Performance Pct], "0.00%"),
    UNICHAR(10)
)

-- Seuil de performance (pour conditional formatting)
Below Target Flag =
IF([Avg Read Performance Pct] < [Performance Target], 1, 0)
```

---

## Partie 3 — Stratégie d'Exportation XLS & PPTX

### 3.1 Export Excel — contraintes et solutions (licence Pro)

**Ce que la licence Pro permet** :
- Export manuel vers Excel depuis le service Power BI (`.xlsx` avec données sous-jacentes ou résumées)
- Export via API REST Power BI (`POST /reports/{reportId}/ExportTo`) avec le format `XLSX`
- Power BI Report Builder : export natif en Excel via paginated reports
- Automatisation via Power Automate (inclus dans M365)

**Ce que la licence Pro ne permet PAS** :
- Export programmé automatique natif depuis Power BI Service (nécessite Premium/Fabric pour les scheduled exports)
- `ExportToFile` API pour les paginated reports (nécessite Premium Per User minimum)

**Solution recommandée pour l'export Excel automatisé avec licence Pro** :

Utiliser **Power Automate** + **Power BI REST API** + **SharePoint** :

```
Déclencheur : Recurrence (1er du mois, après ingestion confirmée)
  ↓
Action : Power BI — Export Report (ExportTo XLSX)
  ↓
Action : Condition — Statut export = Succeeded ?
  ↓ Oui
Action : SharePoint — Create file (YYYYMM_PARR_Anonymised.xlsx)
Action : SharePoint — Create file (YYYYMM_PARR_NonAnonymised.xlsx)
  ↓
Action : Send email notification
```

**Contrainte critique** : l'export Power BI via API génère un snapshot du rapport au format visuel (tables, matrices) — pas un classeur Excel structuré avec des données brutes. Pour les rapports tabulaires (non-anonymisé avec données par Shipper), utiliser **Power BI Report Builder** en paginated report : il produit un Excel correctement structuré, une ligne = un enregistrement.

### 3.2 Export PowerPoint — 1 slide = 1 rapport

**Architecture de la solution PPTX** :

La structure observée dans `PARR_Dashboards_20260512.pdf` (39 slides, 1 slide = 1 rapport 2A.x ou section annexe) doit être reproduite via **Power Automate + Office Script** ou **python-pptx** côté back-end PAFA.

**Contrainte majeure avec licence Pro** :
Power BI Service ne génère pas nativement de PPTX reflétant 1 slide = 1 rapport avec une mise en page propre. L'export PPTX natif Power BI est un export de visuels en tant qu'images dans des slides génériques — pas de contrôle sur la mise en page.

**Solution recommandée : génération PPTX côté serveur (python-pptx)**

Le back-end PAFA (C# / Python worker) génère le PPTX en 3 étapes :

**Étape 3.2.1 — Extraction des données depuis Power BI via REST API**
```python
# Pour chaque rapport 2A.x, appeler l'API Power BI Dataset
# pour récupérer les KPIs (mouvements, top/bottom Shippers)
import requests

def get_report_kpis(dataset_id, dax_query):
    url = f"https://api.powerbi.com/v1.0/myorg/datasets/{dataset_id}/executeQueries"
    payload = {"queries": [{"query": dax_query}], "serializerSettings": {"includeNulls": True}}
    resp = requests.post(url, json=payload, headers={"Authorization": f"Bearer {token}"})
    return resp.json()["results"][0]["tables"][0]["rows"]
```

**Étape 3.2.2 — Export des visuels Power BI comme images**
```python
# ExportTo API pour obtenir chaque visuel en PNG
def export_visual_as_png(report_id, page_name, visual_name):
    body = {
        "format": "PNG",
        "powerBIReportConfiguration": {
            "pages": [{"pageName": page_name, "visuals": [{"visualName": visual_name}]}]
        }
    }
    # POST /reports/{reportId}/ExportTo → polling sur status → GET le fichier PNG
```

**Étape 3.2.3 — Assemblage PPTX avec python-pptx**
```python
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor

def build_parr_slide(prs, slide_config):
    """
    slide_config = {
        'title': '2A.1 ESTIMATED & CHECK READS - PRODUCT CLASSES 1 & 2',
        'chart_images': ['chart_pc1.png', 'chart_pc2.png'],
        'kpis': {
            'industry_monthly_change': '-0.13%',
            'industry_annual_change': '+4.50%',
            'top_movers': [...],
            'observations': ['...', '...']
        }
    }
    """
    slide_layout = prs.slide_layouts[6]  # Blank layout
    slide = prs.slides.add_slide(slide_layout)

    # Header band (couleur Talan/PAFA)
    header = slide.shapes.add_shape(...)
    # Titre du rapport
    txBox = slide.shapes.add_textbox(Inches(0.3), Inches(0.1), Inches(9.4), Inches(0.5))
    txBox.text_frame.text = slide_config['title']

    # Insertion des charts en PNG (positionnés exactement comme le template)
    for i, img_path in enumerate(slide_config['chart_images']):
        slide.shapes.add_picture(img_path, left=..., top=..., width=..., height=...)

    # KPI boxes (Industry movement)
    add_kpi_box(slide, '↓ 0.13% - Monthly change', ...)
    add_kpi_box(slide, '↑ 4.50% - Annual change', ...)

    # Observations text box
    add_observations(slide, slide_config['kpis']['observations'])
```

**Mapping slides → rapports** (basé sur l'analyse du dashboard) :

| Slide | Rapport | Source fact table |
|---|---|---|
| 1 | Cover / Titre | — |
| 2-3 | 2A.1 Estimated & Check Reads PC1+PC2 | `fact_read_performance` (estimated_reads_pct, check_reads_not_done) |
| 4 | 2A.2 No Meter Recorded | `fact_read_performance` |
| 5 | 2A.3 No Meter Recorded + Data Flows | `fact_read_performance` |
| 6 | 2A.4 Shipper Transfer Read Perf | `fact_transfer_read_performance` |
| 7-11 | 2A.5 Read Performance (PC1-4) | `fact_read_performance` |
| 12 | 2A.6 Meter Read Validity Monitoring | `fact_read_performance` |
| 13-20 | 2A.7 No Reads 1/2/3/4 yrs (PC1-4) | `fact_read_performance` |
| 21 | 2A.8 AQ Correction by Reason | `fact_read_performance` |
| 22 | 2A.9 Standard CF AQ > 732k kWh | `fact_supply_point_counts` |
| 23 | 2A.10 Replaced Meter Reads | `fact_read_performance` |
| 24 | 2A.11 Sites above Class 1 threshold | `fact_supply_point_counts` |
| 25-27 | 2A.12a/b/c AQ Read Performance PC4 | `fact_read_performance` |
| 28 | 2A.13 AQ at Risk | `fact_aq_at_risk` |
| 29 | 2A.14 Confirmed Energy Theft | `fact_energy_theft` |
| 30 | 2A.15 CDSP Sites converted PC4 | `fact_class3_reclassification` |
| 31 | 2A.16 PC2/PC3 Read Performance | `fact_read_performance` (Supply Points min%) |
| 32 | 2A.17 IGT Must Read Process | `fact_must_read` |
| 33 | 2A.18 Corrective Opening Reading | `fact_read_performance` (corrections) |
| 34 | 2A.19 Class 4 Vacant Sites | `fact_vacant_sites` |
| 35-38 | Annexe — PARR Report Details | Statique (contenu documentaire) |
| 39 | Back cover | — |

---

## Partie 4 — Plan d'Implémentation Étape par Étape

### PHASE 1 — Fondations (Semaines 1-2)

**Step 1 — Provisionnement de l'environnement**
- Créer la base PostgreSQL dédiée `pafa_dw` (séparée de `pafa_app`)
- Créer les rôles DB : `pafa_reader` (SELECT sur vues), `pafa_writer` (INSERT sur facts), `pafa_admin` (DDL)
- Provisionner le workspace Power BI (Pro) avec les rôles PAFA
- Configurer le On-Premises Data Gateway (ou Azure PostgreSQL + connexion directe)
- Créer le SharePoint folder structure pour les fichiers sources et exports

**Step 2 — Création du schéma base de données**
- Exécuter les DDL (dimensions + faits + vues + audit log)
- Seeder les dimensions statiques : `dim_product_class` (PC1-4), `dim_euc_band` (EUC01-09), `dim_protocol` (P41, P106)
- Seeder `dim_calendar` (2020-01-01 → 2030-12-31)
- Seeder `dim_shipper` avec les 40+ codes Shipper identifiés dans les sources + les alias anonymisés

**Step 3 — Création de la table de mapping Shipper anonymisé**
- Construire manuellement (ou extraire du slide 2 du dashboard) la correspondance `shipper_code ↔ shipper_alias`
  - Ex : `AGA → Tehran` (identifié dans le slide 2A.1 des dashboards)
- Cette table est la clé de voûte de toute la stratégie d'anonymisation

### PHASE 2 — Pipeline d'Ingestion (Semaines 3-5)

**Step 4 — Développement du parser Python**

Un script Python modulaire par type de fichier source :

```python
# Structure recommandée
parsers/
  base_parser.py          # Classe abstraite : validate, parse, transform, load
  mod520a_parser.py       # MOD520A Anonymised + Non-Anonymised (33+25 sheets)
  parr_aq_parser.py       # Rpt_1364 (8 sub-reports)
  aq_risk_parser.py       # AQ at Risk
  read_perf_parser.py     # Read Performance by Shipper
  supply_points_parser.py # Supply Point Counts
  transfer_parser.py      # Shipper Transfer Read Performance
  theft_parser.py         # Energy Theft (×4 files)
  must_read_parser.py     # IGT Must Read + Must Read Reports 1/2/3
  vacant_sites_parser.py  # Vacant Sites Reports 1A/1B/2/3
  class3_parser.py        # Class 3 Reclassification
```

Chaque parser :
1. Détecte le mois de référence depuis le nom de fichier
2. Saute les lignes d'en-tête multi-lignes (skip rows configurables)
3. Normalise les codes Shipper (strip whitespace, uppercase)
4. Convertit les valeurs `'-'` en `NULL` (pattern répandu dans les sources)
5. Convertit les percentages (stockés comme `0.823269` ou `82.3269` selon les fichiers) en valeur décimale uniforme
6. Insère dans les tables de faits + loggue dans `audit_ingestion_log`

**Step 5 — Intégration SharePoint**

```python
# Via Microsoft Graph API (déjà intégré dans le back-end PAFA via GraphServiceClient)
async def fetch_source_files_from_sharepoint(month_folder: str):
    """
    Liste les fichiers dans /PARR/Sources/YYYYMM/
    Télécharge uniquement les fichiers non-encore ingérés (check audit_log)
    Retourne les chemins locaux temporaires
    """
```

**Step 6 — Validation & quarantaine**

Conformément à PF-2 et PF-3 :
- Valider : colonnes obligatoires présentes, types corrects, absence de doublons, date cohérente avec le mois de référence
- Quarantaine : les fichiers invalides sont déplacés vers `/PARR/Quarantine/YYYYMM/` sur SharePoint
- Notification : envoi d'un email (via `IEmailService` existant) à PAFA Admin avec le détail des rejets

### PHASE 3 — Dataset & Rapports Power BI (Semaines 6-8)

**Step 7 — Connexion Power BI au dataset PostgreSQL**
- Configurer la connexion PostgreSQL dans Power BI Desktop
- Importer les vues `v_read_performance_anonymised` et `v_read_performance_non_anonymised`
- Importer toutes les tables de faits et dimensions
- Créer toutes les relations (modèle en étoile)
- Créer les mesures DAX clés (Industry Avg, Monthly Change, Annual Change, Top/Bottom 3)

**Step 8 — Configuration RLS**
- Définir les rôles Power BI : `PAC_Member`, `PAFA_Admin`, `SuperPAFA_Admin`
- Appliquer les filtres DAX par rôle sur chaque table de faits
- Tester avec `View as role` pour valider que PAC_Member ne voit que les données anonymisées

**Step 9 — Création des pages de rapport Power BI**

Une page par slide du dashboard (ou groupe de slides). Nommer les pages avec le code rapport (`2A.1`, `2A.2`...) pour faciliter le mapping API lors de l'export.

Chaque page contient :
- Un visuel principal (line chart tendance 12 mois rolling)
- Un visuel secondaire (bar chart par Shipper ou par Product Class)
- Des card visuels pour les KPIs (Industry movement ↑/↓)
- Un slicer caché (filtré par le RLS, non visible en production)

**Step 10 — Power BI Report Builder (Paginated Reports)**

Pour les exports Excel structurés (non-anonymisé, données Shipper par Shipper) :
- Créer un paginated report par grand groupe de rapports (2B.1-2B.10, 2B.11-2B.22)
- Configurer la source de données : Direct Query sur `v_read_performance_non_anonymised`
- Paramètres : `@reporting_month`, `@product_class`, `@shipper_code`
- Export cible : Excel multi-onglets (1 onglet = 1 rapport 2B.x)

### PHASE 4 — Génération PPTX Automatisée (Semaines 9-11)

**Step 11 — Service de génération PPTX (Python)**

```python
# Dans le back-end PAFA, ajouter un PptxGenerationService
class PptxGenerationService:
    def __init__(self, pbi_client, db_session):
        self.pbi = pbi_client
        self.db = db_session

    async def generate_monthly_dashboard(self, reporting_month: date, mode: str):
        """
        mode = 'anonymised' | 'non_anonymised'
        """
        prs = Presentation()
        prs.slide_width = Inches(13.33)   # Widescreen 16:9
        prs.slide_height = Inches(7.5)

        # Slide 1 : Cover
        self._add_cover_slide(prs, reporting_month, mode)

        # Slides 2A.1 → 2A.19
        for report_config in REPORT_SLIDES_CONFIG[mode]:
            kpis = await self._fetch_kpis(report_config, reporting_month)
            charts = await self._export_pbi_visuals(report_config, reporting_month)
            self._add_report_slide(prs, report_config, kpis, charts)

        # Slides Annexe
        self._add_appendix_slides(prs)

        # Slide finale
        self._add_back_cover(prs)

        output_path = f"/outputs/{reporting_month:%Y%m}_PARR_Dashboard_{mode}.pptx"
        prs.save(output_path)
        return output_path
```

**Step 12 — Configuration du template PPTX**

Créer un template `.pptx` (maître) avec :
- Charte graphique PAFA/Talan (couleurs, fonts, logo)
- Slide master avec header band, footer avec date et numéro de page
- Layouts prédéfinis : `ReportSlide_SingleChart`, `ReportSlide_DualChart`, `ReportSlide_TableOnly`
- Ce template est chargé comme base par `python-pptx` (`Presentation('template.pptx')`)

**Step 13 — Automatisation Power Automate**

```
Déclencheur : HTTP (appelé par le back-end PAFA après ingestion confirmée)
  ↓
Action : Azure Function / HTTP — lancer PptxGenerationService
  ↓
Action : Attente (polling sur status)
  ↓
Action : SharePoint — Upload PPTX anonymisé → /PARR/Outputs/Anonymised/YYYYMM/
Action : SharePoint — Upload PPTX non-anonymisé → /PARR/Outputs/NonAnonymised/YYYYMM/
  ↓
Action : Power BI — Trigger dataset refresh
  ↓
Action : Send email — Notification aux PAFA Admins avec liens SharePoint
```

### PHASE 5 — Sécurité, Audit & Tests (Semaines 12-13)

**Step 14 — Sécurité et conformité GDPR**
- Vérifier que les données Shipper non-anonymisées ne sont jamais exposées dans les logs (NFR : `No sensitive data exposed in logs`)
- Chiffrement des connexions PostgreSQL (SSL/TLS)
- Revue d'accès : les Power BI capacité partagée ne permettent pas le RLS dynamique basé sur Entra ; valider que les workspaces Pro sont bien configurés avec les groupes Entra corrects
- Retention des `audit_ingestion_log` : 12 mois minimum (NFR)

**Step 15 — Tests end-to-end**
- Test d'ingestion avec les 20 fichiers sources fournis (mois d'avril 2026)
- Validation des métriques : comparer les totaux Power BI avec les pivots Excel sources (tolérance : 0%)
- Test RLS : confirmer que PAC_Member ne voit pas `shipper_code` ou `shipper_name`
- Test export PPTX : valider que chaque slide correspond au rapport attendu, avec les bonnes données
- Test de performance : ingestion < 60s, refresh Power BI < 5 min, génération PPTX < 2 min

**Step 16 — Documentation & déploiement**
- Documenter le data dictionary (colonnes, types, sources)
- Documenter le processus de run manuel (cas de fallback si l'automatisation échoue)
- Déployer en production avec le runbook de migration (NFR : Maintainability)

---

## Résumé Décisionnel

| Décision | Choix retenu | Raison |
|---|---|---|
| Anonymisé vs non-anonymisé | 2 vues SQL + 1 dataset Power BI + RLS | Maintenance unique, sécurité garantie par la DB |
| Mode Power BI | Import (pas DirectQuery) | Pro + volumes mensuels → performances optimales |
| Export Excel | Power BI Report Builder (paginated) + Power Automate | Seul moyen d'exporter un Excel structuré multi-onglets en Pro |
| Export PPTX | python-pptx côté serveur PAFA | Contrôle total du layout, 1 slide = 1 rapport garanti |
| Mapping shipper anonymisé | Table `dim_shipper` avec `shipper_alias` | Source unique de vérité, maintenu manuellement ou via API |
| Automatisation | Power Automate + REST API Power BI | Inclus dans M365 Pro, pas besoin de Premium |
| Ingestion des Excel sources | Python (pandas + openpyxl) | Flexibilité sur les headers multi-lignes et formats mixtes |

---

*Document produit par l'équipe Talan — Architecture Logicielle & Business Intelligence — Juin 2026*