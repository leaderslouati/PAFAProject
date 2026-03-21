using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using System.Globalization;

namespace PAFA.Infrastructure.EntityConfigurations;
public class DimCalendarConfiguration : IEntityTypeConfiguration<DimCalendar>
{
    public void Configure(EntityTypeBuilder<DimCalendar> builder)
    {
        builder.ToTable("dim_calendar");
        builder.HasKey(x => x.ReportMonth);

        builder.Property(x => x.ReportMonth)
               .HasColumnName("report_month")
               .HasMaxLength(7)
               .IsRequired();

        builder.Property(x => x.Year)
               .HasColumnName("year");

        builder.Property(x => x.MonthNum)
               .HasColumnName("month_num");

        builder.Property(x => x.MonthLabel)
               .HasColumnName("month_label")
               .HasMaxLength(30);

        builder.Property(x => x.Quarter)
               .HasColumnName("quarter")
               .HasMaxLength(2);

        // Seed 2024-01 → 2025-12
        builder.HasData(GenerateSeed());
    }

    private static IEnumerable<DimCalendar> GenerateSeed()
    {
        var months = new List<DimCalendar>();

        for (int year = 2024; year <= 2025; year++)
        {
            for (int month = 1; month <= 12; month++)
            {
                // Use InvariantCulture so "MMMM" always produces English month names
                // regardless of developer machine locale — keeps seed deterministic.
                var d = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

                months.Add(new DimCalendar
                {
                    ReportMonth = $"{year:D4}-{month:D2}",
                    Year = year,
                    MonthNum = month,
                    MonthLabel = d.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                    Quarter = $"Q{(month - 1) / 3 + 1}"
                });
            }
        }

        return months;
    }
}


