using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewViewReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    JobName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FilesExpected = table.Column<int>(type: "integer", nullable: true),
                    FilesDownloaded = table.Column<int>(type: "integer", nullable: false),
                    FilesProcessed = table.Column<int>(type: "integer", nullable: false),
                    FilesFailed = table.Column<int>(type: "integer", nullable: false),
                    RecordsLoaded = table.Column<long>(type: "bigint", nullable: false),
                    ErrorSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParentJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ingestion_jobs_ingestion_jobs_ParentJobId",
                        column: x => x.ParentJobId,
                        principalTable: "ingestion_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AQThresholdLow = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    AQThresholdHigh = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    MinReadPercentage = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_classes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "report_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ScheduleRef = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shippers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    short_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_entity = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MarketEntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MarketExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PortfolioSize = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shippers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IngestionJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    BlobPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ValidationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RowsRead = table.Column<int>(type: "integer", nullable: true),
                    RowsValid = table.Column<int>(type: "integer", nullable: true),
                    RowsRejected = table.Column<int>(type: "integer", nullable: true),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true, defaultValueSql: "decode('', 'hex')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ingestion_files_ingestion_jobs_IngestionJobId",
                        column: x => x.IngestionJobId,
                        principalTable: "ingestion_jobs",
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
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportTypeId = table.Column<int>(type: "integer", nullable: false),
                    ScheduleNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FilePath_PDF = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FilePath_Excel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FilePath_PPTX = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CommentaryText = table.Column<string>(type: "text", nullable: true),
                    CommentaryBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ObservationsText = table.Column<string>(type: "text", nullable: true, comment: "Observations mensuelles saisies manuellement par l'analyste PAFA."),
                    ObservationsBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "Identifiant (UPN/email) de l'analyste."),
                    ObservationsUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Horodatage UTC de la dernière mise à jour des observations."),
                    IngestionJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsBaseline = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reports_ingestion_jobs_IngestionJobId",
                        column: x => x.IngestionJobId,
                        principalTable: "ingestion_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_reports_report_types_ReportTypeId",
                        column: x => x.ReportTypeId,
                        principalTable: "report_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipper_product_classes",
                columns: table => new
                {
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductClassId = table.Column<int>(type: "integer", nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplyPointCount = table.Column<int>(type: "integer", nullable: true),
                    TotalAQ_MWH = table.Column<decimal>(type: "numeric", nullable: true),
                    EstimatedPct = table.Column<decimal>(type: "numeric(8,4)", nullable: true, comment: "% lectures estimées (0-100). Source: MetricKey='EstimatedPct'."),
                    CheckReadCountNotCompleted = table.Column<int>(type: "integer", nullable: true, comment: "Nb check reads non complétés. >= 0."),
                    ReadPerfPct = table.Column<decimal>(type: "numeric(8,4)", nullable: true, comment: "% global de performance lecture (0-100)."),
                    NoMeterCount = table.Column<int>(type: "integer", nullable: true, comment: "Nb SP sans meter enregistré. >= 0."),
                    NoMeterPct = table.Column<decimal>(type: "numeric(8,4)", nullable: true, comment: "% SP sans meter. PC1/PC2 = 0."),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipper_product_classes", x => new { x.ShipperId, x.ProductClassId, x.ReportingPeriod });
                    table.ForeignKey(
                        name: "FK_shipper_product_classes_product_classes_ProductClassId",
                        column: x => x.ProductClassId,
                        principalTable: "product_classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipper_product_classes_shippers_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipperAlias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false),
                    AliasCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipperAlias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipperAlias_shippers_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metric_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipperShortCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TextValue = table.Column<string>(type: "text", nullable: true),
                    ProductClassCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IngestionFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_metric_values_ingestion_files_IngestionFileId",
                        column: x => x.IngestionFileId,
                        principalTable: "ingestion_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_metric_values_shippers_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "shippers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "validation_errors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IngestionFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    ColumnName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OriginalValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Severity = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_errors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_validation_errors_ingestion_files_IngestionFileId",
                        column: x => x.IngestionFileId,
                        principalTable: "ingestion_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                columns: new[] { "Id", "AQThresholdHigh", "AQThresholdLow", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsDeleted", "MinReadPercentage", "RowVersion", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, null, 732m, "PC1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Large sites — AQ ≥ 732 MWH", true, false, 97.5m, null, null, null },
                    { 2, null, null, "PC2", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Medium NDM", true, false, null, null, null, null },
                    { 3, null, null, "PC3", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Small NDM WAR", true, false, null, null, null, null },
                    { 4, null, null, "PC4", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "IGT Small", true, false, null, null, null, null }
                });

            migrationBuilder.InsertData(
                table: "report_types",
                columns: new[] { "Id", "Audience", "Code", "CreatedAt", "CreatedBy", "IsActive", "IsDeleted", "Label", "ReportCount", "RowVersion", "ScheduleRef", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "Industry", "SCH2A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, false, "Industry Peer Comparison (Anonymised)", 19, null, "Schedule 2A", null, null },
                    { 2, "PAC", "SCH2B", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, false, "Performance Assurance Committee (Non-Anonymised)", 22, null, "Schedule 2B", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_hash",
                table: "ingestion_files",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_files_IngestionJobId",
                table: "ingestion_files",
                column: "IngestionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_ParentJobId",
                table: "ingestion_jobs",
                column: "ParentJobId");

            migrationBuilder.CreateIndex(
                name: "ix_job_period",
                table: "ingestion_jobs",
                column: "ReportingPeriod");

            migrationBuilder.CreateIndex(
                name: "ix_job_status",
                table: "ingestion_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_metric_values_IngestionFileId",
                table: "metric_values",
                column: "IngestionFileId");

            migrationBuilder.CreateIndex(
                name: "IX_metric_values_Period_Shipper_MetricKey",
                table: "metric_values",
                columns: new[] { "ReportingPeriod", "ShipperShortCode", "MetricKey" });

            migrationBuilder.CreateIndex(
                name: "IX_metric_values_ShipperId",
                table: "metric_values",
                column: "ShipperId");

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
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reporttype_code",
                table: "report_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_IngestionJobId",
                table: "reports",
                column: "IngestionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_reports_Period_Type",
                table: "reports",
                columns: new[] { "ReportingPeriod", "ReportTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReportTypeId",
                table: "reports",
                column: "ReportTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_shipper_product_classes_ProductClassId",
                table: "shipper_product_classes",
                column: "ProductClassId");

            migrationBuilder.CreateIndex(
                name: "IX_shipperAliases_ShipperId_IsActive",
                table: "shipperAlias",
                columns: new[] { "ShipperId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_shipper_short_code",
                table: "shippers",
                column: "short_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_valerr_file",
                table: "validation_errors",
                column: "IngestionFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metric_values");

            migrationBuilder.DropTable(
                name: "pafa_user_roles");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "shipper_product_classes");

            migrationBuilder.DropTable(
                name: "shipperAlias");

            migrationBuilder.DropTable(
                name: "validation_errors");

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
