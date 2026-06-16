-- ═══════════════════════════════════════════════════════════════════════════════
-- PAFA SQL Schema — Phase 1: Create Core Tables
-- Database: PostgreSQL 14+
-- Script Date: 2026-06-14
-- ═══════════════════════════════════════════════════════════════════════════════

-- Set schema search path
SET search_path TO public;

-- ═══════════════════════════════════════════════════════════════════════════════
-- 1️⃣ SHIPPER (Referential Master)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS shippers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    short_code VARCHAR(20) UNIQUE NOT NULL,
    name VARCHAR(150) NOT NULL,
    legal_entity VARCHAR(150),
    email VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    market_entry_date DATE,
    market_exit_date DATE,
    portfolio_size INTEGER,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA
);

CREATE INDEX ix_shipper_short_code ON shippers(short_code);
CREATE INDEX ix_shipper_is_active ON shippers(is_active);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 2️⃣ PRODUCT_CLASS (Referential Master)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS product_classes (
    id SERIAL PRIMARY KEY,
    code VARCHAR(10) UNIQUE NOT NULL,
    description TEXT NOT NULL,
    aq_threshold_low NUMERIC(12,4),
    aq_threshold_high NUMERIC(12,4),
    min_read_percentage NUMERIC(6,3),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA
);

CREATE INDEX ix_pc_code ON product_classes(code);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 3️⃣ SHIPPER_PRODUCT_CLASS (Bridge / Many-to-Many)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS shipper_product_classes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    shipper_id UUID NOT NULL REFERENCES shippers(id) ON DELETE CASCADE,
    product_class_id SERIAL NOT NULL REFERENCES product_classes(id) ON DELETE CASCADE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA,
    UNIQUE(shipper_id, product_class_id)
);

CREATE INDEX ix_spc_shipper_id ON shipper_product_classes(shipper_id);
CREATE INDEX ix_spc_product_class_id ON shipper_product_classes(product_class_id);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 4️⃣ SHIPPER_ALIAS (Anonymisation for Schedule 2A)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS shipper_alias (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    shipper_id UUID NOT NULL REFERENCES shippers(id) ON DELETE CASCADE,
    alias_code VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    valid_from TIMESTAMP WITH TIME ZONE,
    valid_to TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA
);

CREATE INDEX ix_shipper_alias_shipper_id ON shipper_alias(shipper_id);
CREATE INDEX ix_shipper_alias_code ON shipper_alias(alias_code);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 5️⃣ INGESTION_JOB (Orchestration)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS ingestion_jobs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_name VARCHAR(100) NOT NULL,
    reporting_period DATE NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'Started',
    files_expected INTEGER,
    files_downloaded INTEGER NOT NULL DEFAULT 0,
    files_processed INTEGER NOT NULL DEFAULT 0,
    files_failed INTEGER NOT NULL DEFAULT 0,
    records_loaded BIGINT NOT NULL DEFAULT 0,
    error_summary VARCHAR(2000),
    retry_count INTEGER NOT NULL DEFAULT 0,
    triggered_by VARCHAR(20),
    started_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    completed_at TIMESTAMP WITH TIME ZONE,
    parent_job_id UUID REFERENCES ingestion_jobs(id) ON DELETE SET NULL,
    correlation_id UUID,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA
);

CREATE INDEX ix_job_period ON ingestion_jobs(reporting_period);
CREATE INDEX ix_job_status ON ingestion_jobs(status);
CREATE INDEX ix_job_correlation_id ON ingestion_jobs(correlation_id);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 6️⃣ INGESTION_FILE (File Processing Audit)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS ingestion_files (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ingestion_job_id UUID NOT NULL REFERENCES ingestion_jobs(id) ON DELETE CASCADE,
    file_name VARCHAR(500) NOT NULL,
    source_system VARCHAR(20) NOT NULL,
    file_type VARCHAR(10),
    file_size_bytes BIGINT,
    blob_path VARCHAR(1000),
    file_hash VARCHAR(64),
    status VARCHAR(30) NOT NULL DEFAULT 'Downloaded',
    validation_status VARCHAR(30) NOT NULL DEFAULT 'Pending',
    rows_read INTEGER,
    rows_valid INTEGER,
    rows_rejected INTEGER,
    error_count INTEGER NOT NULL DEFAULT 0,
    downloaded_at TIMESTAMP WITH TIME ZONE,
    processed_at TIMESTAMP WITH TIME ZONE,
    last_modified_remote TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA
);

CREATE INDEX ix_file_hash ON ingestion_files(file_hash);
CREATE INDEX ix_file_job_id ON ingestion_files(ingestion_job_id);
CREATE INDEX ix_file_status ON ingestion_files(status);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 7️⃣ VALIDATION_ERROR (Detailed Validation Findings)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS validation_errors (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ingestion_file_id UUID NOT NULL REFERENCES ingestion_files(id) ON DELETE CASCADE,
    line_number INTEGER,
    column_name VARCHAR(100),
    error_code VARCHAR(50) NOT NULL,
    error_message TEXT NOT NULL,
    original_value TEXT,
    severity VARCHAR(20) NOT NULL DEFAULT 'ERROR',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA
);

CREATE INDEX ix_ve_file_id ON validation_errors(ingestion_file_id);
CREATE INDEX ix_ve_error_code ON validation_errors(error_code);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 8️⃣ VALIDATION_NOTIFICATION (Audit of Error Notifications)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS validation_notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ingestion_file_id UUID NOT NULL REFERENCES ingestion_files(id) ON DELETE CASCADE,
    file_name VARCHAR(500) NOT NULL,
    reporting_period VARCHAR(50) NOT NULL,
    source_system VARCHAR(20) NOT NULL,
    recipients VARCHAR(2000) NOT NULL,
    total_errors INTEGER NOT NULL,
    sent_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    status VARCHAR(30) NOT NULL DEFAULT 'SENT',
    error_detail TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA
);

CREATE INDEX ix_vn_file_id ON validation_notifications(ingestion_file_id);
CREATE INDEX ix_vn_status ON validation_notifications(status);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 9️⃣ METRIC_VALUES (EAV — Entity/Attribute/Value)
-- ═══════════════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS metric_values (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reporting_period DATE NOT NULL,
    shipper_id UUID REFERENCES shippers(id) ON DELETE SET NULL,
    shipper_short_code VARCHAR(50) NOT NULL,
    metric_key VARCHAR(50) NOT NULL,
    value NUMERIC(18,6),
    text_value TEXT,
    product_class_code VARCHAR(10),
    ingestion_file_id UUID NOT NULL REFERENCES ingestion_files(id) ON DELETE CASCADE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    created_by VARCHAR(100),
    updated_at TIMESTAMP WITH TIME ZONE,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version BYTEA
);

CREATE INDEX ix_metric_values_shipper_id ON metric_values(shipper_id);
CREATE INDEX ix_metric_values_period_shipper_key ON metric_values(reporting_period, shipper_short_code, metric_key);
CREATE INDEX ix_metric_values_period ON metric_values(reporting_period);
CREATE INDEX ix_metric_values_key ON metric_values(metric_key);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 🔟 REPORT_TYPES (Reference: Schedule 2A / 2B)
-- ═══════════════════════════════════════════════════════════════════════════════
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

CREATE INDEX ix_reporttype_code ON report_types(code);

-- ═══════════════════════════════════════════════════════════════════════════════
-- 1️⃣1️⃣ REPORTS (Export / Delivery Tracking)
-- ═══════════════════════════════════════════════════════════════════════════════
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

CREATE INDEX ix_reports_period_type ON reports(reporting_period, report_type_id);
CREATE INDEX ix_reports_status ON reports(status);
CREATE INDEX ix_reports_audience ON reports(audience);

-- ═══════════════════════════════════════════════════════════════════════════════
-- Seed Data: ReportTypes
-- ═══════════════════════════════════════════════════════════════════════════════
INSERT INTO report_types (code, schedule_ref, label, audience, report_count, is_active, created_by)
VALUES 
    ('SCH2A', 'Schedule 2A', 'Industry Peer Comparison (Anonymised)', 'Industry', 19, TRUE, 'SYSTEM'),
    ('SCH2B', 'Schedule 2B', 'Performance Assurance Committee (Non-Anonymised)', 'PAC', 22, TRUE, 'SYSTEM')
ON CONFLICT (code) DO NOTHING;

-- ═══════════════════════════════════════════════════════════════════════════════
-- Seed Data: ProductClasses
-- ═══════════════════════════════════════════════════════════════════════════════
INSERT INTO product_classes (id, code, description, aq_threshold_low, min_read_percentage, is_active, created_by)
VALUES 
    (1, 'PC1', 'Large sites — AQ ≥ 732 MWH', 732, 97.5, TRUE, 'SYSTEM'),
    (2, 'PC2', 'Medium NDM', NULL, 97.5, TRUE, 'SYSTEM'),
    (3, 'PC3', 'Small NDM WAR', NULL, 97.5, TRUE, 'SYSTEM'),
    (4, 'PC4', 'IGT Small', NULL, 97.5, TRUE, 'SYSTEM')
ON CONFLICT (code) DO NOTHING;

-- ═══════════════════════════════════════════════════════════════════════════════
-- Validation & Summary
-- ═══════════════════════════════════════════════════════════════════════════════
SELECT 
    (SELECT COUNT(*) FROM shippers) AS shipper_count,
    (SELECT COUNT(*) FROM product_classes) AS product_class_count,
    (SELECT COUNT(*) FROM report_types) AS report_type_count,
    (SELECT COUNT(*) FROM ingestion_jobs) AS ingestion_job_count,
    (SELECT COUNT(*) FROM reports) AS report_count;

-- ═══════════════════════════════════════════════════════════════════════════════
-- End of Schema Creation Script
-- ═══════════════════════════════════════════════════════════════════════════════
