# SQL Views pour Power BI — Phase 1

## 🎯 Objectif
Créer 4 vues SQL optimisées pour Power BI, utilisables en mode Import ou DirectQuery

---

## 1️⃣ VUE : vw_dim_date (Dimension de Date)

```sql
-- Purpose: Date dimension for time-based analysis
-- Used in: Slicers, filters, trend charts
-- Refresh: Once (static table)

CREATE OR REPLACE VIEW vw_dim_date AS
SELECT
    DISTINCT
    m."ReportingPeriod" AS date_id,
    EXTRACT(YEAR FROM m."ReportingPeriod")::INT AS year,
    EXTRACT(MONTH FROM m."ReportingPeriod")::INT AS month,
    EXTRACT(QUARTER FROM m."ReportingPeriod")::INT AS quarter,
    TO_CHAR(m."ReportingPeriod", 'YYYY-MM') AS year_month,
    TO_CHAR(m."ReportingPeriod", 'YYYY-Q"Q"') AS year_quarter,
    TO_CHAR(m."ReportingPeriod", 'MMMM YYYY') AS month_year_text,
    CASE 
        WHEN EXTRACT(MONTH FROM m."ReportingPeriod") IN (1,2,3) THEN 'Q1'
        WHEN EXTRACT(MONTH FROM m."ReportingPeriod") IN (4,5,6) THEN 'Q2'
        WHEN EXTRACT(MONTH FROM m."ReportingPeriod") IN (7,8,9) THEN 'Q3'
        ELSE 'Q4'
    END AS quarter_name,
    m."ReportingPeriod" AS sort_order
FROM metric_values m
ORDER BY m."ReportingPeriod" DESC;
```

**Utilisation PBI:**
```dax
// Slicer : vw_dim_date[month_year_text]
// Filter: vw_dim_date[year] = 2025
// Sort: vw_dim_date[sort_order]
```

---

## 2️⃣ VUE : vw_2a1_leaderboard (Classement par Shipper)

```sql
-- Purpose: Top performers by read performance %
-- Audience: Schedule 2A (Industry - Anonymised)
-- Aggregation: Max value per shipper/product/month

CREATE OR REPLACE VIEW vw_2a1_leaderboard AS
WITH ranked_data AS (
    SELECT
        TO_CHAR(m."ReportingPeriod"::timestamp, 'YYYY-MM') AS report_month,
        m."ReportingPeriod" AS report_date,
        COALESCE(sa."AliasCode", s.short_code) AS shipper_alias,
        m."ProductClassCode" AS product_class,
        
        MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) AS read_perf_pct,
        MAX(CASE WHEN m."MetricKey" = 'estimated_read_pct' THEN m."Value" END) AS estimated_pct,
        MAX(CASE WHEN m."MetricKey" = 'total_site_count' THEN m."Value" END) AS total_sites,
        
        ROW_NUMBER() OVER (
            PARTITION BY m."ReportingPeriod", m."ProductClassCode"
            ORDER BY MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) DESC
        ) AS rank_in_class,
        
        ROW_NUMBER() OVER (
            PARTITION BY m."ReportingPeriod"
            ORDER BY MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) DESC
        ) AS rank_overall
        
    FROM metric_values m
    INNER JOIN shippers s ON m."ShipperShortCode" = s.short_code AND s."IsDeleted" = false
    LEFT JOIN "shipperAlias" sa ON sa."ShipperId" = s."Id" AND sa."IsDeleted" = false
    WHERE m."MetricKey" IN ('read_performance_pct', 'estimated_read_pct', 'total_site_count')
    GROUP BY m."ReportingPeriod", m."ProductClassCode", sa."AliasCode", s.short_code, m."ShipperShortCode"
)
SELECT
    report_month,
    report_date,
    shipper_alias,
    product_class,
    read_perf_pct,
    estimated_pct,
    total_sites,
    rank_in_class,
    rank_overall,
    CASE 
        WHEN read_perf_pct >= 97.5 THEN 'Compliant'
        WHEN read_perf_pct >= 90.0 THEN 'At Risk'
        ELSE 'Non-Compliant'
    END AS compliance_status
FROM ranked_data
WHERE rank_in_class <= 50  -- Top 50 per product class
ORDER BY report_date DESC, rank_in_class;
```

**Utilisation PBI:**
```dax
// Table visual: Top 10 by month
// Filter: rank_in_class <= 10
// Sort: rank_in_class ASC
```

---

## 3️⃣ VUE : vw_2a1_distribution (Histogramme de Distribution)

```sql
-- Purpose: Distribution histogram (% bins)
-- Shows: How many shippers in each performance band
-- Example: 5 shippers in 90-95% band, 12 in 95-100% band

CREATE OR REPLACE VIEW vw_2a1_distribution AS
WITH binned_data AS (
    SELECT
        m."ReportingPeriod",
        TO_CHAR(m."ReportingPeriod"::timestamp, 'YYYY-MM') AS report_month,
        m."ProductClassCode" AS product_class,
        
        MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) AS read_perf_pct,
        
        CASE
            WHEN MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) < 70 THEN '0-70%'
            WHEN MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) < 80 THEN '70-80%'
            WHEN MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) < 90 THEN '80-90%'
            WHEN MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) < 95 THEN '90-95%'
            WHEN MAX(CASE WHEN m."MetricKey" = 'read_performance_pct' THEN m."Value" END) < 97.5 THEN '95-97.5%'
            ELSE '97.5-100%'
        END AS perf_bin,
        
        m."ShipperShortCode"
    FROM metric_values m
    WHERE m."MetricKey" = 'read_performance_pct'
    GROUP BY m."ReportingPeriod", m."ProductClassCode", m."ShipperShortCode"
)
SELECT
    "ReportingPeriod",
    report_month,
    product_class,
    perf_bin,
    COUNT(DISTINCT "ShipperShortCode") AS shipper_count,
    ROUND(100.0 * COUNT(DISTINCT "ShipperShortCode") / 
        SUM(COUNT(DISTINCT "ShipperShortCode")) OVER (PARTITION BY "ReportingPeriod", product_class), 2) AS percentage
FROM binned_data
GROUP BY "ReportingPeriod", report_month, product_class, perf_bin
ORDER BY "ReportingPeriod" DESC, product_class, 
    CASE perf_bin
        WHEN '0-70%' THEN 1
        WHEN '70-80%' THEN 2
        WHEN '80-90%' THEN 3
        WHEN '90-95%' THEN 4
        WHEN '95-97.5%' THEN 5
        ELSE 6
    END;
```

**Utilisation PBI:**
```dax
// Bar Chart: perf_bin (X-axis) vs shipper_count (Y-axis)
// Tooltip: percentage
// Filter: current month
```

---

## 4️⃣ VUE : vw_2a2_no_meter (Sites sans Compteur)

```sql
-- Purpose: No-meter sites analysis by class & shipper
-- Metric: Cumulative no-read counts (1yr, 2yr, 3yr, 4yr)

CREATE OR REPLACE VIEW vw_2a2_no_meter AS
SELECT
    m."ReportingPeriod",
    TO_CHAR(m."ReportingPeriod"::timestamp, 'YYYY-MM') AS report_month,
    s.short_code AS shipper_code,
    COALESCE(sa."AliasCode", s.short_code) AS shipper_alias,
    m."ProductClassCode" AS product_class,
    
    MAX(CASE WHEN m."MetricKey" = 'no_meter_spr_count' THEN m."Value" END)::INT AS no_meter_sites,
    MAX(CASE WHEN m."MetricKey" = 'no_read_count_1yr' THEN m."Value" END)::INT AS no_read_1yr,
    MAX(CASE WHEN m."MetricKey" = 'no_read_count_2yr' THEN m."Value" END)::INT AS no_read_2yr,
    MAX(CASE WHEN m."MetricKey" = 'no_read_count_3yr' THEN m."Value" END)::INT AS no_read_3yr,
    MAX(CASE WHEN m."MetricKey" = 'no_read_count_4yr' THEN m."Value" END)::INT AS no_read_4yr,
    
    MAX(CASE WHEN m."MetricKey" = 'total_site_count' THEN m."Value" END) AS total_sites,
    
    ROUND(
        100.0 * MAX(CASE WHEN m."MetricKey" = 'no_read_count_4yr' THEN m."Value" END) / 
        NULLIF(MAX(CASE WHEN m."MetricKey" = 'total_site_count' THEN m."Value" END), 0),
        2
    ) AS no_read_4yr_pct
    
FROM metric_values m
INNER JOIN shippers s ON m."ShipperShortCode" = s.short_code AND s."IsDeleted" = false
LEFT JOIN "shipperAlias" sa ON sa."ShipperId" = s."Id" AND sa."IsDeleted" = false
WHERE m."MetricKey" IN (
    'no_meter_spr_count', 
    'no_read_count_1yr', 'no_read_count_2yr', 'no_read_count_3yr', 'no_read_count_4yr',
    'total_site_count'
)
GROUP BY m."ReportingPeriod", m."ProductClassCode", s.short_code, sa."AliasCode", m."ShipperShortCode"
ORDER BY m."ReportingPeriod" DESC, no_read_4yr DESC;
```

**Utilisation PBI:**
```dax
// Table: shipper_alias | product_class | no_read_4yr | no_read_4yr_pct
// Sort: no_read_4yr DESC (problematic sites first)
// Tooltip: no_read_1yr, no_read_2yr, no_read_3yr
```

---

## 📋 Script d'Installation Complet

```sql
-- Run these 4 CREATE VIEW statements in PostgreSQL

-- ✅ Step 1: Create vw_dim_date
CREATE OR REPLACE VIEW vw_dim_date AS
SELECT ... /* Copy from section 1 above */

-- ✅ Step 2: Create vw_2a1_leaderboard
CREATE OR REPLACE VIEW vw_2a1_leaderboard AS
SELECT ... /* Copy from section 2 above */

-- ✅ Step 3: Create vw_2a1_distribution
CREATE OR REPLACE VIEW vw_2a1_distribution AS
SELECT ... /* Copy from section 3 above */

-- ✅ Step 4: Create vw_2a2_no_meter
CREATE OR REPLACE VIEW vw_2a2_no_meter AS
SELECT ... /* Copy from section 4 above */

-- ✅ Verify views
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_type = 'VIEW'
AND table_name LIKE 'vw_%' OR table_name LIKE 'v_%'
ORDER BY table_name;

-- Expected output:
-- fact_read_performance
-- v_parr_industry
-- v_parr_pac
-- vw_2a1_distribution
-- vw_2a1_leaderboard
-- vw_2a2_no_meter
-- vw_dim_date
-- vw_dim_shipper
```

---

## 🔍 Validation Queries

```sql
-- Check vw_dim_date
SELECT * FROM vw_dim_date LIMIT 10;
-- Expected: 5-12 rows (one per month in data)

-- Check vw_2a1_leaderboard
SELECT * FROM vw_2a1_leaderboard LIMIT 20;
-- Expected: Top shippers ranked by performance

-- Check vw_2a1_distribution
SELECT * FROM vw_2a1_distribution LIMIT 10;
-- Expected: Histogram bins with counts

-- Check vw_2a2_no_meter
SELECT * FROM vw_2a2_no_meter LIMIT 10;
-- Expected: No-meter analysis by shipper
```

---

## 📊 Performance Testing

```sql
-- Test query performance (should be < 1 second)

EXPLAIN ANALYZE
SELECT * FROM v_parr_industry 
WHERE "ReportingPeriod" = '2025-04-30'
LIMIT 1000;

EXPLAIN ANALYZE
SELECT * FROM vw_2a1_leaderboard
WHERE report_date = '2025-04-30'
LIMIT 100;

-- If slow (> 1 sec), add indexes:
CREATE INDEX idx_metric_values_period ON metric_values("ReportingPeriod");
CREATE INDEX idx_metric_values_key ON metric_values("MetricKey");
```

---

## 🔐 Security Notes

- **View: v_parr_industry** → Use for Schedule 2A (Anonymised) — Real shipper name NOT exposed
- **View: v_parr_pac** → Use for Schedule 2B (Non-Anonymised) — Real shipper name exposed, apply RLS in Power BI
- **View: vw_2a1_leaderboard** → Use only with alias codes (anonymised)
- **View: vw_2a2_no_meter** → Can use alias or real name depending on audience

---

## ✅ Checklist

- [ ] All 4 new views created successfully
- [ ] Existing views (fact_read_performance, v_parr_industry, v_parr_pac, vw_dim_shipper) verified
- [ ] Performance tests passed (< 1 sec per query)
- [ ] Sample data validated
- [ ] Ready for Power BI import
