using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
            migrationBuilder.EnsureSchema(
                name: "etl");

            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "IngestionJobs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobName = table.Column<string>(type: "text", nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FilesExpected = table.Column<int>(type: "integer", nullable: true),
                    FilesDownloaded = table.Column<int>(type: "integer", nullable: false),
                    FilesProcessed = table.Column<int>(type: "integer", nullable: false),
                    FilesFailed = table.Column<int>(type: "integer", nullable: false),
                    RecordsLoaded = table.Column<long>(type: "bigint", nullable: false),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    TriggeredBy = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ParentJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricValues",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestionFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipperShortCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductClass",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AQThresholdLow = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    AQThresholdHigh = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MinReadPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductClass", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportType",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ScheduleRef = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsAnonymised = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shipper",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LegalEntity = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MarketEntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MarketExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PortfolioSize = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipper", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngestionFile",
                schema: "etl",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IngestionJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "CDSP"),
                    FileType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    BlobPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RowsRead = table.Column<int>(type: "integer", nullable: true),
                    RowsValid = table.Column<int>(type: "integer", nullable: true),
                    RowsRejected = table.Column<int>(type: "integer", nullable: true),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngestionFile_IngestionJob",
                        column: x => x.IngestionJobId,
                        principalSchema: "public",
                        principalTable: "IngestionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Report",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ReportTypeId = table.Column<int>(type: "integer", nullable: false),
                    ScheduleNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FilePath_PDF = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FilePath_Excel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FilePath_PPTX = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CommentaryText = table.Column<string>(type: "text", nullable: true),
                    CommentaryBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsBaseline = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "SYSTEM"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Report", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Report_ReportType",
                        column: x => x.ReportTypeId,
                        principalSchema: "dbo",
                        principalTable: "ReportType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShipperProductClass",
                schema: "dbo",
                columns: table => new
                {
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductClassId = table.Column<int>(type: "integer", nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    SupplyPointCount = table.Column<int>(type: "integer", nullable: true),
                    TotalAQ_MWH = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipperProductClass", x => new { x.ShipperId, x.ProductClassId, x.PeriodYear, x.PeriodMonth });
                    table.ForeignKey(
                        name: "FK_ShipperProductClass_ProductClass",
                        column: x => x.ProductClassId,
                        principalSchema: "dbo",
                        principalTable: "ProductClass",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipperProductClass_Shipper",
                        column: x => x.ShipperId,
                        principalSchema: "dbo",
                        principalTable: "Shipper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationError",
                schema: "etl",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IngestionFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: true),
                    ColumnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OriginalValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Severity = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "ERROR"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationError", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationError_IngestionFile",
                        column: x => x.IngestionFileId,
                        principalSchema: "etl",
                        principalTable: "IngestionFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "ProductClass",
                columns: new[] { "Id", "AQThresholdHigh", "AQThresholdLow", "Code", "Description", "IsActive", "MinReadPercentage" },
                values: new object[,]
                {
                    { 1, null, null, "PC1", "Class 1 – AQ > 732 MWH (Industrial, large sites)", true, 97.5m },
                    { 2, 732m, 73.2m, "PC2", "Class 2 – Quarterly read frequency", true, null },
                    { 3, 73.2m, 0m, "PC3", "Class 3 – Annual read frequency", true, null },
                    { 4, null, 0m, "PC4", "Class 4 – Low read frequency / automated metering", true, null }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "ReportType",
                columns: new[] { "Id", "Code", "IsActive", "IsAnonymised", "Label", "ReportCount", "ScheduleRef" },
                values: new object[] { 1, "SCH2A", true, true, "Industry Peer Comparison View – Anonymised", 19, "Schedule 2A" });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "ReportType",
                columns: new[] { "Id", "Code", "IsActive", "Label", "ReportCount", "ScheduleRef" },
                values: new object[] { 2, "SCH2B", true, "Performance Assurance Committee View – Full", 22, "Schedule 2B" });

            migrationBuilder.CreateIndex(
                name: "IX_IngestionFile_Job_Status",
                schema: "etl",
                table: "IngestionFile",
                columns: new[] { "IngestionJobId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricValue_Period",
                schema: "public",
                table: "MetricValues",
                columns: new[] { "PeriodYear", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricValue_Shipper",
                schema: "public",
                table: "MetricValues",
                column: "ShipperShortCode");

            migrationBuilder.CreateIndex(
                name: "UK_ProductClass_Code",
                schema: "dbo",
                table: "ProductClass",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Report_Period",
                schema: "dbo",
                table: "Report",
                columns: new[] { "PeriodYear", "PeriodMonth", "ReportTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Report_Status",
                schema: "dbo",
                table: "Report",
                columns: new[] { "Status", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "UK_Report_Type_Schedule_Period",
                schema: "dbo",
                table: "Report",
                columns: new[] { "ReportTypeId", "ScheduleNumber", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ReportType_Code",
                schema: "dbo",
                table: "ReportType",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipper_IsActive_Name",
                schema: "dbo",
                table: "Shipper",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Shipper_Name",
                schema: "dbo",
                table: "Shipper",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UK_Shipper_ShortCode",
                schema: "dbo",
                table: "Shipper",
                column: "ShortCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipperProductClass_ProductClassId",
                schema: "dbo",
                table: "ShipperProductClass",
                column: "ProductClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipperProductClass_Shipper_Period",
                schema: "dbo",
                table: "ShipperProductClass",
                columns: new[] { "ShipperId", "PeriodYear", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_ValidationError_File_Severity",
                schema: "etl",
                table: "ValidationError",
                columns: new[] { "IngestionFileId", "Severity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetricValues",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Report",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ShipperProductClass",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ValidationError",
                schema: "etl");

            migrationBuilder.DropTable(
                name: "ReportType",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProductClass",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Shipper",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IngestionFile",
                schema: "etl");

            migrationBuilder.DropTable(
                name: "IngestionJobs",
                schema: "public");
        }
    }
}
