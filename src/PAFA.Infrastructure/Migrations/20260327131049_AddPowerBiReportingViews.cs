using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    public partial class AddPowerBiReportingViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Vue : Dimension Shipper
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_dim_shipper AS
                SELECT 
                    short_code AS shipper_code,
                    name AS real_shipper_name,
                    is_active
                    -- Si tu as géré l'Alias, tu pourras faire le JOIN ici plus tard
                FROM shippers
                WHERE ""IsDeleted"" = false;
            ");

            // 2. Vue : Dimension Product Class
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_dim_product_class AS
                SELECT 
                    ""Code"" AS product_class_code,
                    ""Description"" AS description,
                    ""AQThresholdLow"" AS aq_threshold_low,
                    ""MinReadPercentage"" AS min_read_percentage
                FROM product_classes
                WHERE ""IsActive"" = true;
            ");

            // 3. Vue : Fait Read Performance (Pivot des MetricValues)
            migrationBuilder.Sql("DROP VIEW IF EXISTS fact_read_performance;");

            migrationBuilder.Sql(@"
                CREATE VIEW fact_read_performance AS
                SELECT 
                    to_char(m.""ReportingPeriod""::timestamp, 'YYYY-MM') AS report_month,
                    m.""ReportingPeriod"" AS report_date,
                    m.""ShipperShortCode"" AS shipper_code,
                    m.product_class_code AS product_class,
                    MAX(CASE WHEN m.""MetricKey"" = 'ReadPerfPct' THEN m.""Value"" END) AS read_perf_pct,
                    MAX(CASE WHEN m.""MetricKey"" = 'EstimatedPct' THEN m.""Value"" END) AS estimated_pct,
                    MAX(CASE WHEN m.""MetricKey"" = 'CheckReadCount' THEN m.""Value"" END) AS check_read_count,
                    MAX(CASE WHEN m.""MetricKey"" = 'TotalSites' THEN m.""Value"" END) AS total_sites,
                    CASE 
                        WHEN MAX(CASE WHEN m.""MetricKey"" = 'ReadPerfPct' THEN m.""Value"" END) >= pc.""MinReadPercentage"" 
                        THEN 1 ELSE 0 
                    END AS is_compliant
                FROM metric_values m
                LEFT JOIN product_classes pc ON m.product_class_code = pc.""Code""
                GROUP BY 
                    m.""ReportingPeriod"", 
                    m.""ShipperShortCode"", 
                    m.product_class_code,
                    pc.""MinReadPercentage"";
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // En cas de Rollback (annulation de la migration), on supprime les vues.
            migrationBuilder.Sql("DROP VIEW IF EXISTS fact_read_performance;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_dim_product_class;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_dim_shipper;");
        }
    }
}