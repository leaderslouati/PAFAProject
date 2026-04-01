using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddViewsForReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─────────────────────────────────────────────────────────────────
            // 1. Improve vw_dim_shipper — add alias_code join for Power BI
            //    The LEFT JOIN ensures rows still appear for shippers without alias.
            // ─────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_dim_shipper AS
                SELECT
                    s.short_code                        AS shipper_code,
                    s.name                              AS real_shipper_name,
                    COALESCE(sa.""AliasCode"", s.short_code) AS alias_code,
                    s.is_active
                FROM shippers s
                LEFT JOIN ""shipperAlias"" sa
                    ON sa.""ShipperId"" = s.""Id""
                    AND sa.""IsDeleted"" = false
                WHERE s.""IsDeleted"" = false;
            ");

            ///fact_read_performance
            migrationBuilder.Sql("DROP VIEW IF EXISTS fact_read_performance ");
            migrationBuilder.Sql(@"
                CREATE VIEW fact_read_performance AS
                SELECT 
                    to_char(m.""ReportingPeriod""::timestamp, 'YYYY-MM') AS report_month,
                    m.""ReportingPeriod"" AS report_date,
                    m.""ShipperShortCode"" AS shipper_code,
                    m.""ProductClassCode"" AS product_class,

                    MAX(CASE WHEN m.""MetricKey"" = 'ReadPerfPct' THEN m.""Value"" END) AS read_perf_pct,
                    MAX(CASE WHEN m.""MetricKey"" = 'EstimatedPct' THEN m.""Value"" END) AS estimated_pct,
                    MAX(CASE WHEN m.""MetricKey"" = 'CheckReadCount' THEN m.""Value"" END) AS check_read_count,
                    MAX(CASE WHEN m.""MetricKey"" = 'TotalSites' THEN m.""Value"" END) AS total_sites,

                    CASE 
                        WHEN MAX(CASE WHEN m.""MetricKey"" = 'ReadPerfPct' THEN m.""Value"" END) >= pc.""MinReadPercentage"" 
                        THEN 1 ELSE 0 
                    END AS is_compliant

                FROM metric_values m

                LEFT JOIN product_classes pc 
                    ON m.""ProductClassCode"" = pc.""Code""

                GROUP BY 
                    m.""ReportingPeriod"", 
                    m.""ShipperShortCode"", 
                    m.""ProductClassCode"",
                    pc.""MinReadPercentage"";"
                ); 
            // ─────────────────────────────────────────────────────────────────
            // 2. v_parr_industry — Schedule 2A — ANONYMISED
            //    ⚠️ COMPLIANCE RULE: real_shipper_name MUST NOT appear here.
            //    Only alias_code is exposed. This view is the Power BI source
            //    for all Industry / Shipper-facing reports.
            // ─────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_parr_industry;");
            migrationBuilder.Sql(@"
                CREATE VIEW v_parr_industry AS
                SELECT
                    COALESCE(sa.""AliasCode"", s.short_code) AS shipper_code,
                    fp.report_month,
                    fp.report_date,
                    fp.product_class,
                    fp.read_perf_pct,
                    fp.estimated_pct,
                    fp.check_read_count,
                    fp.total_sites,
                    fp.is_compliant,
                    COALESCE(pc.""MinReadPercentage"", 97.5)  AS unc_threshold
                FROM fact_read_performance fp
                INNER JOIN shippers s
                    ON s.short_code = fp.shipper_code
                   AND s.""IsDeleted"" = false
                LEFT JOIN ""shipperAlias"" sa
                    ON sa.""ShipperId"" = s.""Id""
                   AND sa.""IsDeleted"" = false
                LEFT JOIN product_classes pc
                    ON pc.""Code"" = fp.product_class;
            ");

            // ─────────────────────────────────────────────────────────────────
            // 3. v_parr_pac — Schedule 2B — NON-ANONYMISED
            //    Exposes real_shipper_name. Restricted to PAC + PAFA roles.
            //    Power BI RLS enforces access via EffectiveIdentity.
            // ─────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_parr_pac;");
            migrationBuilder.Sql(@"
                CREATE VIEW v_parr_pac AS
                SELECT
                    s.short_code                             AS shipper_code,
                    s.name                                   AS real_shipper_name,
                    fp.report_month,
                    fp.report_date,
                    fp.product_class,
                    fp.read_perf_pct,
                    fp.estimated_pct,
                    fp.check_read_count,
                    fp.total_sites,
                    fp.is_compliant,
                    COALESCE(pc.""MinReadPercentage"", 97.5)  AS unc_threshold
                FROM fact_read_performance fp
                INNER JOIN shippers s
                    ON s.short_code = fp.shipper_code
                   AND s.""IsDeleted"" = false
                LEFT JOIN product_classes pc
                    ON pc.""Code"" = fp.product_class;
            ");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_dim_date;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_2a1_leaderboard;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_2a1_distribution;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_2a2_no_meter;");

            // vw_dim_date — inchangée, elle est correcte
            migrationBuilder.Sql(@"
        CREATE VIEW vw_dim_date AS
        SELECT DISTINCT
            ""ReportingPeriod""                                AS date_key,
            to_char(""ReportingPeriod""::timestamp, 'Mon-YY') AS month_year,
            EXTRACT(YEAR  FROM ""ReportingPeriod"")::int       AS year,
            EXTRACT(MONTH FROM ""ReportingPeriod"")::int       AS month_num
        FROM metric_values;
    ");

            // vw_2a1_leaderboard — corrigé :
            // - ds.short_code → ds.alias_code
            // - mom_change supprimé (pas dans fact_read_performance)
            migrationBuilder.Sql(@"
        CREATE VIEW vw_2a1_leaderboard AS
        SELECT
            fp.report_month,
            fp.product_class,
            ds.shipper_code                                       AS shipper_code,
            fp.estimated_pct,
            RANK() OVER (PARTITION BY fp.report_month, fp.product_class
                         ORDER BY fp.estimated_pct DESC NULLS LAST) AS rank_worst,
            RANK() OVER (PARTITION BY fp.report_month, fp.product_class
                         ORDER BY fp.estimated_pct ASC  NULLS LAST) AS rank_best
        FROM fact_read_performance fp
        JOIN vw_dim_shipper ds ON ds.shipper_code = fp.shipper_code;
    ");

            // vw_2a1_distribution — corrigé :
            // - GROUP BY avec expression CASE dupliquée (PostgreSQL exige ça)
            migrationBuilder.Sql(@"
        CREATE VIEW vw_2a1_distribution AS
        SELECT
            fp.report_month,
            fp.product_class,
            CASE
                WHEN fp.estimated_pct <  10 THEN '00-10%'
                WHEN fp.estimated_pct <  20 THEN '10-20%'
                WHEN fp.estimated_pct <  30 THEN '20-30%'
                WHEN fp.estimated_pct <  40 THEN '30-40%'
                WHEN fp.estimated_pct <  50 THEN '40-50%'
                WHEN fp.estimated_pct <  60 THEN '50-60%'
                WHEN fp.estimated_pct <  70 THEN '60-70%'
                WHEN fp.estimated_pct <  80 THEN '70-80%'
                WHEN fp.estimated_pct <  90 THEN '80-90%'
                ELSE '90-100%'
            END                AS pct_bin,
            COUNT(*)           AS shipper_count
        FROM fact_read_performance fp
        WHERE fp.estimated_pct IS NOT NULL
        GROUP BY
            fp.report_month,
            fp.product_class,
            CASE
                WHEN fp.estimated_pct <  10 THEN '00-10%'
                WHEN fp.estimated_pct <  20 THEN '10-20%'
                WHEN fp.estimated_pct <  30 THEN '20-30%'
                WHEN fp.estimated_pct <  40 THEN '30-40%'
                WHEN fp.estimated_pct <  50 THEN '40-50%'
                WHEN fp.estimated_pct <  60 THEN '50-60%'
                WHEN fp.estimated_pct <  70 THEN '60-70%'
                WHEN fp.estimated_pct <  80 THEN '70-80%'
                WHEN fp.estimated_pct <  90 THEN '80-90%'
                ELSE '90-100%'
            END;
    ");

            // vw_2a2_no_meter — corrigé :
            // - "shipperProductClasses" → shipper_product_classes
            // - "productClasses"        → product_classes
            // - colonnes en snake_case selon tes migrations précédentes
            migrationBuilder.Sql(@"
        CREATE VIEW vw_2a2_no_meter AS
        SELECT
            spc.""ReportingPeriod""                  AS report_date,
            to_char(spc.""ReportingPeriod""::timestamp, 'YYYY-MM') AS report_month,
            ds.shipper_code                           AS shipper_code,
            pc.""Code""                              AS product_class,
            
            spc.""SupplyPointCount""                 AS sp_total,
            COALESCE(spc.""NoMeterPct"", 0)          AS no_meter_pct,
            RANK() OVER (
                PARTITION BY spc.""ReportingPeriod"", pc.""Code""
                ORDER BY COALESCE(spc.""NoMeterPct"", 0) DESC
            )                                       AS rank_worst
        FROM shipper_product_classes spc
        JOIN product_classes pc   ON pc.""Id""      = spc.""ProductClassId""
        JOIN shippers s           ON s.""Id""       = spc.""ShipperId""
        JOIN vw_dim_shipper ds    ON ds.shipper_code = s.short_code
        WHERE s.""IsDeleted"" = false;
    ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_2a2_no_meter;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_2a1_distribution;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_2a1_leaderboard;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_dim_date;");
        }
    }
}
