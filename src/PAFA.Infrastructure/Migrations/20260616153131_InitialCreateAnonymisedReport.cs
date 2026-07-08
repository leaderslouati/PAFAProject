using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateAnonymisedReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "euc_bands",
                columns: table => new
                {
                    EucCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AqThresholdMinKwh = table.Column<long>(type: "bigint", nullable: false),
                    AqThresholdMaxKwh = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_euc_bands", x => x.EucCode);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reporting_period = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Started"),
                    files_expected = table.Column<int>(type: "integer", nullable: true),
                    files_downloaded = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    files_processed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    files_failed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    records_loaded = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    error_summary = table.Column<string>(type: "jsonb", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    triggered_by = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Scheduler"),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    parent_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_ingestion_jobs_ingestion_jobs_parent_job_id",
                        column: x => x.parent_job_id,
                        principalTable: "ingestion_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "lookup_values",
                columns: table => new
                {
                    LookupId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lookup_values", x => x.LookupId);
                });

            migrationBuilder.CreateTable(
                name: "pafa_permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pafa_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pafa_roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pafa_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pafa_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Department = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pafa_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_classes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    aq_threshold_low = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    aq_threshold_high = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    min_read_percentage = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_classes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_definitions",
                columns: table => new
                {
                    ReportCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SplitBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReportFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RollingMonths = table.Column<int>(type: "integer", nullable: false, defaultValue: 12),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_definitions", x => x.ReportCode);
                });

            migrationBuilder.CreateTable(
                name: "report_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    schedule_ref = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    report_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shippers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    short_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_entity = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    market_entry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    market_exit_date = table.Column<DateOnly>(type: "date", nullable: true),
                    portfolio_size = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shippers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ingestion_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_system = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    file_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    blob_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Downloaded"),
                    validation_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Valid"),
                    rows_read = table.Column<int>(type: "integer", nullable: true),
                    rows_valid = table.Column<int>(type: "integer", nullable: true),
                    rows_rejected = table.Column<int>(type: "integer", nullable: true),
                    error_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    downloaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_modified_remote = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_ingestion_files_ingestion_jobs_ingestion_job_id",
                        column: x => x.ingestion_job_id,
                        principalTable: "ingestion_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pafa_role_permissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false),
                    PafaRoleId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pafa_role_permissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_pafa_role_permissions_pafa_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "pafa_permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pafa_role_permissions_pafa_roles_PafaRoleId",
                        column: x => x.PafaRoleId,
                        principalTable: "pafa_roles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_pafa_role_permissions_pafa_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "pafa_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pafa_user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pafa_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_pafa_user_roles_pafa_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "pafa_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pafa_user_roles_pafa_users_UserId",
                        column: x => x.UserId,
                        principalTable: "pafa_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metric_definitions",
                columns: table => new
                {
                    MetricCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReportCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExtraDimCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_definitions", x => x.MetricCode);
                    table.ForeignKey(
                        name: "FK_metric_definitions_report_definitions_ReportCode",
                        column: x => x.ReportCode,
                        principalTable: "report_definitions",
                        principalColumn: "ReportCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    report_type_id = table.Column<int>(type: "integer", nullable: false),
                    schedule_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reporting_period = table.Column<DateOnly>(type: "date", nullable: false),
                    audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Industry"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    file_path_pdf = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    file_path_excel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    file_path_pptx = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    commentary_text = table.Column<string>(type: "text", nullable: true),
                    commentary_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    observations_text = table.Column<string>(type: "text", nullable: true),
                    observations_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    observations_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ingestion_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_baseline = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_reports_ingestion_jobs_ingestion_job_id",
                        column: x => x.ingestion_job_id,
                        principalTable: "ingestion_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_reports_report_types_report_type_id",
                        column: x => x.report_type_id,
                        principalTable: "report_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "aq_corrections_by_reason",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodId = table.Column<int>(type: "integer", nullable: false, comment: "Format YYYYMM — ex. 202603 = Mars 2026"),
                    ReasonCodeLookupId = table.Column<int>(type: "integer", nullable: false),
                    MprnCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IngestionFileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aq_corrections_by_reason", x => x.Id);
                    table.ForeignKey(
                        name: "FK_aq_corrections_by_reason_lookup_values_ReasonCodeLookupId",
                        column: x => x.ReasonCodeLookupId,
                        principalTable: "lookup_values",
                        principalColumn: "LookupId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_aq_corrections_by_reason_shippers_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipper_alias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shipper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipper_alias", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipper_alias_shippers_shipper_id",
                        column: x => x.shipper_id,
                        principalTable: "shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipper_product_classes",
                columns: table => new
                {
                    shipper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_class_id = table.Column<int>(type: "integer", nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplyPointCount = table.Column<int>(type: "integer", nullable: true),
                    TotalAQ_MWH = table.Column<decimal>(type: "numeric", nullable: true),
                    EstimatedPct = table.Column<decimal>(type: "numeric", nullable: true),
                    CheckReadCountNotCompleted = table.Column<int>(type: "integer", nullable: true),
                    ReadPerfPct = table.Column<decimal>(type: "numeric", nullable: true),
                    NoMeterCount = table.Column<int>(type: "integer", nullable: true),
                    NoMeterPct = table.Column<decimal>(type: "numeric", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipper_product_classes", x => new { x.shipper_id, x.product_class_id });
                    table.ForeignKey(
                        name: "FK_shipper_product_classes_product_classes_product_class_id",
                        column: x => x.product_class_id,
                        principalTable: "product_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shipper_product_classes_shippers_shipper_id",
                        column: x => x.shipper_id,
                        principalTable: "shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supply_point_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GasDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassId = table.Column<int>(type: "integer", nullable: true),
                    EucCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LdzCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MprnCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AqRoll = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supply_point_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supply_point_snapshots_shippers_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "metric_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    reporting_period = table.Column<DateOnly>(type: "date", nullable: false),
                    shipper_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shipper_short_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    metric_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    text_value = table.Column<string>(type: "text", nullable: true),
                    product_class_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReportCode = table.Column<string>(type: "text", nullable: true),
                    EucCode = table.Column<string>(type: "text", nullable: true),
                    LookupValueId = table.Column<int>(type: "integer", nullable: true),
                    ingestion_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true),
                    ReportCode1 = table.Column<string>(type: "character varying(10)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_metric_values_ingestion_files_ingestion_file_id",
                        column: x => x.ingestion_file_id,
                        principalTable: "ingestion_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_metric_values_lookup_values_LookupValueId",
                        column: x => x.LookupValueId,
                        principalTable: "lookup_values",
                        principalColumn: "LookupId");
                    table.ForeignKey(
                        name: "FK_metric_values_report_definitions_ReportCode1",
                        column: x => x.ReportCode1,
                        principalTable: "report_definitions",
                        principalColumn: "ReportCode");
                    table.ForeignKey(
                        name: "FK_metric_values_shippers_shipper_id",
                        column: x => x.shipper_id,
                        principalTable: "shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "validation_errors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ingestion_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: true),
                    column_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    original_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ERROR"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_errors", x => x.id);
                    table.ForeignKey(
                        name: "FK_validation_errors_ingestion_files_ingestion_file_id",
                        column: x => x.ingestion_file_id,
                        principalTable: "ingestion_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "validation_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ingestion_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reporting_period = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_system = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    total_errors = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "SENT"),
                    error_detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_validation_notifications_ingestion_files_ingestion_file_id",
                        column: x => x.ingestion_file_id,
                        principalTable: "ingestion_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "euc_bands",
                columns: new[] { "EucCode", "AqThresholdMaxKwh", "AqThresholdMinKwh", "Description" },
                values: new object[,]
                {
                    { "EUC01", 73199L, 0L, "< 73,200 kWh/yr" },
                    { "EUC02", 292999L, 73200L, "73,200 – 293,000 kWh/yr" },
                    { "EUC03", 731999L, 293000L, "293,000 – 732,000 kWh/yr" },
                    { "EUC04", 2195999L, 732000L, "732,000 – 2,196,000 kWh/yr" },
                    { "EUC05", 7319999L, 2196000L, "2,196,000 – 7,320,000 kWh/yr" },
                    { "EUC06", 14639999L, 7320000L, "7,320,000 – 14,640,000 kWh/yr" },
                    { "EUC07", 29279999L, 14640000L, "14,640,000 – 29,280,000 kWh/yr" },
                    { "EUC08", 58599999L, 29280000L, "29,280,000 – 58,600,000 kWh/yr" },
                    { "EUC09", 9223372036854775807L, 58600000L, ">= 58,600,000 kWh/yr" }
                });

            migrationBuilder.InsertData(
                table: "lookup_values",
                columns: new[] { "LookupId", "Category", "Code", "Label", "SortOrder" },
                values: new object[,]
                {
                    { 1, "ReasonCode", "01", "Confirmed Theft", 1 },
                    { 2, "ReasonCode", "02", "Change in Consumer Plant", 2 },
                    { 3, "ReasonCode", "03", "Commencement of New Business Activity", 3 },
                    { 4, "ReasonCode", "04", "Tolerance Change", 4 },
                    { 5, "ReasonCode", "05", "Winter Consumption", 5 },
                    { 6, "ReasonCode", "06", "Erroneous AQ based on Read History", 6 },
                    { 7, "ReasonCode", "07", "Erroneous AQ – Change in operation and/or use", 7 },
                    { 8, "ReasonCode", "08", "AQ decrease due to the site being Vacant", 8 },
                    { 9, "ReasonCode", "09", "AQ increase due to the site no longer being Vacant", 9 },
                    { 10, "AgeBucket", "a_0_6", "a. 0 – 6 months", 1 },
                    { 11, "AgeBucket", "b_6_12", "b. 6 – 12 months", 2 },
                    { 12, "AgeBucket", "c_12_plus", "c. 12 months +", 3 },
                    { 13, "ObligationType", "MONTHLY_293K", "Monthly Read – AQ ≥ 293,000 kWh", 1 },
                    { 14, "ObligationType", "SMART_AMR", "Monthly Read – SMART/AMR < 293,000 kWh", 2 },
                    { 15, "ObligationType", "ANNUAL", "Annual Read – non-Smart < 293,000 kWh", 3 },
                    { 16, "YearBand", "1YR", "1 Year", 1 },
                    { 17, "YearBand", "2YR", "2 Years", 2 },
                    { 18, "YearBand", "3YR", "3 Years", 3 },
                    { 19, "YearBand", "4YR", "4 Years", 4 },
                    { 20, "MRECode", "MRE01026", "Reading breached the Lower Outer Tolerance", 1 },
                    { 21, "MRECode", "MRE01027", "Reading breached the Upper Outer Tolerance", 2 },
                    { 22, "MRECode", "MRE01028", "Reading breached the Lower Inner Tolerance – no override flag provided", 3 },
                    { 23, "MRECode", "MRE01029", "Reading breached the Upper Inner Tolerance – no override flag provided", 4 },
                    { 24, "MRECode", "MRE01030", "Override Tolerance passed & override flag provided", 5 },
                    { 25, "PeriodCode", "P41", "UNC Modification P41", 1 },
                    { 26, "PeriodCode", "P106", "UNC Modification P106", 2 }
                });

            migrationBuilder.InsertData(
                table: "pafa_permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { 1, "users.create", "Create user accounts" },
                    { 2, "users.delete", "Delete user accounts" },
                    { 3, "reports.anonymised.view", "View Schedule 2A (anonymised) reports" },
                    { 4, "reports.nonanonymised.view", "View Schedule 2B (non-anonymised) reports" },
                    { 5, "reports.anonymised.edit", "Edit Schedule 2A reports (observations)" },
                    { 6, "reports.nonanonymised.edit", "Edit Schedule 2B reports (observations)" },
                    { 7, "reports.download", "Download report files (PDF/Excel/PPTX)" }
                });

            migrationBuilder.InsertData(
                table: "pafa_roles",
                columns: new[] { "Id", "Description", "Name", "Role" },
                values: new object[,]
                {
                    { 1, "Gemserv analyst — read reports", "PafaUser", "PAFA_USER" },
                    { 2, "Admin full access", "PafaAdmin", "PAFA_ADMIN" },
                    { 3, "PAC access", "PacMember", "PAC_MEMBER" },
                    { 4, "Own data access", "Shipper", "SHIPPER" }
                });

            migrationBuilder.InsertData(
                table: "product_classes",
                columns: new[] { "id", "aq_threshold_high", "aq_threshold_low", "code", "created_at", "created_by", "description", "is_active", "min_read_percentage", "row_version", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { 1, null, 732m, "PC1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Large sites — AQ ≥ 732 MWH", true, 97.5m, null, null, null },
                    { 2, null, null, "PC2", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Medium NDM", true, null, null, null, null },
                    { 3, null, null, "PC3", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Small NDM WAR", true, null, null, null, null },
                    { 4, null, null, "PC4", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "IGT Small", true, null, null, null, null }
                });

            migrationBuilder.InsertData(
                table: "report_definitions",
                columns: new[] { "ReportCode", "DisplayOrder", "ReportFormat", "RollingMonths", "SplitBy", "Topic" },
                values: new object[,]
                {
                    { "2A.1", 1, "Percentage", 12, "Class", "Estimated & Check Reads" },
                    { "2A.10", 10, "Count", 12, "EUC Band", "Replaced Meter Reads" },
                    { "2A.11a", 11, "Count + AQ", 12, "Current Class", "Sites above Class 1 threshold" },
                    { "2A.11b", 12, "Count + AQ", 12, "Shipper vs CDSP", "Sites reclassified to Class 1" },
                    { "2A.12a", 13, "Pct Read", 12, "Obligation", "AQ Read Perf PC4 Monthly ≥293k" },
                    { "2A.12b", 14, "Pct Read", 12, "Obligation", "AQ Read Perf PC4 Smart/AMR" },
                    { "2A.12c", 15, "Pct Read", 12, "Obligation", "AQ Read Perf PC4 Annual" },
                    { "2A.13", 16, "Pct overdue", 2, "Obligation", "AQ Overdue for Meter Reading" },
                    { "2A.14", 17, "Percentage", 12, "Claims & Withdrawals", "Confirmed Energy Theft Notifications" },
                    { "2A.15", 18, "Count", 12, "Current Class", "CDSP Sites Converted PC4" },
                    { "2A.16", 19, "Percentage", 12, "Current Class", "PC2/PC3 Read vs Min Pct Requirement" },
                    { "2A.17", 20, "Pct/Count", 12, "Count/Percentage", "IGT Must Read Process" },
                    { "2A.18", 21, "Pct/Count", 12, "Count/Percentage", "Corrective Opening Meter Reading Rejections" },
                    { "2A.19", 22, "Pct/Count", 12, "Count/Percentage", "Class 4 Vacant Sites" },
                    { "2A.2", 2, "Percentage", 12, "Class", "No Meter Recorded in SP" },
                    { "2A.3", 3, "Percentage", 12, "Class", "No Meter Recorded + Data Flows" },
                    { "2A.4", 4, "Percentage", 12, "Total", "Shipper Transfer Read Performance" },
                    { "2A.5", 5, "Percentage", 12, "Class", "Read Performance" },
                    { "2A.6", 6, "Percentage", 12, "Class", "Meter Read Validity Monitoring" },
                    { "2A.7", 7, "Percentage", 12, "EUC + Class", "No Reads 1,2,3,4 Years" },
                    { "2A.8", 8, "Count", 12, "Reason Code", "AQ Correction by Reason Code" },
                    { "2A.9", 9, "Count", 12, "EUC Band", "Standard CF AQ > 732k kWh" },
                    { "2B.11", 23, "Pct/Count", 12, "EUC × Shipper", "AQ Portfolio Calculation (8 sub-reports)" }
                });

            migrationBuilder.InsertData(
                table: "report_types",
                columns: new[] { "id", "audience", "code", "created_at", "created_by", "is_active", "label", "report_count", "row_version", "schedule_ref", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { 1, "Industry", "SCH2A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Industry Peer Comparison (Anonymised)", 19, null, "Schedule 2A", null, null },
                    { 2, "PAC", "SCH2B", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Performance Assurance Committee (Non-Anonymised)", 22, null, "Schedule 2B", null, null }
                });

            migrationBuilder.InsertData(
                table: "metric_definitions",
                columns: new[] { "MetricCode", "ExtraDimCategory", "Label", "ReportCode", "Unit" },
                values: new object[,]
                {
                    { "AQ_AT_RISK_GWH", "ObligationType", "Total AQ overdue for meter reading (GWh)", "2A.13", "GWh" },
                    { "AQ_AT_RISK_PCT", "ObligationType", "Percentage AQ overdue for meter reading", "2A.13", "Pct" },
                    { "AQ_CALC_FREQ_12M", "EUC", "AQ Calculation Frequency – 12 months", "2B.11", "Pct" },
                    { "AQ_CALC_FREQ_1M", "EUC", "AQ Calculation Frequency – 1 month", "2B.11", "Pct" },
                    { "AQ_CALC_FREQ_36M", "EUC", "AQ Calculation Frequency – 36+ months", "2B.11", "Pct" },
                    { "AQ_CALC_FREQ_4M", "EUC", "AQ Calculation Frequency – 4 months", "2B.11", "Pct" },
                    { "AQ_CALC_PCT", "EUC", "% Portfolio AQ calculated in month", "2B.11", "Pct" },
                    { "AQ_CORR_REASON_COUNT", "ReasonCode", "Count of AQ Corrections by Reason Code", "2A.8", "Count" },
                    { "AQ_DEC_PCT", "EUC", "% AQ in decrease (rolling 12m)", "2B.11", "Pct" },
                    { "AQ_FAIL_COUNT", "EUC", "Count of AQ calculation failures", "2B.11", "Count" },
                    { "AQ_INC_PCT", "EUC", "% AQ in increase (rolling 12m)", "2B.11", "Pct" },
                    { "AQ_READ_PERF_PCT", "ObligationType", "AQ Read Performance % (Monthly ≥293k)", "2A.12a", "Pct" },
                    { "CHECK_COUNT", null, "Count of Check Reads not completed", "2A.1", "Count" },
                    { "CLASS1_ABOVE_AQ", null, "Total AQ of SP above Class 1 threshold", "2A.11a", "GWh" },
                    { "CLASS1_ABOVE_COUNT", null, "Count of SP above Class 1 threshold", "2A.11a", "Count" },
                    { "CLASS3_CONV_COUNT", null, "Count of SP converted from PC2/3 to PC4", "2A.15", "Count" },
                    { "CLASS3_CONV_PCT", null, "Percentage of SP converted to PC4", "2A.15", "Pct" },
                    { "COMR_COUNT", null, "Count of Corrective Opening Meter Readings", "2A.18", "Count" },
                    { "COMR_REJECT_RAISED_PCT", null, "% Rejections raised against COMRs", "2A.18", "Pct" },
                    { "COMR_REJECT_RECV_PCT", null, "% Rejections received on COMRs", "2A.18", "Pct" },
                    { "EST_PCT", null, "Percentage of Estimated Reads", "2A.1", "Pct" },
                    { "MIN_PCT_NOT_MET_COUNT", null, "Count of SP not meeting Min Read Req.", "2A.16", "Count" },
                    { "MIN_PCT_REQ_PCT", null, "Percentage meeting Min Read Requirement", "2A.16", "Pct" },
                    { "MPRN_ENTERING_COUNT", null, "Count MPRNs entering Must Read", "2A.17", "Count" },
                    { "MPRN_REMOVED_PCT", "AgeBucket", "Percentage MPRNs removed from Must Read", "2A.17", "Pct" },
                    { "MRE01026", "MRECode", "MRE01026 – Lower Outer Tolerance breached", "2A.6", "Pct" },
                    { "MRE01027", "MRECode", "MRE01027 – Upper Outer Tolerance breached", "2A.6", "Pct" },
                    { "MRE01028", "MRECode", "MRE01028 – Lower Inner Tolerance breached", "2A.6", "Pct" },
                    { "MRE01029", "MRECode", "MRE01029 – Upper Inner Tolerance breached", "2A.6", "Pct" },
                    { "MRE01030", "MRECode", "MRE01030 – Override Tolerance passed", "2A.6", "Pct" },
                    { "MUST_READ_AGE_PCT", "AgeBucket", "Percentage MPRNs removed by Age Bucket", "2A.17", "Pct" },
                    { "NO_METER_FLOW_PCT", null, "Percentage SP no meter but data flows received", "2A.3", "Pct" },
                    { "NO_METER_PCT", null, "Percentage SP with no meter recorded", "2A.2", "Pct" },
                    { "NO_READ_1YR", "YearBand", "No read for 1 year", "2A.7", "Pct" },
                    { "NO_READ_2YR", "YearBand", "No read for 2 years", "2A.7", "Pct" },
                    { "NO_READ_3YR", "YearBand", "No read for 3 years", "2A.7", "Pct" },
                    { "NO_READ_4YR", "YearBand", "No read for 4 years", "2A.7", "Pct" },
                    { "READ_PERF_PCT", null, "Percentage of Read Submissions", "2A.5", "Pct" },
                    { "RECLASSIFIED_COUNT", null, "Count of SP reclassified to Class 1", "2A.11b", "Count" },
                    { "REPLACED_READ_COUNT", "EUC", "Count of replaced meter reads", "2A.10", "Count" },
                    { "STD_CF_COUNT", "EUC", "Count of sites using standard CF (>732k)", "2A.9", "Count" },
                    { "THEFT_CLAIM_ENERGY_PCT", "PeriodCode", "% Energy value of Theft Claims (P41)", "2A.14", "Pct" },
                    { "THEFT_CLAIM_OBJ_PCT", "PeriodCode", "% Energy Theft Claims objected (P41)", "2A.14", "Pct" },
                    { "THEFT_P106_CLAIM_ENERGY_PCT", "PeriodCode", "% Energy value of Theft Claims (P106)", "2A.14", "Pct" },
                    { "THEFT_P106_CLAIM_OBJ_PCT", "PeriodCode", "% Energy Theft Claims objected (P106)", "2A.14", "Pct" },
                    { "THEFT_P106_WD_ENERGY_PCT", "PeriodCode", "% Energy value of Withdrawals (P106)", "2A.14", "Pct" },
                    { "THEFT_P106_WD_OBJ_PCT", "PeriodCode", "% Energy Theft Withdrawals objected (P106)", "2A.14", "Pct" },
                    { "THEFT_WD_ENERGY_PCT", "PeriodCode", "% Energy value of Withdrawals (P41)", "2A.14", "Pct" },
                    { "THEFT_WD_OBJ_PCT", "PeriodCode", "% Energy Theft Withdrawals objected (P41)", "2A.14", "Pct" },
                    { "TRANSFER_COUNT", null, "Number of Transfers", "2A.4", "Count" },
                    { "TRANSFER_READ_PCT", null, "Transfer Read Performance Percentage", "2A.4", "Pct" },
                    { "VACANT_EOD_COUNT", null, "Count of Vacant sites at end of month", "2A.19", "Count" },
                    { "VACANT_IN_MONTH_COUNT", null, "Count of sites set to Vacant in month", "2A.19", "Count" },
                    { "VACANT_PROPORTION_AGE", "AgeBucket", "Proportion of Vacant sites by Age Bucket", "2A.19", "Pct" }
                });

            migrationBuilder.InsertData(
                table: "pafa_role_permissions",
                columns: new[] { "PermissionId", "RoleId", "PafaRoleId" },
                values: new object[,]
                {
                    { 3, 1, null },
                    { 7, 1, null },
                    { 1, 2, null },
                    { 2, 2, null },
                    { 3, 2, null },
                    { 4, 2, null },
                    { 5, 2, null },
                    { 6, 2, null },
                    { 7, 2, null },
                    { 3, 3, null },
                    { 4, 3, null },
                    { 7, 3, null },
                    { 3, 4, null },
                    { 7, 4, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_aq_corrections_by_reason_ReasonCodeLookupId",
                table: "aq_corrections_by_reason",
                column: "ReasonCodeLookupId");

            migrationBuilder.CreateIndex(
                name: "IX_aq_corrections_by_reason_ShipperId",
                table: "aq_corrections_by_reason",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_aq_corrections_Period_Shipper_Reason",
                table: "aq_corrections_by_reason",
                columns: new[] { "PeriodId", "ShipperId", "ReasonCodeLookupId" });

            migrationBuilder.CreateIndex(
                name: "IX_aq_corrections_PeriodId",
                table: "aq_corrections_by_reason",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "ix_file_hash",
                table: "ingestion_files",
                column: "file_hash");

            migrationBuilder.CreateIndex(
                name: "ix_file_job_id",
                table: "ingestion_files",
                column: "ingestion_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_file_status",
                table: "ingestion_files",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_parent_job_id",
                table: "ingestion_jobs",
                column: "parent_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_correlation_id",
                table: "ingestion_jobs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_period",
                table: "ingestion_jobs",
                column: "reporting_period");

            migrationBuilder.CreateIndex(
                name: "ix_job_status",
                table: "ingestion_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_lookup_values_Category_Code",
                table: "lookup_values",
                columns: new[] { "Category", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metric_definitions_ReportCode",
                table: "metric_definitions",
                column: "ReportCode");

            migrationBuilder.CreateIndex(
                name: "IX_metric_values_ingestion_file_id",
                table: "metric_values",
                column: "ingestion_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_metric_values_key",
                table: "metric_values",
                column: "metric_key");

            migrationBuilder.CreateIndex(
                name: "IX_metric_values_LookupValueId",
                table: "metric_values",
                column: "LookupValueId");

            migrationBuilder.CreateIndex(
                name: "ix_metric_values_period",
                table: "metric_values",
                column: "reporting_period");

            migrationBuilder.CreateIndex(
                name: "ix_metric_values_period_shipper_key",
                table: "metric_values",
                columns: new[] { "reporting_period", "shipper_short_code", "metric_key" });

            migrationBuilder.CreateIndex(
                name: "IX_metric_values_ReportCode1",
                table: "metric_values",
                column: "ReportCode1");

            migrationBuilder.CreateIndex(
                name: "ix_metric_values_shipper_id",
                table: "metric_values",
                column: "shipper_id");

            migrationBuilder.CreateIndex(
                name: "IX_pafa_permissions_Code",
                table: "pafa_permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pafa_role_permissions_PafaRoleId",
                table: "pafa_role_permissions",
                column: "PafaRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_pafa_role_permissions_PermissionId",
                table: "pafa_role_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_pafa_roles_Name",
                table: "pafa_roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pafa_roles_Role",
                table: "pafa_roles",
                column: "Role",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pafa_user_roles_RoleId",
                table: "pafa_user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_pafa_users_Email",
                table: "pafa_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pafa_users_Username",
                table: "pafa_users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pc_code",
                table: "product_classes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reporttype_code",
                table: "report_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_audience",
                table: "reports",
                column: "audience");

            migrationBuilder.CreateIndex(
                name: "IX_reports_ingestion_job_id",
                table: "reports",
                column: "ingestion_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_period_type",
                table: "reports",
                columns: new[] { "reporting_period", "report_type_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reports_report_type_id",
                table: "reports",
                column: "report_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_status",
                table: "reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_shipper_alias_code",
                table: "shipper_alias",
                column: "alias_code");

            migrationBuilder.CreateIndex(
                name: "ix_shipper_alias_shipper_id",
                table: "shipper_alias",
                column: "shipper_id");

            migrationBuilder.CreateIndex(
                name: "ix_spc_product_class_id",
                table: "shipper_product_classes",
                column: "product_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_spc_shipper_id",
                table: "shipper_product_classes",
                column: "shipper_id");

            migrationBuilder.CreateIndex(
                name: "ux_spc_shipper_product_class",
                table: "shipper_product_classes",
                columns: new[] { "shipper_id", "product_class_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipper_is_active",
                table: "shippers",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_shipper_short_code",
                table: "shippers",
                column: "short_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_GasDay",
                table: "supply_point_snapshots",
                column: "GasDay");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_GasDay_Shipper_Class_EUC",
                table: "supply_point_snapshots",
                columns: new[] { "GasDay", "ShipperId", "ClassId", "EucCode" });

            migrationBuilder.CreateIndex(
                name: "IX_supply_point_snapshots_ShipperId",
                table: "supply_point_snapshots",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "ix_ve_error_code",
                table: "validation_errors",
                column: "error_code");

            migrationBuilder.CreateIndex(
                name: "ix_ve_file_id",
                table: "validation_errors",
                column: "ingestion_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_vn_file_id",
                table: "validation_notifications",
                column: "ingestion_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_vn_status",
                table: "validation_notifications",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aq_corrections_by_reason");

            migrationBuilder.DropTable(
                name: "euc_bands");

            migrationBuilder.DropTable(
                name: "metric_definitions");

            migrationBuilder.DropTable(
                name: "metric_values");

            migrationBuilder.DropTable(
                name: "pafa_role_permissions");

            migrationBuilder.DropTable(
                name: "pafa_user_roles");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "shipper_alias");

            migrationBuilder.DropTable(
                name: "shipper_product_classes");

            migrationBuilder.DropTable(
                name: "supply_point_snapshots");

            migrationBuilder.DropTable(
                name: "validation_errors");

            migrationBuilder.DropTable(
                name: "validation_notifications");

            migrationBuilder.DropTable(
                name: "lookup_values");

            migrationBuilder.DropTable(
                name: "report_definitions");

            migrationBuilder.DropTable(
                name: "pafa_permissions");

            migrationBuilder.DropTable(
                name: "pafa_roles");

            migrationBuilder.DropTable(
                name: "pafa_users");

            migrationBuilder.DropTable(
                name: "report_types");

            migrationBuilder.DropTable(
                name: "product_classes");

            migrationBuilder.DropTable(
                name: "shippers");

            migrationBuilder.DropTable(
                name: "ingestion_files");

            migrationBuilder.DropTable(
                name: "ingestion_jobs");
        }
    }
}
