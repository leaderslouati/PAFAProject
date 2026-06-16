// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/EntityConfigurations/ReportDefinitionConfiguration.cs
//
// MANQUANT — À CRÉER
// Configuration EF Core + seed des 23 rapports PARR catalogue.
// ═══════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public void Configure(EntityTypeBuilder<ReportDefinition> b)
    {
        b.ToTable("report_definitions");

        b.HasKey(x => x.ReportCode);

        b.Property(x => x.ReportCode).HasMaxLength(10).IsRequired();
        b.Property(x => x.Topic).HasMaxLength(200).IsRequired();
        b.Property(x => x.SplitBy).HasMaxLength(100);
        b.Property(x => x.ReportFormat).HasMaxLength(50);
        b.Property(x => x.RollingMonths).HasDefaultValue(12);
        b.Property(x => x.DisplayOrder).IsRequired();

        b.HasMany(x => x.Metrics)
            .WithOne(m => m.Report)
            .HasForeignKey(m => m.ReportCode)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Seed 23 rapports (2A.1–2A.19 + 2B.11 exclusif) ─────────────────
        b.HasData(
            // ── Schedule 2A (Anonymised / Industry) ─────────────────────────
            new ReportDefinition { ReportCode = "2A.1",   Topic = "Estimated & Check Reads",                           SplitBy = "Class",              ReportFormat = "Percentage",  RollingMonths = 12, DisplayOrder = 1  },
            new ReportDefinition { ReportCode = "2A.2",   Topic = "No Meter Recorded in SP",                           SplitBy = "Class",              ReportFormat = "Percentage",  RollingMonths = 12, DisplayOrder = 2  },
            new ReportDefinition { ReportCode = "2A.3",   Topic = "No Meter Recorded + Data Flows",                    SplitBy = "Class",              ReportFormat = "Percentage",  RollingMonths = 12, DisplayOrder = 3  },
            new ReportDefinition { ReportCode = "2A.4",   Topic = "Shipper Transfer Read Performance",                 SplitBy = "Total",              ReportFormat = "Percentage",  RollingMonths = 12, DisplayOrder = 4  },
            new ReportDefinition { ReportCode = "2A.5",   Topic = "Read Performance",                                  SplitBy = "Class",              ReportFormat = "Percentage",  RollingMonths = 12, DisplayOrder = 5  },
            new ReportDefinition { ReportCode = "2A.6",   Topic = "Meter Read Validity Monitoring",                    SplitBy = "Class",              ReportFormat = "Percentage",  RollingMonths = 12, DisplayOrder = 6  },
            new ReportDefinition { ReportCode = "2A.7",   Topic = "No Reads 1,2,3,4 Years",                           SplitBy = "EUC + Class",        ReportFormat = "Percentage",  RollingMonths = 12, DisplayOrder = 7  },
            new ReportDefinition { ReportCode = "2A.8",   Topic = "AQ Correction by Reason Code",                     SplitBy = "Reason Code",        ReportFormat = "Count",       RollingMonths = 12, DisplayOrder = 8  },
            new ReportDefinition { ReportCode = "2A.9",   Topic = "Standard CF AQ > 732k kWh",                        SplitBy = "EUC Band",           ReportFormat = "Count",       RollingMonths = 12, DisplayOrder = 9  },
            new ReportDefinition { ReportCode = "2A.10",  Topic = "Replaced Meter Reads",                             SplitBy = "EUC Band",           ReportFormat = "Count",       RollingMonths = 12, DisplayOrder = 10 },
            new ReportDefinition { ReportCode = "2A.11a", Topic = "Sites above Class 1 threshold",                    SplitBy = "Current Class",      ReportFormat = "Count + AQ",  RollingMonths = 12, DisplayOrder = 11 },
            new ReportDefinition { ReportCode = "2A.11b", Topic = "Sites reclassified to Class 1",                    SplitBy = "Shipper vs CDSP",    ReportFormat = "Count + AQ",  RollingMonths = 12, DisplayOrder = 12 },
            new ReportDefinition { ReportCode = "2A.12a", Topic = "AQ Read Perf PC4 Monthly ≥293k",                  SplitBy = "Obligation",         ReportFormat = "Pct Read",    RollingMonths = 12, DisplayOrder = 13 },
            new ReportDefinition { ReportCode = "2A.12b", Topic = "AQ Read Perf PC4 Smart/AMR",                      SplitBy = "Obligation",         ReportFormat = "Pct Read",    RollingMonths = 12, DisplayOrder = 14 },
            new ReportDefinition { ReportCode = "2A.12c", Topic = "AQ Read Perf PC4 Annual",                         SplitBy = "Obligation",         ReportFormat = "Pct Read",    RollingMonths = 12, DisplayOrder = 15 },
            new ReportDefinition { ReportCode = "2A.13",  Topic = "AQ Overdue for Meter Reading",                    SplitBy = "Obligation",         ReportFormat = "Pct overdue", RollingMonths = 2,  DisplayOrder = 16 }, // ← 2 mois seulement
            new ReportDefinition { ReportCode = "2A.14",  Topic = "Confirmed Energy Theft Notifications",            SplitBy = "Claims & Withdrawals", ReportFormat = "Percentage", RollingMonths = 12, DisplayOrder = 17 },
            new ReportDefinition { ReportCode = "2A.15",  Topic = "CDSP Sites Converted PC4",                        SplitBy = "Current Class",      ReportFormat = "Count",       RollingMonths = 12, DisplayOrder = 18 },
            new ReportDefinition { ReportCode = "2A.16",  Topic = "PC2/PC3 Read vs Min Pct Requirement",            SplitBy = "Current Class",      ReportFormat = "Percentage",  RollingMonths = 12, DisplayOrder = 19 },
            new ReportDefinition { ReportCode = "2A.17",  Topic = "IGT Must Read Process",                           SplitBy = "Count/Percentage",   ReportFormat = "Pct/Count",   RollingMonths = 12, DisplayOrder = 20 },
            new ReportDefinition { ReportCode = "2A.18",  Topic = "Corrective Opening Meter Reading Rejections",    SplitBy = "Count/Percentage",   ReportFormat = "Pct/Count",   RollingMonths = 12, DisplayOrder = 21 },
            new ReportDefinition { ReportCode = "2A.19",  Topic = "Class 4 Vacant Sites",                           SplitBy = "Count/Percentage",   ReportFormat = "Pct/Count",   RollingMonths = 12, DisplayOrder = 22 },

            // ── Schedule 2B exclusif (Non-Anonymised) ────────────────────────
            // 2B.1–2B.10 et 2B.14–2B.22 réutilisent les mêmes données que 2A.
            // Seul 2B.11 est exclusif : AQ Portfolio (8 sous-rapports Rpt_1364).
            new ReportDefinition { ReportCode = "2B.11",  Topic = "AQ Portfolio Calculation (8 sub-reports)", SplitBy = "EUC × Shipper", ReportFormat = "Pct/Count", RollingMonths = 12, DisplayOrder = 23 }
        );
    }
}
