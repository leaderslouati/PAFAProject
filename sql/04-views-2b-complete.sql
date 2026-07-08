-- ═══════════════════════════════════════════════════════════════════════════════
-- PAFA SQL — Schedule 2B : Vues Complètes (2B.1 à 2B.22)
-- Database  : PostgreSQL 14+
-- Prérequis : sql/01-create-tables.sql + sql/02-create-views-powerbi.sql
-- Exécution : psql -U pafa -d pafadb -f sql/04-views-2b-complete.sql
--
-- Source officielle du mapping : Files/PARR Reports - Mapping.xlsx
--   Feuille "2B PARR Reports - Non Anonymise"
--
-- RÈGLE : les vues 2B n'utilisent PAS l'alias anonymisé — elles exposent le NOM
--         RÉEL du shipper. La sécurité est appliquée au niveau Power BI (RLS).
-- ═══════════════════════════════════════════════════════════════════════════════

SET search_path TO public;

-- ── Helper : shipper avec nom réel (pour 2B) ────────────────────────────────
-- Pas de CTE partagé en SQL standard ; chaque vue répète le JOIN direct.

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.1 — Estimated & Check Reads (non-anonymisé)
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- Identique à 2A.1 mais avec shipper_real_name au lieu de shipper_alias
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b1_estimated_check_reads AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                             AS report_month,
    s.name                                                                              AS shipper_real_name,
    s.short_code                                                                        AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END)             AS read_perf_pct,
    MAX(CASE WHEN m.metric_key = 'estimated_read_pct'   THEN m.value END)             AS estimated_pct,
    MAX(CASE WHEN m.metric_key = 'check_read_count'     THEN m.value END)::BIGINT     AS check_read_count,
    MAX(CASE WHEN m.metric_key = 'total_site_count'     THEN m.value END)::BIGINT     AS total_sites,
    CASE
        WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 97.5 THEN 'Compliant'
        WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 90.0 THEN 'At Risk'
        ELSE 'Non-Compliant'
    END AS compliance_status
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.1','2A.1')
  AND m.metric_key IN ('read_performance_pct','estimated_read_pct','check_read_count','total_site_count')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, s.short_code;

COMMENT ON VIEW vw_2b1_estimated_check_reads IS 'Schedule 2B.1 — Estimated & Check Reads (non-anonymisé, noms réels)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.2 — No Meter Recorded in SP
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b2_no_meter AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                          AS report_month,
    s.name                                                                           AS shipper_real_name,
    s.short_code                                                                     AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'no_meter_spr_count'  THEN m.value END)::INT      AS no_meter_sites,
    MAX(CASE WHEN m.metric_key = 'total_site_count'    THEN m.value END)::BIGINT   AS total_sites,
    MAX(CASE WHEN m.metric_key = 'data_flows_received' THEN m.value END)::BIGINT   AS data_flows_received,
    ROUND(
        100.0 * MAX(CASE WHEN m.metric_key = 'no_meter_spr_count' THEN m.value END) /
        NULLIF(MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END), 0),
        2
    ) AS no_meter_pct
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.2','2A.2')
  AND m.metric_key IN ('no_meter_spr_count','total_site_count','data_flows_received')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, no_meter_sites DESC NULLS LAST;

COMMENT ON VIEW vw_2b2_no_meter IS 'Schedule 2B.2 — No Meter Recorded in SP (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.3 — No Meter Recorded and Data Flows Received
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b3_no_meter_dataflows AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                          AS report_month,
    s.name                                                                           AS shipper_real_name,
    s.short_code                                                                     AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'no_meter_spr_count'  THEN m.value END)::INT      AS no_meter_sites,
    MAX(CASE WHEN m.metric_key = 'data_flows_received' THEN m.value END)::BIGINT   AS data_flows_received,
    MAX(CASE WHEN m.metric_key = 'total_site_count'    THEN m.value END)::BIGINT   AS total_sites,
    ROUND(
        100.0 * MAX(CASE WHEN m.metric_key = 'data_flows_received' THEN m.value END) /
        NULLIF(MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END), 0),
        2
    ) AS data_flows_pct
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.3','2A.3')
  AND m.metric_key IN ('no_meter_spr_count','data_flows_received','total_site_count')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, s.short_code;

COMMENT ON VIEW vw_2b3_no_meter_dataflows IS 'Schedule 2B.3 — No Meter Recorded and Data Flows Received (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.4 — Shipper Transfer Read Performance
-- Source : MOD520A + Transfer Read Performance (CDSP/SharePoint & DDP)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b4_transfer_read AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                            AS report_month,
    s.name                                                                             AS shipper_real_name,
    s.short_code                                                                       AS shipper_code,
    MAX(CASE WHEN m.metric_key = 'transfer_read_succ_pct' THEN m.value END)          AS transfer_read_pct,
    MAX(CASE WHEN m.metric_key = 'transfer_read_total'    THEN m.value END)::BIGINT  AS transfer_total,
    CASE
        WHEN MAX(CASE WHEN m.metric_key = 'transfer_read_succ_pct' THEN m.value END) >= 97.5 THEN 'Compliant'
        WHEN MAX(CASE WHEN m.metric_key = 'transfer_read_succ_pct' THEN m.value END) >= 90.0 THEN 'At Risk'
        ELSE 'Non-Compliant'
    END AS compliance_status
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.4','2A.4')
  AND m.metric_key IN ('transfer_read_succ_pct','transfer_read_total')
GROUP BY m.reporting_period, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, transfer_read_pct DESC NULLS LAST;

COMMENT ON VIEW vw_2b4_transfer_read IS 'Schedule 2B.4 — Shipper Transfer Read Performance (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.5 — Read Performance
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b5_read_performance AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                           AS report_month,
    s.name                                                                            AS shipper_real_name,
    s.short_code                                                                      AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END)           AS read_perf_pct,
    MAX(CASE WHEN m.metric_key = 'total_site_count'     THEN m.value END)::BIGINT   AS total_sites,
    COALESCE(pc.min_read_percentage, 97.5)                                           AS unc_threshold,
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
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
LEFT JOIN product_classes pc ON pc.code = m.product_class_code AND pc.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.5','2A.5')
  AND m.metric_key IN ('read_performance_pct','total_site_count')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code, pc.min_read_percentage
ORDER BY m.reporting_period DESC, m.product_class_code, rank_in_class;

COMMENT ON VIEW vw_2b5_read_performance IS 'Schedule 2B.5 — Read Performance (non-anonymisé, noms réels)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.6 — Meter Read Validity Monitoring (MRE Codes)
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b6_meter_validity AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month,
    s.name                                 AS shipper_real_name,
    s.short_code                           AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'mre01026_pct' THEN m.value END) AS mre_01026_pct,
    MAX(CASE WHEN m.metric_key = 'mre01027_pct' THEN m.value END) AS mre_01027_pct,
    MAX(CASE WHEN m.metric_key = 'mre01028_pct' THEN m.value END) AS mre_01028_pct,
    MAX(CASE WHEN m.metric_key = 'mre01029_pct' THEN m.value END) AS mre_01029_pct,
    MAX(CASE WHEN m.metric_key = 'mre01030_pct' THEN m.value END) AS mre_01030_pct,
    MAX(CASE WHEN m.metric_key = 'invalid_read_count' THEN m.value END)::BIGINT AS invalid_read_count
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.6','2A.6')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, s.short_code;

COMMENT ON VIEW vw_2b6_meter_validity IS 'Schedule 2B.6 — Meter Read Validity Monitoring (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.7 — No Read 1,2,3 or 4 — Class 1/2/3/4 (split 4 tabs by class)
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b7_no_reads AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                          AS report_month,
    s.name                                                                           AS shipper_real_name,
    s.short_code                                                                     AS shipper_code,
    m.product_class_code,
    m.euc_code,
    eb.description                                                                   AS euc_description,
    MAX(CASE WHEN m.metric_key = 'no_read_count_1yr' THEN m.value END)::BIGINT     AS no_read_1yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_2yr' THEN m.value END)::BIGINT     AS no_read_2yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_3yr' THEN m.value END)::BIGINT     AS no_read_3yr,
    MAX(CASE WHEN m.metric_key = 'no_read_count_4yr' THEN m.value END)::BIGINT     AS no_read_4yr,
    MAX(CASE WHEN m.metric_key = 'total_site_count'  THEN m.value END)::BIGINT     AS total_sites
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
LEFT JOIN euc_bands eb ON eb."EucCode" = m.euc_code
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.7','2A.7')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code, m.euc_code, eb.description
ORDER BY m.reporting_period DESC, m.product_class_code, s.short_code;

COMMENT ON VIEW vw_2b7_no_reads IS 'Schedule 2B.7 — No Reads 1/2/3/4+ years par classe (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.8 — AQ Corrections by Reason Code
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b8_aq_corrections AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')   AS report_month,
    s.name                                   AS shipper_real_name,
    s.short_code                             AS shipper_code,
    lv."Label"                               AS correction_reason,
    lv."Code"                                AS reason_code,
    SUM(m.value)::BIGINT                     AS correction_count
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
LEFT JOIN lookup_values lv ON lv."LookupId" = m."LookupValueId"
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.8','2A.8')
  AND m.metric_key IN ('aq_correction_count','aq_correction_reason_01')
GROUP BY m.reporting_period, s.name, s.short_code, m.shipper_short_code, lv."Label", lv."Code"
ORDER BY m.reporting_period DESC, correction_count DESC;

COMMENT ON VIEW vw_2b8_aq_corrections IS 'Schedule 2B.8 — AQ Corrections by Reason Code (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.9 — Standard CF AQ > 732,000 kWh
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b9_standard_cf AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                           AS report_month,
    s.name                                                                            AS shipper_real_name,
    s.short_code                                                                      AS shipper_code,
    m.product_class_code,
    m.euc_code,
    eb.description                                                                    AS euc_description,
    SUM(m.value)::BIGINT                                                             AS std_cf_site_count
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
LEFT JOIN euc_bands eb ON eb."EucCode" = m.euc_code
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.9','2A.9')
  AND m.metric_key = 'std_corr_factor_count'
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code, m.euc_code, eb.description
ORDER BY m.reporting_period DESC, std_cf_site_count DESC NULLS LAST;

COMMENT ON VIEW vw_2b9_standard_cf IS 'Schedule 2B.9 — Standard CF AQ > 732,000 kWh (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.10 — Replaced Meter Reads
-- Source : MOD520A__PAF_Reports_MMMYY_Non Anonymised (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b10_replaced_reads AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                           AS report_month,
    s.name                                                                            AS shipper_real_name,
    s.short_code                                                                      AS shipper_code,
    m.product_class_code,
    m.euc_code,
    eb.description                                                                    AS euc_description,
    SUM(m.value)::BIGINT                                                             AS replaced_read_count
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
LEFT JOIN euc_bands eb ON eb."EucCode" = m.euc_code
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.10','2A.10')
  AND m.metric_key = 'replaced_read_count'
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code, m.euc_code, eb.description
ORDER BY m.reporting_period DESC, replaced_read_count DESC NULLS LAST;

COMMENT ON VIEW vw_2b10_replaced_reads IS 'Schedule 2B.10 — Replaced Meter Reads (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.11a à 2B.11h — AQ Portfolio Calculations
-- Source : Rpt_1364_PARR AQ report_YYYY-MM (CDSP/SharePoint)
-- Les 8 sous-onglets correspondent à différentes métriques du RPT_1364
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b11_aq_portfolio AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                           AS report_month,
    s.name                                                                            AS shipper_real_name,
    s.short_code                                                                      AS shipper_code,
    m.product_class_code,
    -- Tab 2B.11a : AQ Portfolio Calculation
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_monthly_293k_pct' THEN m.value END)  AS aq_perf_293k_pct,
    -- Tab 2B.11b : Increase
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_smart_amr_pct'    THEN m.value END)  AS aq_perf_smart_amr_pct,
    -- Tab 2B.11c : Decrease 12m
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_annual_pct'       THEN m.value END)  AS aq_perf_annual_pct,
    -- Tab 2B.11h : failure by reason
    MAX(CASE WHEN m.metric_key = 'aq_overdue_count'              THEN m.value END)::INT AS aq_overdue_count,
    MAX(CASE WHEN m.metric_key = 'aq_correction_count'           THEN m.value END)::INT AS aq_correction_count
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code LIKE '2B.11%'
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, s.short_code;

COMMENT ON VIEW vw_2b11_aq_portfolio IS 'Schedule 2B.11a-h — AQ Portfolio Calculations (Rpt_1364, non-anonymisé) — vue consolidée';

-- Vue dédiée par sous-onglet pour Power BI
CREATE OR REPLACE VIEW vw_2b11a_aq_portfolio AS
SELECT reporting_period, report_month, shipper_real_name, shipper_code, product_class_code,
       aq_perf_293k_pct AS aq_portfolio_pct
FROM vw_2b11_aq_portfolio WHERE aq_perf_293k_pct IS NOT NULL;

CREATE OR REPLACE VIEW vw_2b11b_aq_portfolio_inc AS
SELECT reporting_period, report_month, shipper_real_name, shipper_code, product_class_code,
       aq_correction_count AS aq_increase_count
FROM vw_2b11_aq_portfolio WHERE aq_correction_count IS NOT NULL;

COMMENT ON VIEW vw_2b11a_aq_portfolio IS 'Schedule 2B.11a — AQ Portfolio Calculation';
COMMENT ON VIEW vw_2b11b_aq_portfolio_inc IS 'Schedule 2B.11b — AQ Portfolio Calculation Increase';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.14a — Sites above Class 1 threshold (not in Class 1)
-- Source : EUC09_Reporting_PAC_YYYY_MM (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b14a_euc_class1_above AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                              AS report_month,
    s.name                                                                               AS shipper_real_name,
    s.short_code                                                                         AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'class1_above_thresh_count'   THEN m.value END)::INT  AS class1_above_thresh_count,
    MAX(CASE WHEN m.metric_key = 'class1_above_thresh_aq_gwh'  THEN m.value END)       AS class1_above_thresh_aq_gwh
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.14a','2A.11a')
  AND m.metric_key IN ('class1_above_thresh_count','class1_above_thresh_aq_gwh')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, class1_above_thresh_aq_gwh DESC NULLS LAST;

COMMENT ON VIEW vw_2b14a_euc_class1_above IS 'Schedule 2B.14a — Sites above Class 1 threshold (EUC09, non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.14b — Sites reclassified to Class 1 by Shipper and CDSP
-- Source : EUC09_Reporting_PAC_YYYY_MM (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b14b_euc_reclassified AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                             AS report_month,
    s.name                                                                              AS shipper_real_name,
    s.short_code                                                                        AS shipper_code,
    MAX(CASE WHEN m.metric_key = 'class1_reclassified_count' THEN m.value END)::INT   AS class1_reclassified_count
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.14b','2A.11b')
  AND m.metric_key = 'class1_reclassified_count'
GROUP BY m.reporting_period, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, class1_reclassified_count DESC NULLS LAST;

COMMENT ON VIEW vw_2b14b_euc_reclassified IS 'Schedule 2B.14b — Sites reclassified to Class 1 (EUC09, non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.15a/b/c — Class 4 Read Performance (Monthly + Annual)
-- Source : Class 4 Read Performance (DDP)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b15_class4_read AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                                AS report_month,
    s.name                                                                                 AS shipper_real_name,
    s.short_code                                                                           AS shipper_code,
    -- 2B.15a : Monthly % portfolio AQ (>= 293k)
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_monthly_293k_pct' THEN m.value END)       AS class4_monthly_293k_pct,
    -- 2B.15b : Monthly % portfolio AQ (Smart/AMR)
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_smart_amr_pct'    THEN m.value END)       AS class4_monthly_smart_pct,
    -- 2B.15c : Annual % portfolio AQ
    MAX(CASE WHEN m.metric_key = 'aq_read_perf_annual_pct'       THEN m.value END)       AS class4_annual_pct,
    MAX(CASE WHEN m.metric_key = 'class23_mpr_pct'               THEN m.value END)       AS class4_mpr_pct,
    MAX(CASE WHEN m.metric_key = 'class4_aq_read_pct'            THEN m.value END)       AS class4_aq_read_pct
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code LIKE '2B.15%'
GROUP BY m.reporting_period, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, class4_monthly_293k_pct DESC NULLS LAST;

COMMENT ON VIEW vw_2b15_class4_read IS 'Schedule 2B.15a/b/c — Class 4 Read Performance (DDP, non-anonymisé) — vue consolidée';

CREATE OR REPLACE VIEW vw_2b15a_class4_monthly_read AS
SELECT reporting_period, report_month, shipper_real_name, shipper_code, class4_monthly_293k_pct
FROM vw_2b15_class4_read WHERE class4_monthly_293k_pct IS NOT NULL;

CREATE OR REPLACE VIEW vw_2b15b_class4_monthly_read AS
SELECT reporting_period, report_month, shipper_real_name, shipper_code, class4_monthly_smart_pct
FROM vw_2b15_class4_read WHERE class4_monthly_smart_pct IS NOT NULL;

CREATE OR REPLACE VIEW vw_2b15c_class4_annual_read AS
SELECT reporting_period, report_month, shipper_real_name, shipper_code, class4_annual_pct
FROM vw_2b15_class4_read WHERE class4_annual_pct IS NOT NULL;

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.16 — Breakdown of AQ overdue a Meter Reading
-- Source : AQ at Risk MMM YYYY For PAFA (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b16_aq_overdue AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                   AS report_month,
    s.name                                                                    AS shipper_real_name,
    s.short_code                                                              AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'aq_at_risk_gwh'   THEN m.value END)       AS aq_at_risk_gwh,
    MAX(CASE WHEN m.metric_key = 'aq_at_risk_pct'   THEN m.value END)       AS aq_at_risk_pct,
    MAX(CASE WHEN m.metric_key = 'aq_overdue_count' THEN m.value END)::INT  AS aq_overdue_count,
    CASE
        WHEN MAX(CASE WHEN m.metric_key = 'aq_at_risk_pct' THEN m.value END) >= 5.0 THEN 'High Risk'
        WHEN MAX(CASE WHEN m.metric_key = 'aq_at_risk_pct' THEN m.value END) >= 2.0 THEN 'Medium Risk'
        ELSE 'Low Risk'
    END AS risk_level
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.16','2A.13')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, aq_at_risk_gwh DESC NULLS LAST;

COMMENT ON VIEW vw_2b16_aq_overdue IS 'Schedule 2B.16 — AQ overdue a Meter Reading (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.17 — Confirmed Energy Theft (Claims & Withdrawal objections)
-- Source : Confirmed Energy Theft Claim/Withdrawal objections_P41/P106 (CDSP/SharePoint)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b17_energy_theft AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                          AS report_month,
    s.name                                                                           AS shipper_real_name,
    s.short_code                                                                     AS shipper_code,
    MAX(CASE WHEN m.metric_key = 'energy_theft_count'     THEN m.value END)::INT   AS theft_claim_count,
    MAX(CASE WHEN m.metric_key = 'theft_objection_count'  THEN m.value END)::INT   AS theft_wd_count,
    MAX(CASE WHEN m.metric_key = 'theft_claim_obj_pct'    THEN m.value END)        AS theft_claim_obj_pct,
    MAX(CASE WHEN m.metric_key = 'theft_claim_energy_pct' THEN m.value END)        AS theft_claim_energy_pct,
    MAX(CASE WHEN m.metric_key = 'theft_wd_obj_pct'       THEN m.value END)        AS theft_wd_obj_pct
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.17','2A.14')
GROUP BY m.reporting_period, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, theft_claim_count DESC NULLS LAST;

COMMENT ON VIEW vw_2b17_energy_theft IS 'Schedule 2B.17 — Confirmed Energy Theft submissions & objections (non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.18 — Sites converted PC 2/3 → PC4 (low read submission)
-- Source : Supply Points Reclassified to Class 4 (PAC) — DDP
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b18_pc_reclassified AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                       AS report_month,
    s.name                                                                        AS shipper_real_name,
    s.short_code                                                                  AS shipper_code,
    MAX(CASE WHEN m.metric_key = 'pc2_to4_conv_count'  THEN m.value END)::INT   AS pc_to4_conv_count,
    MAX(CASE WHEN m.metric_key = 'class3_conv_count'   THEN m.value END)::INT   AS class3_conv_count,
    MAX(CASE WHEN m.metric_key = 'class3_conv_aq_gwh'  THEN m.value END)        AS class3_conv_aq_gwh,
    MAX(CASE WHEN m.metric_key = 'class3_conv_pct'     THEN m.value END)        AS class3_conv_pct
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.18','2A.15')
GROUP BY m.reporting_period, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, pc_to4_conv_count DESC NULLS LAST;

COMMENT ON VIEW vw_2b18_pc_reclassified IS 'Schedule 2B.18 — Sites converted PC2/3 to PC4 low read submission (DDP, non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.19 — Class 2/3 Individual Read Performance vs Minimum %
-- Source : Supply Points with Minimum Threshold (PAC) — DDP
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b19_min_threshold AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                         AS report_month,
    s.name                                                                          AS shipper_real_name,
    s.short_code                                                                    AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'min_pct_req_pct'       THEN m.value END)        AS min_pct_requirement,
    MAX(CASE WHEN m.metric_key = 'min_pct_not_met_count'  THEN m.value END)::INT  AS sites_not_meeting_min,
    MAX(CASE WHEN m.metric_key = 'total_site_count'       THEN m.value END)::BIGINT AS total_sites,
    MAX(CASE WHEN m.metric_key = 'read_performance_pct'   THEN m.value END)        AS actual_read_perf_pct
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.19','2A.16')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, sites_not_meeting_min DESC NULLS LAST;

COMMENT ON VIEW vw_2b19_min_threshold IS 'Schedule 2B.19 — Class 2/3 Read Performance vs Min % (DDP, non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.20 — IGT Must Read process — Known Meter Issue flag
-- Source : IGT Must Read - PARR Reports (DDP)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b20_igt_must_read AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                           AS report_month,
    s.name                                                                            AS shipper_real_name,
    s.short_code                                                                      AS shipper_code,
    MAX(CASE WHEN m.metric_key = 'igt_known_issue_count' THEN m.value END)::INT     AS igt_known_issue_count,
    MAX(CASE WHEN m.metric_key = 'mprn_removed_pct'      THEN m.value END)          AS mprn_removed_pct,
    MAX(CASE WHEN m.metric_key = 'must_read_age_pct'     THEN m.value END)          AS must_read_age_pct,
    MAX(CASE WHEN m.metric_key = 'mprn_entering_count'   THEN m.value END)::BIGINT  AS mprn_entering_count
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.20','2A.17')
GROUP BY m.reporting_period, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, igt_known_issue_count DESC NULLS LAST;

COMMENT ON VIEW vw_2b20_igt_must_read IS 'Schedule 2B.20 — IGT Must Read Known Meter Issue flag (DDP, non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.21 — Corrective Opening Meter Reading Rejections (COMR)
-- Source : 2B.21 Corrective Opening Meter Reading Rejections_MMM-YY (CDSP/SharePoint)
-- AUSSI source de la feuille 2A.18 (même fichier, anonymisé pour 2A)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b21_comr_rejections AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                           AS report_month,
    s.name                                                                            AS shipper_real_name,
    s.short_code                                                                      AS shipper_code,
    MAX(CASE WHEN m.metric_key = 'comr_count'              THEN m.value END)::INT   AS comr_count,
    MAX(CASE WHEN m.metric_key = 'comr_rejections'         THEN m.value END)::INT   AS comr_rejections,
    MAX(CASE WHEN m.metric_key = 'comr_reject_recv_pct'    THEN m.value END)        AS comr_reject_recv_pct,
    MAX(CASE WHEN m.metric_key = 'comr_reject_raised_pct'  THEN m.value END)        AS comr_reject_raised_pct
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.21','2A.18')
GROUP BY m.reporting_period, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, comr_rejections DESC NULLS LAST;

COMMENT ON VIEW vw_2b21_comr_rejections IS 'Schedule 2B.21 / 2A.18 — Corrective Opening Meter Reading Rejections (COMR, non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2B.22 — Class 4 Vacant Sites
-- Source : PARR Reports (DDP)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE OR REPLACE VIEW vw_2b22_vacant_sites AS
SELECT
    m.reporting_period,
    TO_CHAR(m.reporting_period, 'YYYY-MM')                                             AS report_month,
    s.name                                                                              AS shipper_real_name,
    s.short_code                                                                        AS shipper_code,
    m.product_class_code,
    MAX(CASE WHEN m.metric_key = 'class4_vacant_sites'       THEN m.value END)::BIGINT AS class4_vacant_sites,
    MAX(CASE WHEN m.metric_key = 'vacant_in_month_count'     THEN m.value END)::BIGINT AS vacant_in_month_count,
    MAX(CASE WHEN m.metric_key = 'vacant_eod_count'          THEN m.value END)::BIGINT AS vacant_eod_count,
    MAX(CASE WHEN m.metric_key = 'vacant_proportion_age_pct' THEN m.value END)         AS vacant_proportion_pct,
    MAX(CASE WHEN m.metric_key = 'total_site_count'          THEN m.value END)::BIGINT AS total_sites
FROM metric_values m
INNER JOIN shippers s ON s.short_code = m.shipper_short_code AND s.is_deleted = FALSE
WHERE m.is_deleted = FALSE
  AND m.report_code IN ('2B.22','2A.19')
GROUP BY m.reporting_period, m.product_class_code, s.name, s.short_code, m.shipper_short_code
ORDER BY m.reporting_period DESC, class4_vacant_sites DESC NULLS LAST;

COMMENT ON VIEW vw_2b22_vacant_sites IS 'Schedule 2B.22 — Class 4 Vacant Sites (DDP, non-anonymisé)';

-- ═══════════════════════════════════════════════════════════════════════════════
-- Vérification finale
-- ═══════════════════════════════════════════════════════════════════════════════
SELECT table_name
FROM information_schema.views
WHERE table_schema = 'public' AND table_name LIKE 'vw_2b%'
ORDER BY table_name;

SELECT
    'vw_2b1_estimated_check_reads' AS vue,  '2B.1 — Estimated & Check Reads'              AS rapport UNION ALL
SELECT 'vw_2b2_no_meter',                   '2B.2 — No Meter Recorded in SP'              UNION ALL
SELECT 'vw_2b3_no_meter_dataflows',         '2B.3 — No Meter + Data Flows'                UNION ALL
SELECT 'vw_2b4_transfer_read',              '2B.4 — Transfer Read Performance'            UNION ALL
SELECT 'vw_2b5_read_performance',           '2B.5 — Read Performance'                     UNION ALL
SELECT 'vw_2b6_meter_validity',             '2B.6 — Meter Read Validity Monitoring'       UNION ALL
SELECT 'vw_2b7_no_reads',                   '2B.7 — No Reads 1/2/3/4+ years'             UNION ALL
SELECT 'vw_2b8_aq_corrections',             '2B.8 — AQ Corrections by Reason'            UNION ALL
SELECT 'vw_2b9_standard_cf',                '2B.9 — Standard CF AQ > 732k kWh'           UNION ALL
SELECT 'vw_2b10_replaced_reads',            '2B.10 — Replaced Meter Reads'               UNION ALL
SELECT 'vw_2b11_aq_portfolio',              '2B.11a-h — AQ Portfolio (Rpt_1364)'         UNION ALL
SELECT 'vw_2b14a_euc_class1_above',         '2B.14a — Sites above Class 1 (EUC09)'       UNION ALL
SELECT 'vw_2b14b_euc_reclassified',         '2B.14b — Reclassified to Class 1 (EUC09)'   UNION ALL
SELECT 'vw_2b15_class4_read',               '2B.15a-c — Class 4 Read Perf (DDP)'         UNION ALL
SELECT 'vw_2b16_aq_overdue',                '2B.16 — AQ Overdue (AQ at Risk file)'       UNION ALL
SELECT 'vw_2b17_energy_theft',              '2B.17 — Energy Theft (Claim/WD objections)' UNION ALL
SELECT 'vw_2b18_pc_reclassified',           '2B.18 — PC 2/3 → PC4 Reclassification'     UNION ALL
SELECT 'vw_2b19_min_threshold',             '2B.19 — Class 2/3 vs Min % (DDP)'           UNION ALL
SELECT 'vw_2b20_igt_must_read',             '2B.20 — IGT Must Read (DDP)'                UNION ALL
SELECT 'vw_2b21_comr_rejections',           '2B.21 — COMR Rejections'                    UNION ALL
SELECT 'vw_2b22_vacant_sites',              '2B.22 — Class 4 Vacant Sites (DDP)'
ORDER BY vue;
