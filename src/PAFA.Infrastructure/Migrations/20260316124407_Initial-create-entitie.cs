using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initialcreateentitie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "validation_errors",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "shippers",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                defaultValueSql: "decode('', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "shipper_product_classes",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                defaultValueSql: "decode('', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "reports",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "report_types",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldDefaultValueSql: "decode('', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "product_classes",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldDefaultValueSql: "decode('', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "metric_values",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "ingestion_jobs",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "ingestion_files",
                type: "bytea",
                rowVersion: true,
                nullable: true,
                defaultValueSql: "decode('', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true);

            migrationBuilder.UpdateData(
                table: "product_classes",
                keyColumn: "Id",
                keyValue: 1,
                column: "RowVersion",
                value: null);

            migrationBuilder.UpdateData(
                table: "product_classes",
                keyColumn: "Id",
                keyValue: 2,
                column: "RowVersion",
                value: null);

            migrationBuilder.UpdateData(
                table: "product_classes",
                keyColumn: "Id",
                keyValue: 3,
                column: "RowVersion",
                value: null);

            migrationBuilder.UpdateData(
                table: "product_classes",
                keyColumn: "Id",
                keyValue: 4,
                column: "RowVersion",
                value: null);

            migrationBuilder.UpdateData(
                table: "report_types",
                keyColumn: "Id",
                keyValue: 1,
                column: "RowVersion",
                value: null);

            migrationBuilder.UpdateData(
                table: "report_types",
                keyColumn: "Id",
                keyValue: 2,
                column: "RowVersion",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "validation_errors",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "shippers",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true,
                oldDefaultValueSql: "decode('', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "shipper_product_classes",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true,
                oldDefaultValueSql: "decode('', 'hex')");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "reports",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "report_types",
                type: "bytea",
                nullable: false,
                defaultValueSql: "decode('', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "product_classes",
                type: "bytea",
                nullable: false,
                defaultValueSql: "decode('', 'hex')",
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "metric_values",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "ingestion_jobs",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "ingestion_files",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldNullable: true,
                oldDefaultValueSql: "decode('', 'hex')");

            migrationBuilder.UpdateData(
                table: "product_classes",
                keyColumn: "Id",
                keyValue: 1,
                column: "RowVersion",
                value: new byte[0]);

            migrationBuilder.UpdateData(
                table: "product_classes",
                keyColumn: "Id",
                keyValue: 2,
                column: "RowVersion",
                value: new byte[0]);

            migrationBuilder.UpdateData(
                table: "product_classes",
                keyColumn: "Id",
                keyValue: 3,
                column: "RowVersion",
                value: new byte[0]);

            migrationBuilder.UpdateData(
                table: "product_classes",
                keyColumn: "Id",
                keyValue: 4,
                column: "RowVersion",
                value: new byte[0]);

            migrationBuilder.UpdateData(
                table: "report_types",
                keyColumn: "Id",
                keyValue: 1,
                column: "RowVersion",
                value: new byte[0]);

            migrationBuilder.UpdateData(
                table: "report_types",
                keyColumn: "Id",
                keyValue: 2,
                column: "RowVersion",
                value: new byte[0]);
        }
    }
}
