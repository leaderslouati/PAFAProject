using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingAndExportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure all base tables exist (if using migrations-first approach)
            // This migration assumes the following tables exist:
            // - shippers
            // - product_classes
            // - ingestion_jobs
            // - ingestion_files
            // - metric_values
            // - report_types
            // - reports
            //
            // If they don't, run sql/01-create-tables.sql directly in PostgreSQL.

            // Create or alter report_types table (if not exists)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS report_types (
                    id SERIAL PRIMARY KEY,
                    code VARCHAR(10) UNIQUE NOT NULL,
                    schedule_ref VARCHAR(20),
                    label VARCHAR(200) NOT NULL,
                    audience VARCHAR(20) NOT NULL,
                    report_count INTEGER NOT NULL DEFAULT 0,
                    is_active BOOLEAN NOT NULL DEFAULT TRUE,
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
                    created_by VARCHAR(100),
                    updated_at TIMESTAMP WITH TIME ZONE,
                    updated_by VARCHAR(100),
                    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                    row_version BYTEA
                );
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_reporttype_code ON report_types(code);
            ");

            // Create or alter reports table (if not exists)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS reports (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    report_type_id INTEGER NOT NULL REFERENCES report_types(id) ON DELETE RESTRICT,
                    schedule_number INTEGER NOT NULL,
                    title VARCHAR(500) NOT NULL,
                    reporting_period DATE NOT NULL,
                    audience VARCHAR(20) NOT NULL,
                    status VARCHAR(30) NOT NULL DEFAULT 'Pending',
                    generated_at TIMESTAMP WITH TIME ZONE,
                    published_at TIMESTAMP WITH TIME ZONE,
                    file_path_pdf VARCHAR(1000),
                    file_path_excel VARCHAR(1000),
                    file_path_pptx VARCHAR(1000),
                    commentary_text TEXT,
                    commentary_by VARCHAR(200),
                    observations_text TEXT,
                    observations_by VARCHAR(256),
                    observations_updated_at TIMESTAMP WITH TIME ZONE,
                    ingestion_job_id UUID REFERENCES ingestion_jobs(id) ON DELETE SET NULL,
                    is_baseline BOOLEAN NOT NULL DEFAULT FALSE,
                    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
                    created_by VARCHAR(100),
                    updated_at TIMESTAMP WITH TIME ZONE,
                    updated_by VARCHAR(100),
                    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                    row_version BYTEA
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_reports_period_type ON reports(reporting_period, report_type_id);
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_reports_status ON reports(status);
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_reports_audience ON reports(audience);
            ");

            // Seed ReportTypes if not already present
            migrationBuilder.Sql(@"
                INSERT INTO report_types (code, schedule_ref, label, audience, report_count, is_active, created_by)
                VALUES 
                    ('SCH2A', 'Schedule 2A', 'Industry Peer Comparison (Anonymised)', 'Industry', 19, TRUE, 'SYSTEM'),
                    ('SCH2B', 'Schedule 2B', 'Performance Assurance Committee (Non-Anonymised)', 'PAC', 22, TRUE, 'SYSTEM')
                ON CONFLICT (code) DO NOTHING;
            ");

            // Create Views for Power BI
            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_dim_date AS
                SELECT
                    DISTINCT
                    m.reporting_period AS date_id,
                    EXTRACT(YEAR FROM m.reporting_period)::INT AS year,
                    EXTRACT(MONTH FROM m.reporting_period)::INT AS month,
                    EXTRACT(QUARTER FROM m.reporting_period)::INT AS quarter,
                    TO_CHAR(m.reporting_period, 'YYYY-MM') AS year_month,
                    TO_CHAR(m.reporting_period, 'YYYY-""Q""Q') AS year_quarter,
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
            ");

            migrationBuilder.Sql(@"
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
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW fact_read_performance AS
                SELECT
                    m.reporting_period::DATE AS report_month,
                    TO_CHAR(m.reporting_period, 'YYYY-MM') AS report_month_key,
                    m.shipper_short_code,
                    m.product_class_code,
                    
                    MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END)::NUMERIC(6,3) AS read_perf_pct,
                    MAX(CASE WHEN m.metric_key = 'estimated_read_pct' THEN m.value END)::NUMERIC(6,3) AS estimated_pct,
                    MAX(CASE WHEN m.metric_key = 'check_read_count' THEN m.value END)::BIGINT AS check_read_count,
                    MAX(CASE WHEN m.metric_key = 'total_site_count' THEN m.value END)::BIGINT AS total_sites,
                    
                    CASE 
                        WHEN MAX(CASE WHEN m.metric_key = 'read_performance_pct' THEN m.value END) >= 97.5 THEN 1
                        ELSE 0
                    END AS is_compliant,
                    
                    MAX(CASE WHEN m.metric_key = 'no_meter_spr_count' THEN m.value END)::INT AS no_meter_sites,
                    MAX(CASE WHEN m.metric_key = 'no_read_count_4yr' THEN m.value END)::INT AS no_read_4yr,
                    
                    COUNT(*) AS metric_count,
                    MAX(m.created_at) AS last_updated
                    
                FROM metric_values m
                WHERE m.is_deleted = FALSE
                GROUP BY 
                    m.reporting_period,
                    m.shipper_short_code,
                    m.product_class_code
                ORDER BY m.reporting_period DESC, m.shipper_short_code, m.product_class_code;
            ");

            migrationBuilder.Sql(@"
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
                WHERE rank_in_product_class <= 50
                ORDER BY reporting_period DESC, rank_in_product_class;
            ");

            migrationBuilder.Sql(@"
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
                WHERE rank_in_product_class <= 50
                ORDER BY reporting_period DESC, rank_in_product_class;
            ");

            migrationBuilder.Sql(@"
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
            ");

            migrationBuilder.Sql(@"
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
            ");

            migrationBuilder.Sql(@"
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
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop views in reverse order
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_2a2_no_meter;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_2a1_distribution;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_2a1_leaderboard;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS v_parr_pac;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS v_parr_industry;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS fact_read_performance;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_dim_shipper;");
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS vw_dim_date;");

            // Drop tables if they were created by this migration (optional)
            // In practice, keep the tables for data integrity
            // Only drop views and seed data cleanup
        }
    }
}
