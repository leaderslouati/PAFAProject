using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "decode('', 'hex')")
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "decode('', 'hex')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shippers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    LegalEntity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MarketEntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MarketExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PortfolioSize = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
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
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ReportTypeId = table.Column<int>(type: "integer", nullable: false),
                    ScheduleNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    Audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FilePath_PDF = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FilePath_Excel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FilePath_PPTX = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CommentaryText = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CommentaryBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsBaseline = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
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
                    TotalAQ_MWH = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
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
                name: "metric_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IngestionFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipperShortCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    ReportingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
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
                        principalColumn: "Id");
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
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
                table: "product_classes",
                columns: new[] { "Id", "AQThresholdHigh", "AQThresholdLow", "Code", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsDeleted", "MinReadPercentage", "RowVersion", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, null, 732m, "PC1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Large sites — AQ ≥ 732 MWH", true, false, 97.5m, new byte[0], null, null },
                    { 2, null, null, "PC2", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Medium NDM", true, false, null, new byte[0], null, null },
                    { 3, null, null, "PC3", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Small NDM WAR", true, false, null, new byte[0], null, null },
                    { 4, null, null, "PC4", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "IGT Small", true, false, null, new byte[0], null, null }
                });

            migrationBuilder.InsertData(
                table: "report_types",
                columns: new[] { "Id", "Audience", "Code", "CreatedAt", "CreatedBy", "IsActive", "IsDeleted", "Label", "ReportCount", "RowVersion", "ScheduleRef", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "Industry", "SCH2A", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, false, "Industry Peer Comparison (Anonymised)", 19, new byte[0], "Schedule 2A", null, null },
                    { 2, "PAC", "SCH2B", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, false, "Performance Assurance Committee (Non-Anonymised)", 22, new byte[0], "Schedule 2B", null, null }
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
                name: "IX_metric_values_ShipperId",
                table: "metric_values",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "ix_mv_metric_key",
                table: "metric_values",
                column: "MetricKey");

            migrationBuilder.CreateIndex(
                name: "ix_mv_period",
                table: "metric_values",
                column: "ReportingPeriod");

            migrationBuilder.CreateIndex(
                name: "ix_mv_period_key",
                table: "metric_values",
                columns: new[] { "ReportingPeriod", "MetricKey" });

            migrationBuilder.CreateIndex(
                name: "ix_mv_ssc",
                table: "metric_values",
                column: "ShipperShortCode");

            migrationBuilder.CreateIndex(
                name: "ix_mv_unique",
                table: "metric_values",
                columns: new[] { "IngestionFileId", "ShipperShortCode", "ReportingPeriod", "MetricKey" },
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
                name: "ix_report_status",
                table: "reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_report_unique",
                table: "reports",
                columns: new[] { "ReportTypeId", "ReportingPeriod", "ScheduleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipper_product_classes_ProductClassId",
                table: "shipper_product_classes",
                column: "ProductClassId");

            migrationBuilder.CreateIndex(
                name: "ix_shipper_ssc",
                table: "shippers",
                column: "ShortCode",
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
                name: "reports");

            migrationBuilder.DropTable(
                name: "shipper_product_classes");

            migrationBuilder.DropTable(
                name: "validation_errors");

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
