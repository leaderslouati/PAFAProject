using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEntitiesShippers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipper_alias");

            migrationBuilder.AddColumn<string>(
                name: "alias_code",
                table: "shippers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "alias_code",
                table: "shippers");

            migrationBuilder.CreateTable(
                name: "shipper_alias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shipper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "ix_shipper_alias_code",
                table: "shipper_alias",
                column: "alias_code");

            migrationBuilder.CreateIndex(
                name: "ix_shipper_alias_shipper_id",
                table: "shipper_alias",
                column: "shipper_id");
        }
    }
}
