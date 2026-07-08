-- ═══════════════════════════════════════════════════════════════════════════════
-- PAFA SQL — Schedule 2A : Vues Complètes (2A.3 à 2A.19)
-- Database  : PostgreSQL 14+
-- Prérequis : sql/01-create-tables.sql + sql/02-create-views-powerbi.sql
-- Exécution : psql -U pafa -d pafadb -f sql/03-views-2a-complete.sql
-- ═══════════════════════════════════════════════════════════════════════════════

SET search_path TO public;

-- ── Helper CTE réutilisable pour le contexte shipper anonymisé ───────────────
-- NOTE: Les vues 2A utilisent toujours l'alias anonymisé (jamais le nom réel).

-- ═══════════════════════════════════════════════════════════════════════════════
-- MAPPING OFFICIEL : Files/PARR Reports - Mapping.xlsx
--   • 2A.1-2A.10   → MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
--   • 2A.11a-11b   → EUC09_Reporting_PAC_YYYY_MM (CDSP/SharePoint)
--   • 2A.12a-12c   → Class 4 Read Performance (DDP)
--   • 2A.13        → AQ at Risk MMM YYYY For PAFA (CDSP/SharePoint)
--   • 2A.14        → Confirmed Energy Theft Claim/Withdrawal objections (CDSP/SharePoint)
--   • 2A.15        → Supply Points Reclassified to Class 4 (DDP)
--   • 2A.16        → Supply Points with Minimum Threshold (DDP)
--   • 2A.17        → IGT Must Read - PARR Reports (DDP)
--   • 2A.18        → 2B.21 Corrective Opening Meter Reading Rejections (CDSP/SharePoint)
--   • 2A.19        → PARR Reports / Class 4 Vacant Sites (DDP)
-- ═══════════════════════════════════════════════════════════════════════════════

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.3 — No Meter Recorded and Data Flows Received
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- NB : Correspond à "2A.3 No Meter Recorded and data flows received" dans le mapping
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a3_read_performance_industry AS
WITH shipper_ctx AS (
    SELECT s.short_code,
           COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
),
shipper_data AS (
    SELECT
        m.reporting_period,
        TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month,
        sc.shipper_alias,
        m.product_class_code,
        MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) AS read_perf_pct,
        MAX(CASE WHEN m.metric_key = 'estimated_read_pct'   THEN m.value END) AS estimated_pct,
        MAX(CASE WHEN m.metric_key = 'total_site_count'     THEN m.value END) AS total_sites,
        CASE
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 97.5 THEN 'Compliant'
            WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 90.0 THEN 'At Risk'
            ELSE 'Non-Compliant'
        END AS compliance_status
    FROM metric_values m
    INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
    WHERE m.is_deleted = FALSE
      AND m.report_code IN ('2A.3','2A.1')
      AND m.metric_key IN ('read_performance_pct','estimated_read_pct','total_site_count')
    GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code
),
industry_avg AS (
    SELECT
        reporting_period,
        report_month,
        'INDUSTRY AVERAGE' AS shipper_alias,
        product_class_code,
        ROUND(AVG(read_perf_pct)::NUMERIC, 3)  AS read_perf_pct,
        ROUND(AVG(estimated_pct)::NUMERIC, 3)   AS estimated_pct,
        SUM(total_sites)                         AS total_sites,
        'Average'                                AS compliance_status
    FROM shipper_data
    GROUP BY reporting_period, report_month, product_class_code
)
SELECT reporting_period, report_month, shipper_alias, product_class_code,
       read_perf_pct, estimated_pct, total_sites, compliance_status,
       FALSE AS is_industry_average
FROM shipper_data
UNION ALL
SELECT reporting_period, report_month, shipper_alias, product_class_code,
       read_perf_pct, estimated_pct, total_sites, compliance_status,
       TRUE AS is_industry_average
FROM industry_avg
ORDER BY reporting_period DESC, is_industry_average, read_perf_pct DESC NULLS LAST;

COMMENT ON VIEW vw_2a3_read_performance_industry IS 'Schedule 2A.3 — Read performance agrégée industrie + par shipper (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.4 — Shipper Transfer Read Performance
-- Source : TRANSFER (Shipper Transfer Read Performance.xlsx)
-- Métriques : transfer_read_succ_pct, transfer_read_total
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a4_transfer_read AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                          AS report_month,
    sc.shipper_alias,
    MAX(CASE WHEN m.metric_key = 'transfer_read_succ_pct' THEN m.value END)         AS transfer_read_pct,
    MAX(CASE WHEN m.metric_key = 'transfer_read_total'    THEN m.value END)::BIGINT AS transfer_total,
    CASE
        WHEN MAX(CASE WHEN m.metric_key = 'transfer_read_succ_pct' THEN m.value END) >= 97.5 THEN 'Compliant'
        WHEN MAX(CASE WHEN m.metric_key = 'transfer_read_succ_pct' THEN m.value END) >= 90.0 THEN 'At Risk'
        ELSE 'Non-Compliant'
    END AS compliance_status
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.4'
  AND m.metric_key IN ('transfer_read_succ_pct','transfer_read_total')
GROUP BY m.reporting_period, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, transfer_read_pct DESC NULLS LAST;

COMMENT ON VIEW vw_2a4_transfer_read IS 'Schedule 2A.4 — Shipper Transfer Read Performance (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.5 — Read Performance by Shipper (détail par product class)
-- Source : MOD520A / Read Performance by Shipper.xlsx
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a5_read_performance AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                      AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END)       AS read_perf_pct,
    MAX(CASE WHEN m.metric_key = 'total_site_count'     THEN m.value END)::BIGINT AS total_sites,
    COALESCE(pc.min_read_percentage, 97.5)                                       AS unc_threshold,
    CASE
        WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >=
             COALESCE(pc.min_read_percentage, 97.5) THEN 'Compliant'
        ELSE 'Non-Compliant'
    END AS compliance_status,
    ROW_NUMBER() OVER (
        PARTITION BY m.reporting_period, m.product_class_code
        ORDER BY MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) DESC NULLS LAST
    ) AS rank_in_class
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
LEFT JOIN product_classes pc ON pc.code = m.product_class_code AND pc.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2A.5','2A.1')
  AND m.metric_key IN ('read_performance_pct','total_site_count')
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code, pc.min_read_percentage
ORDER BY m.reporting_period DESC, m.product_class_code, rank_in_class;

COMMENT ON VIEW vw_2a5_read_performance IS 'Schedule 2A.5 — Read performance par shipper et product class (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.6 — Meter Read Validity Monitoring (MRE Rejection Codes)
-- Source : EUC09_Reporting_PAC
-- Dimensions : MRE code (via lookup_values), shipper, period
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a6_meter_validity AS
WITH shipper_ctx AS (
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
    -- MRE rejection codes
    MAX(CASE WHEN m.metric_key = 'mre01026_pct' THEN m.value END) AS mre_01026_pct,
    MAX(CASE WHEN m.metric_key = 'mre01027_pct' THEN m.value END) AS mre_01027_pct,
    MAX(CASE WHEN m.metric_key = 'mre01028_pct' THEN m.value END) AS mre_01028_pct,
    MAX(CASE WHEN m.metric_key = 'mre01029_pct' THEN m.value END) AS mre_01029_pct,
    MAX(CASE WHEN m.metric_key = 'mre01030_pct' THEN m.value END) AS mre_01030_pct,
    MAX(CASE WHEN m.metric_key = 'invalid_read_count' THEN m.value END)::BIGINT AS invalid_read_count
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.6'
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, sc.shipper_alias;

COMMENT ON VIEW vw_2a6_meter_validity IS 'Schedule 2A.6 — Meter read validity monitoring (MRE rejection codes, anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.7 — No Reads by Years (par tranche EUC)
-- Source : RPT_1364 / MOD520A
-- Dimensions : euc_code (band), product_class, shipper
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a7_no_reads AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                        AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    m.euc_code,
    eb.description                                                                  AS euc_description,
    MAX(CASE WHEN m.metric_key = 'no_read_count_1yr' THEN m.value END)::BIGINT    AS no_read_1yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_2yr' THEN m.value END)::BIGINT    AS no_read_2yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_3yr' THEN m.value END)::BIGINT    AS no_read_3yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_4yr' THEN m.value END)::BIGINT    AS no_read_4yr,
    MAX(CASE WHEN m.metric_key = 'total_site_count'  THEN m.value END)::BIGINT    AS total_sites
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
LEFT JOIN euc_bands eb ON eb."EucCode" = m.euc_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.7'
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code, m.euc_code, eb.description
ORDER BY m.reporting_period DESC, sc.shipper_alias, m.euc_code;

COMMENT ON VIEW vw_2a7_no_reads IS 'Schedule 2A.7 — No reads par tranche EUC et années (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.8 — AQ Corrections by Reason
-- Source : RPT_1364_PARR AQ
-- Dimensions : reason (via lookup_values), shipper, period
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a8_aq_corrections AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')   AS report_month,
    sc.shipper_alias,
    lv."Label"                               AS correction_reason,
    lv."Code"                                AS reason_code,
    SUM(m.value)::BIGINT                     AS correction_count
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
LEFT JOIN lookup_values lv ON lv."LookupId" = m."LookupValueId"
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.8'
  AND m.metric_key IN ('aq_correction_count','aq_correction_reason_01')
GROUP BY m.reporting_period, sc.shipper_alias, m.shipper_short_code, lv."Label", lv."Code"
ORDER BY m.reporting_period DESC, correction_count DESC;

COMMENT ON VIEW vw_2a8_aq_corrections IS 'Schedule 2A.8 — Corrections AQ par raison (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.9 — Standard Correction Factors
-- Source : EUC09
-- Dimensions : euc_code, product_class, shipper
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a9_standard_cf AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                             AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    m.euc_code,
    eb.description                                                                      AS euc_description,
    SUM(m.value)::BIGINT                                                               AS std_cf_site_count
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
LEFT JOIN euc_bands eb ON eb."EucCode" = m.euc_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.9'
  AND m.metric_key = 'std_corr_factor_count'
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code, m.euc_code, eb.description
ORDER BY m.reporting_period DESC, sc.shipper_alias, m.euc_code;

COMMENT ON VIEW vw_2a9_standard_cf IS 'Schedule 2A.9 — Standard Correction Factors par EUC et shipper (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.10 — Replaced Reads
-- Source : EUC09
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a10_replaced_reads AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                      AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    m.euc_code,
    eb.description                                                               AS euc_description,
    SUM(m.value)::BIGINT                                                        AS replaced_read_count
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
LEFT JOIN euc_bands eb ON eb."EucCode" = m.euc_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.10'
  AND m.metric_key = 'replaced_read_count'
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code, m.euc_code, eb.description
ORDER BY m.reporting_period DESC, replaced_read_count DESC;

COMMENT ON VIEW vw_2a10_replaced_reads IS 'Schedule 2A.10 — Replaced Reads par EUC et shipper (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.11 — Supply Points with Minimum Percentage Requirement
-- Source : Supply Points with Min Pct Req (PC2, PC3)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a11_min_pct AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                        AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'min_pct_req_pct'      THEN m.value END)        AS min_pct_requirement,
    MAX(CASE WHEN m.metric_key = 'min_pct_not_met_count' THEN m.value END)::INT  AS sites_not_meeting_min,
    MAX(CASE WHEN m.metric_key = 'total_site_count'      THEN m.value END)::BIGINT AS total_sites,
    MAX(CASE WHEN m.metric_key = 'read_performance_pct'  THEN m.value END)        AS actual_read_perf_pct,
    ROUND(
        100.0 * MAX(CASE WHEN m.metric_key = 'min_pct_not_met_count' THEN m.value END) /
        NULLIF(MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END), 0),
        2
    ) AS pct_not_meeting_min
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.11'
  AND m.metric_key IN ('min_pct_req_pct','min_pct_not_met_count','total_site_count','read_performance_pct')
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, sites_not_meeting_min DESC NULLS LAST;

COMMENT ON VIEW vw_2a11_min_pct IS 'Schedule 2A.11 — Supply points with minimum % requirement (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.12a — AQ Obligations : Class 1 Above Threshold
-- Source : RPT_1364
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a12a_aq_class1 AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                              AS report_month,
    sc.shipper_alias,
    MAX(CASE WHEN m.metric_key = 'class1_above_thresh_count'  THEN m.value END)::INT   AS class1_above_thresh_count,
    MAX(CASE WHEN m.metric_key = 'class1_above_thresh_aq_gwh' THEN m.value END)        AS class1_above_thresh_aq_gwh,
    MAX(CASE WHEN m.metric_key = 'class1_reclassified_count'  THEN m.value END)::INT   AS class1_reclassified_count
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.12a'
GROUP BY m.reporting_period, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, class1_above_thresh_aq_gwh DESC NULLS LAST;

COMMENT ON VIEW vw_2a12a_aq_class1 IS 'Schedule 2A.12a — AQ Obligations Class 1 Above Threshold (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.12b — AQ Obligations : Read Performance by Class
-- Source : RPT_1364 / MOD520A
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a12b_aq_supply AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                                AS report_month,
    sc.shipper_alias,
    MAX(CASE WHEN m.metric_key = 'class1_thresh_sites'          THEN m.value END)::INT   AS class1_thresh_sites,
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_monthly_293k_pct' THEN m.value END)       AS aq_read_perf_monthly_293k_pct,
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_smart_amr_pct'   THEN m.value END)        AS aq_read_perf_smart_amr_pct,
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_annual_pct'      THEN m.value END)        AS aq_read_perf_annual_pct
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.12b'
GROUP BY m.reporting_period, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, sc.shipper_alias;

COMMENT ON VIEW vw_2a12b_aq_supply IS 'Schedule 2A.12b — AQ read performance par classe (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.13 — AQ at Risk
-- Source : AQ at Risk.xlsx / RPT_1364
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a13_aq_at_risk AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                    AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'aq_at_risk_gwh' THEN m.value END)          AS aq_at_risk_gwh,
    MAX(CASE WHEN m.metric_key = 'aq_at_risk_pct' THEN m.value END)          AS aq_at_risk_pct,
    MAX(CASE WHEN m.metric_key = 'aq_overdue_count' THEN m.value END)::INT   AS aq_overdue_count,
    CASE
        WHEN MAX(CASE WHEN m.metric_key = 'aq_at_risk_pct' THEN m.value END) >= 5.0 THEN 'High Risk'
        WHEN MAX(CASE WHEN m.metric_key = 'aq_at_risk_pct' THEN m.value END) >= 2.0 THEN 'Medium Risk'
        ELSE 'Low Risk'
    END AS risk_level
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.13'
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, aq_at_risk_gwh DESC NULLS LAST;

COMMENT ON VIEW vw_2a13_aq_at_risk IS 'Schedule 2A.13 — AQ at Risk par shipper (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.14 — Class 3 Conversion (due to low read submission)
-- Source : Class 3 conversion*.xlsx
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a14_class3_conv AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                       AS report_month,
    sc.shipper_alias,
    MAX(CASE WHEN m.metric_key = 'class3_conv_count'  THEN m.value END)::INT    AS class3_conv_count,
    MAX(CASE WHEN m.metric_key = 'class3_conv_aq_gwh' THEN m.value END)         AS class3_conv_aq_gwh,
    MAX(CASE WHEN m.metric_key = 'class3_conv_pct'    THEN m.value END)         AS class3_conv_pct
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.14'
GROUP BY m.reporting_period, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, class3_conv_count DESC NULLS LAST;

COMMENT ON VIEW vw_2a14_class3_conv IS 'Schedule 2A.14 — Class 3 conversion due to low read submission (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.15 — Energy Theft Claims (Confirmed objections P41/P106)
-- Source : Confirmed Energy Theft Claim objections*.xlsx
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a15_theft_claims AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                         AS report_month,
    sc.shipper_alias,
    MAX(CASE WHEN m.metric_key = 'energy_theft_count'    THEN m.value END)::INT   AS theft_claim_count,
    MAX(CASE WHEN m.metric_key = 'theft_claim_obj_pct'   THEN m.value END)        AS theft_claim_obj_pct,
    MAX(CASE WHEN m.metric_key = 'theft_claim_energy_pct' THEN m.value END)       AS theft_claim_energy_pct
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.15'
GROUP BY m.reporting_period, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, theft_claim_count DESC NULLS LAST;

COMMENT ON VIEW vw_2a15_theft_claims IS 'Schedule 2A.15 — Energy Theft Claim objections (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.16 — Energy Theft Withdrawal Objections
-- Source : Confirmed Energy Theft Withdrawal objections*.xlsx
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a16_theft_wd AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                          AS report_month,
    sc.shipper_alias,
    MAX(CASE WHEN m.metric_key = 'theft_objection_count' THEN m.value END)::INT    AS theft_wd_count,
    MAX(CASE WHEN m.metric_key = 'theft_wd_obj_pct'      THEN m.value END)         AS theft_wd_obj_pct
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.16'
GROUP BY m.reporting_period, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, theft_wd_count DESC NULLS LAST;

COMMENT ON VIEW vw_2a16_theft_wd IS 'Schedule 2A.16 — Energy Theft Withdrawal objections (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.17 — MPRN Must Read (Removed, Count, Age bucket)
-- Source : Report 1/2/3 - Percentage/Count MPRN removed from Must Read.xlsx
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a17_mprn_must_read AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                           AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    lv."Label"                                                                        AS age_bucket,
    MAX(CASE WHEN m.metric_key = 'mprn_removed_pct'    THEN m.value END)            AS mprn_removed_pct,
    MAX(CASE WHEN m.metric_key = 'must_read_age_pct'   THEN m.value END)            AS must_read_age_pct,
    MAX(CASE WHEN m.metric_key = 'mprn_entering_count' THEN m.value END)::BIGINT    AS mprn_entering_count
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
LEFT JOIN lookup_values lv ON lv."LookupId" = m."LookupValueId"
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.17'
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code, lv."Label"
ORDER BY m.reporting_period DESC, sc.shipper_alias, m.product_class_code;

COMMENT ON VIEW vw_2a17_mprn_must_read IS 'Schedule 2A.17 — MPRN removed from Must Read (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.18 — Vacant Sites Set In-Month
-- Source : Report 1A - Sites set to Vacant within the month.xlsx
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a18_vacant_in_month AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
)
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                           AS report_month,
    sc.shipper_alias,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'vacant_in_month_count' THEN m.value END)::BIGINT  AS vacant_in_month_count,
    MAX(CASE WHEN m.metric_key = 'total_site_count'      THEN m.value END)::BIGINT  AS total_sites,
    ROUND(
        100.0 * MAX(CASE WHEN m.metric_key = 'vacant_in_month_count' THEN m.value END) /
        NULLIF(MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END), 0),
        2
    ) AS vacant_in_month_pct
FROM metric_values m
INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
WHERE m.is_deleted = FALSE
  AND m.report_code = '2A.18'
GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code
ORDER BY m.reporting_period DESC, vacant_in_month_count DESC NULLS LAST;

COMMENT ON VIEW vw_2a18_vacant_in_month IS 'Schedule 2A.18 — Vacant sites set in-month (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2A.19 — Vacant Sites End of Period (Count + Proportion)
-- Source : Report 1B + Report 2 - Proportion.xlsx
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2a19_vacant_eod AS
WITH shipper_ctx AS (
    SELECT s.short_code, COALESCE(sa.alias_code, s.short_code) AS shipper_alias
    FROM shippers s
    LEFT JOIN shipper_alias sa ON sa.shipper_id = s.id
        AND sa.is_active = TRUE AND (sa.valid_to IS NULL OR sa.valid_to > now())
    WHERE s.is_deleted = FALSE
),
ranked AS (
    SELECT
        m.reporting_period,
        TO_CHAR(m.reporting_period, 'YYYY-MM')                                             AS report_month,
        sc.shipper_alias,
        m.product_class_code,
        lv."Label"                                                                          AS age_band,
        MAX(CASE WHEN m.metric_key = 'vacant_eod_count'         THEN m.value END)::BIGINT AS vacant_eod_count,
        MAX(CASE WHEN m.metric_key = 'vacant_proportion_age_pct' THEN m.value END)        AS vacant_proportion_pct,
        MAX(CASE WHEN m.metric_key = 'total_site_count'          THEN m.value END)::BIGINT AS total_sites
    FROM metric_values m
    INNER JOIN shipper_ctx sc ON m.shipper_short_code = sc.short_code
    LEFT JOIN lookup_values lv ON lv."LookupId" = m."LookupValueId"
    WHERE m.is_deleted = FALSE
      AND m.report_code = '2A.19'
    GROUP BY m.reporting_period, m.product_class_code, sc.shipper_alias, m.shipper_short_code, lv."Label"
)
SELECT *,
    ROW_NUMBER() OVER (
        PARTITION BY reporting_period, product_class_code
        ORDER BY vacant_eod_count DESC NULLS LAST
    ) AS rank_in_class
FROM ranked
ORDER BY reporting_period DESC, vacant_eod_count DESC NULLS LAST;

COMMENT ON VIEW vw_2a19_vacant_eod IS 'Schedule 2A.19 — Vacant sites end-of-period with proportion (anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- Vérification
-- ═══════════════════════════════════════════════════════════════════════════════
SELECT table_name
FROM information_schema.views
WHERE table_schema = 'public' AND table_name LIKE 'vw_2a%'
ORDER BY table_name;
