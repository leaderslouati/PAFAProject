using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PAFA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationIdToIngestionJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "NoMeterCount",
                table: "shipper_product_classes",
                type: "integer",
                nullable: true,
                comment: "Nb SP sans meter enregistr�. >= 0.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Nb SP sans meter enregistré. >= 0.");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedPct",
                table: "shipper_product_classes",
                type: "numeric(8,4)",
                nullable: true,
                comment: "% lectures estim�es (0-100). Source: MetricKey='EstimatedPct'.",
                oldClrType: typeof(decimal),
                oldType: "numeric(8,4)",
                oldNullable: true,
                oldComment: "% lectures estimées (0-100). Source: MetricKey='EstimatedPct'.");

            migrationBuilder.AlterColumn<int>(
                name: "CheckReadCountNotCompleted",
                table: "shipper_product_classes",
                type: "integer",
                nullable: true,
                comment: "Nb check reads non compl�t�s. >= 0.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Nb check reads non complétés. >= 0.");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ObservationsUpdatedAt",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Horodatage UTC de la derni�re mise � jour des observations.",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Horodatage UTC de la dernière mise à jour des observations.");

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "ingestion_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedRemote",
                table: "ingestion_files",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "ingestion_jobs");

            migrationBuilder.DropColumn(
                name: "LastModifiedRemote",
                table: "ingestion_files");

            migrationBuilder.AlterColumn<int>(
                name: "NoMeterCount",
                table: "shipper_product_classes",
                type: "integer",
                nullable: true,
                comment: "Nb SP sans meter enregistré. >= 0.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Nb SP sans meter enregistr�. >= 0.");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedPct",
                table: "shipper_product_classes",
                type: "numeric(8,4)",
                nullable: true,
                comment: "% lectures estimées (0-100). Source: MetricKey='EstimatedPct'.",
                oldClrType: typeof(decimal),
                oldType: "numeric(8,4)",
                oldNullable: true,
                oldComment: "% lectures estim�es (0-100). Source: MetricKey='EstimatedPct'.");

            migrationBuilder.AlterColumn<int>(
                name: "CheckReadCountNotCompleted",
                table: "shipper_product_classes",
                type: "integer",
                nullable: true,
                comment: "Nb check reads non complétés. >= 0.",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldComment: "Nb check reads non compl�t�s. >= 0.");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ObservationsUpdatedAt",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Horodatage UTC de la dernière mise à jour des observations.",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Horodatage UTC de la derni�re mise � jour des observations.");
        }
    }
}
