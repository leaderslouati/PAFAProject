using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperAliasEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "shippers",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "shippers",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "shippers",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "shippers",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "shippers",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "shippers",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "shippers",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "shippers",
                keyColumn: "Id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000008"));

            migrationBuilder.CreateTable(
                name: "shipperAlias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShipperId = table.Column<Guid>(type: "uuid", nullable: false),
                    alias_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shipperAlias_ShipperId",
                table: "shipperAlias",
                column: "ShipperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipperAlias");

            migrationBuilder.InsertData(
                table: "shippers",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Email", "is_active", "IsDeleted", "legal_entity", "MarketEntryDate", "MarketExitDate", "name", "PortfolioSize", "RowVersion", "short_code", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SEED", null, true, false, "Alpha Gas Limited", null, null, "Alpha Gas Ltd", null, null, "SHIP_A", null, null },
                    { new Guid("a0000001-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SEED", null, true, false, "Beta Energy PLC", null, null, "Beta Energy plc", null, null, "SHIP_B", null, null },
                    { new Guid("a0000001-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SEED", null, true, false, "Gamma Supply Limited", null, null, "Gamma Supply Ltd", null, null, "SHIP_C", null, null },
                    { new Guid("a0000001-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SEED", null, true, false, "Delta Gas Company", null, null, "Delta Gas Co", null, null, "SHIP_D", null, null },
                    { new Guid("a0000001-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SEED", null, true, false, "Epsilon Energy Ltd", null, null, "Epsilon Energy", null, null, "SHIP_E", null, null },
                    { new Guid("a0000001-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SEED", null, true, false, "Zeta Gas Limited", null, null, "Zeta Gas Ltd", null, null, "SHIP_F", null, null },
                    { new Guid("a0000001-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SEED", null, true, false, "Eta Supply PLC", null, null, "Eta Supply plc", null, null, "SHIP_G", null, null },
                    { new Guid("a0000001-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SEED", null, true, false, "Theta Gas Corporation", null, null, "Theta Gas Corp", null, null, "SHIP_H", null, null }
                });
        }
    }
}
