-- ═══════════════════════════════════════════════════════════════════════════════
-- PAFA SQL Schema — Phase 2: Create Views for Power BI
-- Database: PostgreSQL 14+
-- Script Date: 2026-06-14
-- ═══════════════════════════════════════════════════════════════════════════════

SET search_path TO public;

-- ═══════════════════════════════════════════════════════════════════════════════
-- VUE 1️⃣ : vw_dim_date (Date Dimension)
-- Purpose: Time-based dimension for reporting
-- Used in: Slicers, filters, trend analysis
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_dim_date AS
SELECT
    DISTINCT
    m.reporting_period AS date_id,
    EXTRACT(YEAR FROM m.reporting_period)::INT AS year,
    EXTRACT(MONTH FROM m.reporting_period)::INT AS month,
    EXTRACT(QUARTER FROM m.reporting_period)::INT AS quarter,
    TO_CHAR(m.reporting_period, 'YYYY-MM') AS year_month,
    TO_CHAR(m.reporting_period, 'YYYY-"Q"Q') AS year_quarter,
    TO_CHAR(m.reporting_period, 'FMMonth YYYY') AS month_year_text,
    CASE 
        WHEN EXTRACT(MONTH FROM m.reporting_period) IN (1,2,3) THEN 'Q1'
        WHEN EXTRACT(MONTH FROM m.reporting_period) IN (4,5,6) THEN 'Q2'
        WHEN EXTRACT(MONTH FROM m.reporting_period) IN (7,8,9) THEN 'Q3'
        ELSE 'Q4'
    END AS quarter_name,
    m.reporting_period AS sort_order
FROM metric_values m
ORDER BY m.reporting_period DESC;

COMMENT ON VIEW vw_dim_date IS 'Date dimension — supports temporal slicing in Power BI';

-- ═══════════════════════════════════════════════════════════════════════════════
-- VUE 2️⃣ : vw_dim_shipper (Shipper Dimension - with Anonymisation)
-- Purpose: Shipper master with current alias
-- Used in: Shipper filter, ranking context
-- Security: Exposes alias code instead of real name for Schedule 2A (Industry)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_dim_shipper AS
SELECT
    s.id AS shipper_id,
    s.short_code AS shipper_code,
    s.name AS shipper_real_name,
    COALESCE(sa.alias_code, s.short_code) AS shipper_alias,
    s.is_active,
    s.portfolio_size,
    s.market_entry_date,
    s.market_exit_date,
    CASE WHEN sa.id IS NOT NULL THEN TRUE ELSE FALSE END AS has_active_alias
FROM shippers s
LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id 
    AND sa.is_active = TRUE 
    AND (sa.valid_to IS NULL OR sa.valid_to > now())
WHERE s.is_deleted = FALSE
ORDER BY s.short_code;

COMMENT ON VIEW vw_dim_shipper IS 'Shipper dimension with anonymisation support for Schedule 2A';

-- ═══════════════════════════════════════════════════════════════════════════════
-- VUE 3️⃣ : fact_read_performance (CORE Fact Table for Power BI)
-- Purpose: Monthly read performance facts by shipper/product
-- Aggregation: MAX per shipper/product/month
-- Used in: All Schedule 2A & 2B reports
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW fact_read_performance AS
SELECT
    m.reporting_period::DATE AS report_month,
    TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month_key,
    m.shipper_short_code,
    m.product_class_code,
    
    -- Read Performance Metrics
    MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END)::NUMERIC(6,3) AS read_perf_pct,
    MAX(CASE WHEN m.metric_key = 'estimated_read_pct' THEN m.value END)::NUMERIC(6,3) AS estimated_pct,
    MAX(CASE WHEN m.metric_key = 'check_read_count' THEN m.value END)::BIGINT AS check_read_count,
    MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END)::BIGINT AS total_sites,
    
    -- Compliance Flag (derived: read_perf >= 97.5%)
    CASE 
        WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 97.5 THEN 1
        ELSE 0
    END AS is_compliant,
    
    -- No-Meter Analysis
    MAX(CASE WHEN m.metric_key = 'no_meter_spr_count' THEN m.value END)::INT AS no_meter_sites,
    MAX(CASE WHEN m.metric_key = 'no_read_count_4yr' THEN m.value END)::INT AS no_read_4yr,
    
    -- Data Quality
    COUNT(*) AS metric_count,
    MAX(m.created_at) AS last_updated
    
FROM metric_values m
WHERE m.is_deleted = FALSE
GROUP BY 
    m.reporting_period,
    m.shipper_short_code,
    m.product_class_code
ORDER BY m.reporting_period DESC, m.shipper_short_code, m.product_class_code;

COMMENT ON VIEW fact_read_performance IS 'Core fact table for Power BI — monthly read performance by shipper/product';

-- ═══════════════════════════════════════════════════════════════════════════════
-- VUE 4️⃣ : v_parr_industry (Schedule 2A — ANONYMISED)
-- Purpose: Industry peer comparison with alias codes (no real shipper names)
-- Audience: Schedule 2A (Industry stakeholders)
-- Aggregation: Max per shipper/product/month with rank
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW v_parr_industry AS
WITH shipper_context AS (
    SELECT
        s.short_code,
        COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id 
        AND sa.is_active = TRUE 
        AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
),
ranked_data AS (
    SELECT
        m.reporting_period,
        TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month,
        sc.shipper_alias,
        m.product_class_code,
        
        MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) AS read_perf_pct,
        MAX(CASE WHEN m.metric_key = 'estimated_read_pct' THEN m.value END) AS estimated_pct,
        MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END) AS total_sites,
        
        CASE 
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 97.5 THEN 'Compliant'
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 90.0 THEN 'At Risk'
            ELSE 'Non-Compliant'
        END AS compliance_status,
        
        ROW_NUMBER() OVER (
            PARTITION BY m.reporting_period, m.product_class_code
            ORDER BY MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) DESC NULLS LAST
        ) AS rank_in_product_class,
        
        ROW_NUMBER() OVER (
            PARTITION BY m.reporting_period
            ORDER BY MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) DESC NULLS LAST
        ) AS rank_overall
        
    FROM metric_values m
    INNER JOIN shipper_context sc ON m.shipper_short_code = sc.short_code
    WHERE m.is_deleted = FALSE
        AND m.metric_key IN ('read_performance_pct', 'estimated_read_pct', 'total_site_count')
    GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code
)
SELECT
    reporting_period,
    report_month,
    shipper_alias,
    product_class_code,
    read_perf_pct,
    estimated_pct,
    total_sites,
    compliance_status,
    rank_in_product_class,
    rank_overall
FROM ranked_data
WHERE rank_in_product_class <= 50  -- Top 50 per product class
ORDER BY reporting_period DESC, rank_in_product_class;

COMMENT ON VIEW v_parr_industry IS 'Schedule 2A Industry Comparison (Anonymised with aliases)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- VUE 5️⃣ : v_parr_pac (Schedule 2B — NON-ANONYMISED)
-- Purpose: Performance Assurance Committee view with real shipper names
-- Audience: Schedule 2B (PAC stakeholders — apply RLS in Power BI)
-- Security: Real shipper names exposed — requires RLS at Power BI level
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW v_parr_pac AS
WITH shipper_names AS (
    SELECT
        s.short_code,
        s.name AS shipper_real_name
    FROM shippers s
    WHERE s.is_deleted = FALSE
),
ranked_data AS (
    SELECT
        m.reporting_period,
        TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month,
        sn.shipper_real_name,
        m.product_class_code,
        
        MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) AS read_perf_pct,
        MAX(CASE WHEN m.metric_key = 'estimated_read_pct' THEN m.value END) AS estimated_pct,
        MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END) AS total_sites,
        
        CASE 
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 97.5 THEN 'Compliant'
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 90.0 THEN 'At Risk'
            ELSE 'Non-Compliant'
        END AS compliance_status,
        
        ROW_NUMBER() OVER (
            PARTITION BY m.reporting_period, m.product_class_code
            ORDER BY MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) DESC NULLS LAST
        ) AS rank_in_product_class,
        
        ROW_NUMBER() OVER (
            PARTITION BY m.reporting_period
            ORDER BY MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) DESC NULLS LAST
        ) AS rank_overall
        
    FROM metric_values m
    INNER JOIN shipper_names sn ON m.shipper_short_code = sn.short_code
    WHERE m.is_deleted = FALSE
        AND m.metric_key IN ('read_performance_pct', 'estimated_read_pct', 'total_site_count')
    GROUP BY m.reporting_period, m.product_class_code, sn.shipper_real_name, m.shipper_short_code
)
SELECT
    reporting_period,
    report_month,
    shipper_real_name,
    product_class_code,
    read_perf_pct,
    estimated_pct,
    total_sites,
    compliance_status,
    rank_in_product_class,
    rank_overall
FROM ranked_data
WHERE rank_in_product_class <= 50  -- Top 50 per product class
ORDER BY reporting_period DESC, rank_in_product_class;

COMMENT ON VIEW v_parr_pac IS 'Schedule 2B PAC (Non-Anonymised with real names) — Apply RLS in Power BI';

-- ═══════════════════════════════════════════════════════════════════════════════
-- VUE 6️⃣ : vw_2a1_leaderboard (Schedule 2A Report 2A.1 — Leaderboard)
-- Purpose: Ranking view for "Estimated and Check Reads" leaderboard
-- Shows: Top performers ranked by read performance %
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a1_leaderboard AS
WITH shipper_context AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id 
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) AS read_perf_pct,
    MAX(CASE WHEN m.metric_key = 'estimated_read_pct' THEN m.value END) AS estimated_pct,
    MAX(CASE WHEN m.metric_key = 'check_read_count' THEN m.value END) AS check_read_count,
    MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END) AS total_sites,
    ROW_NUMBER() OVER (
        PARTITION BY m.reporting_period, m.product_class_code
        ORDER BY MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) DESC NULLS LAST
    ) AS rank_in_class
FROM metric_values m
INNER JOIN shipper_context sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, rank_in_class;

COMMENT ON VIEW vw_2a1_leaderboard IS 'Schedule 2A Report 2A.1 — Leaderboard ranking by read performance';

-- ═══════════════════════════════════════════════════════════════════════════════
-- VUE 7️⃣ : vw_2a1_distribution (Schedule 2A Report 2A.1 — Histogram)
-- Purpose: Distribution histogram showing count of shippers in each performance band
-- Used in: Histogram/bin analysis showing compliance distribution
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a1_distribution AS
WITH binned_data AS (
    SELECT
        m.reporting_period,
        TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month,
        m.product_class_code,
        m.shipper_short_code,
        
        MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) AS read_perf_pct,
        
        CASE
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) < 70 THEN '0-70%'
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) < 80 THEN '70-80%'
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) < 90 THEN '80-90%'
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) < 95 THEN '90-95%'
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) < 97.5 THEN '95-97.5%'
            ELSE '97.5-100%'
        END AS perf_bin
        
    FROM metric_values m
    WHERE m.is_deleted = FALSE AND m.metric_key = 'read_performance_pct'
    GROUP BY m.reporting_period, m.product_class_code, m.shipper_short_code
)
SELECT
    reporting_period,
    report_month,
    product_class_code,
    perf_bin,
    COUNT(DISTINCT shipper_short_code) AS shipper_count,
    ROUND(100.0 * COUNT(DISTINCT shipper_short_code) / 
        SUM(COUNT(DISTINCT shipper_short_code)) OVER (PARTITION BY reporting_period, product_class_code), 2) 
        AS percentage
FROM binned_data
GROUP BY reporting_period, report_month, product_class_code, perf_bin
ORDER BY reporting_period DESC, product_class_code, 
    CASE perf_bin
        WHEN '0-70%' THEN 1
        WHEN '70-80%' THEN 2
        WHEN '80-90%' THEN 3
        WHEN '90-95%' THEN 4
        WHEN '95-97.5%' THEN 5
        ELSE 6
    END;

COMMENT ON VIEW vw_2a1_distribution IS 'Schedule 2A Report 2A.1 — Distribution histogram of compliance';

-- ═══════════════════════════════════════════════════════════════════════════════
-- VUE 8️⃣ : vw_2a2_no_meter (Schedule 2A Report 2A.2 — No-Meter Analysis)
-- Purpose: Sites with no metering device, no-read cumulative counts
-- Shows: Problematic sites that have not been read in 1/2/3/4 years
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a2_no_meter AS
WITH shipper_context AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id 
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    
    MAX(CASE WHEN m.metric_key = 'no_meter_spr_count' THEN m.value END)::INT AS no_meter_sites,
    MAX(CASE WHEN m.metric_key = 'no_read_count_1yr' THEN m.value END)::INT AS no_read_1yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_2yr' THEN m.value END)::INT AS no_read_2yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_3yr' THEN m.value END)::INT AS no_read_3yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_4yr' THEN m.value END)::INT AS no_read_4yr,
    MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END) AS total_sites,
    
    ROUND(
        100.0 * MAX(CASE WHEN m.metric_key = 'no_read_count_4yr' THEN m.value END) / 
        NULLIF(MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END), 0),
        2
    ) AS no_read_4yr_pct
    
FROM metric_values m
INNER JOIN shipper_context sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
    AND m.metric_key IN ('no_meter_spr_count', 'no_read_count_1yr', 'no_read_count_2yr', 
                         'no_read_count_3yr', 'no_read_count_4yr', 'total_site_count')
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, no_read_4yr DESC;

COMMENT ON VIEW vw_2a2_no_meter IS 'Schedule 2A Report 2A.2 — No-meter and no-read analysis';

-- ═══════════════════════════════════════════════════════════════════════════════
-- Verification Query
-- ═══════════════════════════════════════════════════════════════════════════════
SELECT 
    table_name,
    table_schema
FROM information_schema.tables
WHERE table_schema = 'public' 
    AND (table_type = 'VIEW' OR table_name IN (
        'vw_dim_date', 'vw_dim_shipper', 'fact_read_performance',
        'v_parr_industry', 'v_parr_pac', 'vw_2a1_leaderboard',
        'vw_2a1_distribution', 'vw_2a2_no_meter'
    ))
ORDER BY table_name;

-- ═══════════════════════════════════════════════════════════════════════════════
-- End of Views Creation Script
-- ═══════════════════════════════════════════════════════════════════════════════
