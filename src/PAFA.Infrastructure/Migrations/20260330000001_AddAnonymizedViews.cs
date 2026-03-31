// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Migrations/20260330000001_AddAnonymizedViews.cs
// PURPOSE: Create v_parr_industry (anonymised / Schedule 2A) and
//          v_parr_pac (non-anonymised / Schedule 2B) views.
//          Also improves vw_dim_shipper to expose alias_code.
// NOTE: SQL-only migration – no C# model changes.
// ═══════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnonymizedViews : Migration
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
                    COALESCE(sa.alias_code, s.short_code) AS alias_code,
                    s.is_active
                FROM shippers s
                LEFT JOIN ""shipperAlias"" sa
                    ON sa.""ShipperId"" = s.""Id""
                    AND sa.""IsDeleted"" = false
                WHERE s.""IsDeleted"" = false;
            ");

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
                    COALESCE(sa.alias_code, s.short_code) AS shipper_code,
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_parr_industry;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS v_parr_pac;");

            // Restore vw_dim_shipper to original (without alias_code)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_dim_shipper AS
                SELECT
                    short_code AS shipper_code,
                    name       AS real_shipper_name,
                    is_active
                FROM shippers
                WHERE ""IsDeleted"" = false;
            ");
        }
    }
}
