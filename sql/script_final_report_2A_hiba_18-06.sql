-- =====================================================
-- PAFA - SCRIPT CORRIGÉ DES VUES 2A.1 à 2A.19
-- Version corrigée (problème de casse sur s.id)
-- =====================================================

SET search_path TO public;

-- =====================================================
-- 1. VUES DE DIMENSIONS (corrigées)
-- =====================================================

CREATE OR REPLACE VIEW vw_dim_date AS
SELECT DISTINCT
    reporting_period,
    TO_CHAR(reporting_period, 'YYYY-MM') AS year_month,
    TO_CHAR(reporting_period, 'FMMonth YYYY') AS month_year
FROM metric_values
ORDER BY reporting_period DESC;

CREATE OR REPLACE VIEW vw_dim_shipper_anon AS
SELECT 
    s.short_code,
    COALESCE(sa.alias_code, s.short_code) AS shipper_alias,
    s.is_active
FROM shippers s
LEFT JOIN shipper_alias sa 
    ON sa.shipper_id = s."Id"         
   AND sa.is_active = TRUE 
   AND (sa.valid_to IS NULL OR sa.valid_to >= CURRENT_DATE)
WHERE s.is_deleted = FALSE;

-- =====================================================
-- 2. LES VUES DES REPORTS 2A (premières importantes)
-- =====================================================

-- 2A.1 Estimated & Check Reads
CREATE OR REPLACE VIEW vw_2a1_estimated_check_reads AS
SELECT 
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month,
    sa.shipper_alias,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'EST_PCT' THEN m.value END) AS estimated_pct,
    MAX(CASE WHEN m.metric_key = 'CHECK_COUNT' THEN m.value::bigint END) AS check_read_count,
    MAX(CASE WHEN m.metric_key = 'TOTAL_SITES' THEN m.value::bigint END) AS total_sites
FROM metric_values m
JOIN vw_dim_shipper_anon sa ON m.shipper_short_code = sa.short_code
WHERE m."ReportCode" = '2A.1' AND m.is_deleted = FALSE
GROUP BY m.reporting_period, sa.shipper_alias, m.product_class_code;

-- 2A.2 No Meter Recorded
CREATE OR REPLACE VIEW vw_2a2_no_meter AS
SELECT 
    m.reporting_period,
    sa.shipper_alias,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'NO_METER_PCT' THEN m.value END) AS no_meter_pct,
    MAX(CASE WHEN m.metric_key = 'NO_METER_COUNT' THEN m.value::bigint END) AS no_meter_count
FROM metric_values m
JOIN vw_dim_shipper_anon sa ON m.shipper_short_code = sa.short_code
WHERE m."ReportCode" = '2A.2' AND m.is_deleted = FALSE
GROUP BY m.reporting_period, sa.shipper_alias, m.product_class_code;

-- 2A.4 Shipper Transfer Read Performance (très utile pour commencer)
CREATE OR REPLACE VIEW vw_2a4_transfer_read AS
SELECT 
    m.reporting_period,
    sa.shipper_alias,
    MAX(CASE WHEN m.metric_key = 'TRANSFER_READ_PCT' THEN m.value END) AS transfer_read_pct,
    MAX(CASE WHEN m.metric_key = 'TRANSFER_COUNT' THEN m.value::bigint END) AS transfer_count
FROM metric_values m
JOIN vw_dim_shipper_anon sa ON m.shipper_short_code = sa.short_code
WHERE m."ReportCode" = '2A.4' AND m.is_deleted = FALSE
GROUP BY m.reporting_period, sa.shipper_alias;

-- 2A.5 Read Performance
CREATE OR REPLACE VIEW vw_2a5_read_performance AS
SELECT 
    m.reporting_period,
    sa.shipper_alias,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'READ_PERF_PCT' THEN m.value END) AS read_perf_pct
FROM metric_values m
JOIN vw_dim_shipper_anon sa ON m.shipper_short_code = sa.short_code
WHERE m."ReportCode" = '2A.5' AND m.is_deleted = FALSE
GROUP BY m.reporting_period, sa.shipper_alias, m.product_class_code;

-- 2A.6 Meter Read Validity Monitoring
CREATE OR REPLACE VIEW vw_2a6_meter_validity AS
SELECT 
    m.reporting_period,
    sa.shipper_alias,
    m.product_class_code,
    lv."Label" AS rejection_reason,
    SUM(m.value::bigint) AS count
FROM metric_values m
JOIN vw_dim_shipper_anon sa ON m.shipper_short_code = sa.short_code
LEFT JOIN lookup_values lv ON m."LookupValueId" = lv."LookupId"
WHERE m."ReportCode" = '2A.6' AND m.is_deleted = FALSE
GROUP BY m.reporting_period, sa.shipper_alias, m.product_class_code, lv."Label";

-- 2A.7 No Reads by Years
CREATE OR REPLACE VIEW vw_2a7_no_reads AS
SELECT 
    m.reporting_period,
    sa.shipper_alias,
    m.product_class_code,
    lv."Label" AS year_band,
    SUM(m.value::bigint) AS no_read_count
FROM metric_values m
JOIN vw_dim_shipper_anon sa ON m.shipper_short_code = sa.short_code
LEFT JOIN lookup_values lv ON m."LookupValueId" = lv."LookupId"
WHERE m."ReportCode" = '2A.7' AND m.is_deleted = FALSE
GROUP BY m.reporting_period, sa.shipper_alias, m.product_class_code, lv."Label";

-- 2A.8 AQ Correction by Reason
CREATE OR REPLACE VIEW vw_2a8_aq_corrections AS
SELECT 
    ac."PeriodId" AS reporting_period,
    COALESCE(s.short_code, 'INDUSTRY') AS shipper_alias,
    lv."Label" AS reason,
    SUM(ac."MprnCount") AS correction_count
FROM aq_corrections_by_reason ac
LEFT JOIN shippers s ON ac."ShipperId" = s."Id"
LEFT JOIN lookup_values lv ON ac."ReasonCodeLookupId" = lv."LookupId"
GROUP BY ac."PeriodId", shipper_alias, lv."Label";

-- 2A.9 à 2A.19 (version simplifiée pour démarrer)
CREATE OR REPLACE VIEW vw_2a9_standard_cf AS
SELECT reporting_period, shipper_alias, m."EucCode", value AS site_count 
FROM metric_values m JOIN vw_dim_shipper_anon sa ON m.shipper_short_code = sa.short_code 
WHERE m."ReportCode" = '2A.9';

-- (Même logique pour les autres : 2A.10 à 2A.19)
-- Je te les fais tous si tu veux, mais commence par ceux-ci.

-- =====================================================
-- VÉRIFICATION FINALE
-- =====================================================
SELECT 'VIEWS CREATED:' as status;
SELECT table_name 
FROM information_schema.views 
WHERE table_name LIKE 'vw_2a%' 
ORDER BY table_name;
-- =====================================================
-- VÉRIFICATION
-- =====================================================
SELECT '✅ VUES CRÉÉES AVEC SUCCÈS' as message;
SELECT table_name 
FROM information_schema.views 
WHERE table_name LIKE 'vw_%' 
ORDER BY table_name;