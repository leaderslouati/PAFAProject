using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class FactReadPerformanceConfiguration
    : IEntityTypeConfiguration<FactReadPerformance>
{
    public void Configure(EntityTypeBuilder<FactReadPerformance> builder)
    {
        // Vue SQL — pas de table physique, pas de clé
        builder.HasNoKey()
               .ToView("fact_read_performance");

        builder.Property(x => x.ReportMonth)
               .HasColumnName("report_month");

        builder.Property(x => x.ShipperCode)
               .HasColumnName("shipper_code");

        builder.Property(x => x.ProductClass)
               .HasColumnName("product_class");

        builder.Property(x => x.ReadPerfPct)
               .HasColumnName("read_perf_pct");

        builder.Property(x => x.EstimatedPct)
               .HasColumnName("estimated_pct");

        builder.Property(x => x.CheckReadCount)
               .HasColumnName("check_read_count");

        builder.Property(x => x.TotalSites)
               .HasColumnName("total_sites");

        builder.Property(x => x.IsCompliant)
               .HasColumnName("is_compliant");
    }
}