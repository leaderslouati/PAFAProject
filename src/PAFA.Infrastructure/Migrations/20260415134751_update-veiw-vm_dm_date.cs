using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updateveiwvm_dm_date : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_dim_date;");

            // Nouvelle migration
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_dim_date AS
                SELECT DISTINCT
                    to_char(""ReportingPeriod""::timestamp, 'YYYY-MM') AS date_key,
                    to_char(""ReportingPeriod""::timestamp, 'Mon-YY')  AS month_year,
                    EXTRACT(YEAR  FROM ""ReportingPeriod"")::int        AS year,
                    EXTRACT(MONTH FROM ""ReportingPeriod"")::int        AS month_num
                FROM metric_values;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_dim_date;");
        }
    }
}
