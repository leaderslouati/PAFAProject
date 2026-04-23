using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddValidationNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "validation_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IngestionFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReportingPeriod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Recipients = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TotalErrors = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ErrorDetail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_validation_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_validation_notifications_ingestion_files_IngestionFileId",
                        column: x => x.IngestionFileId,
                        principalTable: "ingestion_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_val_notif_file",
                table: "validation_notifications",
                column: "IngestionFileId");

            migrationBuilder.CreateIndex(
                name: "ix_val_notif_sent_at",
                table: "validation_notifications",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "validation_notifications");
        }
    }
}
