// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/EntityConfigurations/MetricDefinitionConfiguration.cs
//
// MANQUANT — À CRÉER
// Configuration EF Core + seed des ~45 MetricCodes PARR.
// ═══════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities.Referential;

namespace PAFA.Infrastructure.EntityConfigurations;

public class MetricDefinitionConfiguration : IEntityTypeConfiguration<MetricDefinition>
{
    public void Configure(EntityTypeBuilder<MetricDefinition> b)
    {
        b.ToTable("metric_definitions");

        b.HasKey(x => x.MetricCode);
        b.Property(x => x.MetricCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.ReportCode).HasMaxLength(10).IsRequired();
        b.Property(x => x.Label).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        b.Property(x => x.ExtraDimCategory).HasMaxLength(50);

        b.HasOne(x => x.Report)
            .WithMany(r => r.Metrics)
            .HasForeignKey(x => x.ReportCode)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Seed des métriques par rapport ───────────────────────────────────
        b.HasData(
            // 2A.1 — Estimated & Check Reads
            new MetricDefinition { MetricCode = "EST_PCT",              ReportCode = "2A.1",   Label = "Percentage of Estimated Reads",              Unit = "Pct",   ExtraDimCategory = null          },
            new MetricDefinition { MetricCode = "CHECK_COUNT",          ReportCode = "2A.1",   Label = "Count of Check Reads not completed",         Unit = "Count", ExtraDimCategory = null          },

            // 2A.2 — No Meter Recorded
            new MetricDefinition { MetricCode = "NO_METER_PCT",         ReportCode = "2A.2",   Label = "Percentage SP with no meter recorded",       Unit = "Pct",   ExtraDimCategory = null          },

            // 2A.3 — No Meter + Data Flows
            new MetricDefinition { MetricCode = "NO_METER_FLOW_PCT",    ReportCode = "2A.3",   Label = "Percentage SP no meter but data flows received", Unit = "Pct", ExtraDimCategory = null       },

            // 2A.4 — Shipper Transfer Read Performance
            new MetricDefinition { MetricCode = "TRANSFER_READ_PCT",    ReportCode = "2A.4",   Label = "Transfer Read Performance Percentage",       Unit = "Pct",   ExtraDimCategory = null          },
            new MetricDefinition { MetricCode = "TRANSFER_COUNT",       ReportCode = "2A.4",   Label = "Number of Transfers",                        Unit = "Count", ExtraDimCategory = null          },

            // 2A.5 — Read Performance
            new MetricDefinition { MetricCode = "READ_PERF_PCT",        ReportCode = "2A.5",   Label = "Percentage of Read Submissions",             Unit = "Pct",   ExtraDimCategory = null          },

            // 2A.6 — Meter Read Validity Monitoring
            new MetricDefinition { MetricCode = "MRE01026",             ReportCode = "2A.6",   Label = "MRE01026 – Lower Outer Tolerance breached",  Unit = "Pct",   ExtraDimCategory = "MRECode"     },
            new MetricDefinition { MetricCode = "MRE01027",             ReportCode = "2A.6",   Label = "MRE01027 – Upper Outer Tolerance breached",  Unit = "Pct",   ExtraDimCategory = "MRECode"     },
            new MetricDefinition { MetricCode = "MRE01028",             ReportCode = "2A.6",   Label = "MRE01028 – Lower Inner Tolerance breached",  Unit = "Pct",   ExtraDimCategory = "MRECode"     },
            new MetricDefinition { MetricCode = "MRE01029",             ReportCode = "2A.6",   Label = "MRE01029 – Upper Inner Tolerance breached",  Unit = "Pct",   ExtraDimCategory = "MRECode"     },
            new MetricDefinition { MetricCode = "MRE01030",             ReportCode = "2A.6",   Label = "MRE01030 – Override Tolerance passed",       Unit = "Pct",   ExtraDimCategory = "MRECode"     },

            // 2A.7 — No Reads 1,2,3,4 Years
            new MetricDefinition { MetricCode = "NO_READ_1YR",          ReportCode = "2A.7",   Label = "No read for 1 year",                         Unit = "Pct",   ExtraDimCategory = "YearBand"    },
            new MetricDefinition { MetricCode = "NO_READ_2YR",          ReportCode = "2A.7",   Label = "No read for 2 years",                        Unit = "Pct",   ExtraDimCategory = "YearBand"    },
            new MetricDefinition { MetricCode = "NO_READ_3YR",          ReportCode = "2A.7",   Label = "No read for 3 years",                        Unit = "Pct",   ExtraDimCategory = "YearBand"    },
            new MetricDefinition { MetricCode = "NO_READ_4YR",          ReportCode = "2A.7",   Label = "No read for 4 years",                        Unit = "Pct",   ExtraDimCategory = "YearBand"    },

            // 2A.8 — AQ Correction by Reason Code (stocké dans aq_corrections_by_reason)
            new MetricDefinition { MetricCode = "AQ_CORR_REASON_COUNT", ReportCode = "2A.8",   Label = "Count of AQ Corrections by Reason Code",    Unit = "Count", ExtraDimCategory = "ReasonCode"  },

            // 2A.9 — Standard CF AQ > 732k
            new MetricDefinition { MetricCode = "STD_CF_COUNT",         ReportCode = "2A.9",   Label = "Count of sites using standard CF (>732k)",  Unit = "Count", ExtraDimCategory = "EUC"         },

            // 2A.10 — Replaced Meter Reads
            new MetricDefinition { MetricCode = "REPLACED_READ_COUNT",  ReportCode = "2A.10",  Label = "Count of replaced meter reads",              Unit = "Count", ExtraDimCategory = "EUC"         },

            // 2A.11a — Sites above Class 1 threshold
            new MetricDefinition { MetricCode = "CLASS1_ABOVE_COUNT",   ReportCode = "2A.11a", Label = "Count of SP above Class 1 threshold",       Unit = "Count", ExtraDimCategory = null           },
            new MetricDefinition { MetricCode = "CLASS1_ABOVE_AQ",      ReportCode = "2A.11a", Label = "Total AQ of SP above Class 1 threshold",    Unit = "GWh",   ExtraDimCategory = null           },

            // 2A.11b — Sites reclassified to Class 1
            new MetricDefinition { MetricCode = "RECLASSIFIED_COUNT",   ReportCode = "2A.11b", Label = "Count of SP reclassified to Class 1",       Unit = "Count", ExtraDimCategory = null           },

            // 2A.12a/b/c — AQ Read Performance PC4
            new MetricDefinition { MetricCode = "AQ_READ_PERF_PCT",     ReportCode = "2A.12a", Label = "AQ Read Performance % (Monthly ≥293k)",    Unit = "Pct",   ExtraDimCategory = "ObligationType" },
            // NOTE: 2A.12b et 2A.12c partagent le même MetricCode avec un LookupValue différent
            // → Le ReportCode distingue les 3 sous-rapports dans les SPs SQL.

            // 2A.13 — AQ at Risk
            new MetricDefinition { MetricCode = "AQ_AT_RISK_PCT",       ReportCode = "2A.13",  Label = "Percentage AQ overdue for meter reading",   Unit = "Pct",   ExtraDimCategory = "ObligationType" },
            new MetricDefinition { MetricCode = "AQ_AT_RISK_GWH",       ReportCode = "2A.13",  Label = "Total AQ overdue for meter reading (GWh)",  Unit = "GWh",   ExtraDimCategory = "ObligationType" },

            // 2A.14 — Energy Theft
            new MetricDefinition { MetricCode = "THEFT_CLAIM_OBJ_PCT",       ReportCode = "2A.14", Label = "% Energy Theft Claims objected (P41)",       Unit = "Pct",   ExtraDimCategory = "PeriodCode" },
            new MetricDefinition { MetricCode = "THEFT_CLAIM_ENERGY_PCT",     ReportCode = "2A.14", Label = "% Energy value of Theft Claims (P41)",       Unit = "Pct",   ExtraDimCategory = "PeriodCode" },
            new MetricDefinition { MetricCode = "THEFT_P106_CLAIM_OBJ_PCT",   ReportCode = "2A.14", Label = "% Energy Theft Claims objected (P106)",      Unit = "Pct",   ExtraDimCategory = "PeriodCode" },
            new MetricDefinition { MetricCode = "THEFT_P106_CLAIM_ENERGY_PCT",ReportCode = "2A.14", Label = "% Energy value of Theft Claims (P106)",      Unit = "Pct",   ExtraDimCategory = "PeriodCode" },
            new MetricDefinition { MetricCode = "THEFT_WD_OBJ_PCT",           ReportCode = "2A.14", Label = "% Energy Theft Withdrawals objected (P41)",  Unit = "Pct",   ExtraDimCategory = "PeriodCode" },
            new MetricDefinition { MetricCode = "THEFT_WD_ENERGY_PCT",        ReportCode = "2A.14", Label = "% Energy value of Withdrawals (P41)",        Unit = "Pct",   ExtraDimCategory = "PeriodCode" },
            new MetricDefinition { MetricCode = "THEFT_P106_WD_OBJ_PCT",      ReportCode = "2A.14", Label = "% Energy Theft Withdrawals objected (P106)", Unit = "Pct",   ExtraDimCategory = "PeriodCode" },
            new MetricDefinition { MetricCode = "THEFT_P106_WD_ENERGY_PCT",   ReportCode = "2A.14", Label = "% Energy value of Withdrawals (P106)",       Unit = "Pct",   ExtraDimCategory = "PeriodCode" },

            // 2A.15 — CDSP Sites Converted PC4
            new MetricDefinition { MetricCode = "CLASS3_CONV_COUNT",    ReportCode = "2A.15",  Label = "Count of SP converted from PC2/3 to PC4",  Unit = "Count", ExtraDimCategory = null           },
            new MetricDefinition { MetricCode = "CLASS3_CONV_PCT",      ReportCode = "2A.15",  Label = "Percentage of SP converted to PC4",         Unit = "Pct",   ExtraDimCategory = null           },

            // 2A.16 — PC2/PC3 Min Percentage
            new MetricDefinition { MetricCode = "MIN_PCT_REQ_PCT",      ReportCode = "2A.16",  Label = "Percentage meeting Min Read Requirement",   Unit = "Pct",   ExtraDimCategory = null           },
            new MetricDefinition { MetricCode = "MIN_PCT_NOT_MET_COUNT", ReportCode = "2A.16", Label = "Count of SP not meeting Min Read Req.",     Unit = "Count", ExtraDimCategory = null           },

            // 2A.17 — IGT Must Read Process
            new MetricDefinition { MetricCode = "MPRN_REMOVED_PCT",     ReportCode = "2A.17",  Label = "Percentage MPRNs removed from Must Read",   Unit = "Pct",   ExtraDimCategory = "AgeBucket"    },
            new MetricDefinition { MetricCode = "MUST_READ_AGE_PCT",    ReportCode = "2A.17",  Label = "Percentage MPRNs removed by Age Bucket",    Unit = "Pct",   ExtraDimCategory = "AgeBucket"    },
            new MetricDefinition { MetricCode = "MPRN_ENTERING_COUNT",  ReportCode = "2A.17",  Label = "Count MPRNs entering Must Read",             Unit = "Count", ExtraDimCategory = null           },

            // 2A.18 — Corrective Opening Meter Reading Rejections
            new MetricDefinition { MetricCode = "COMR_COUNT",              ReportCode = "2A.18", Label = "Count of Corrective Opening Meter Readings", Unit = "Count", ExtraDimCategory = null          },
            new MetricDefinition { MetricCode = "COMR_REJECT_RECV_PCT",    ReportCode = "2A.18", Label = "% Rejections received on COMRs",             Unit = "Pct",   ExtraDimCategory = null          },
            new MetricDefinition { MetricCode = "COMR_REJECT_RAISED_PCT",  ReportCode = "2A.18", Label = "% Rejections raised against COMRs",          Unit = "Pct",   ExtraDimCategory = null          },

            // 2A.19 — Class 4 Vacant Sites
            new MetricDefinition { MetricCode = "VACANT_IN_MONTH_COUNT",   ReportCode = "2A.19", Label = "Count of sites set to Vacant in month",      Unit = "Count", ExtraDimCategory = null          },
            new MetricDefinition { MetricCode = "VACANT_EOD_COUNT",        ReportCode = "2A.19", Label = "Count of Vacant sites at end of month",       Unit = "Count", ExtraDimCategory = null          },
            new MetricDefinition { MetricCode = "VACANT_PROPORTION_AGE",   ReportCode = "2A.19", Label = "Proportion of Vacant sites by Age Bucket",    Unit = "Pct",   ExtraDimCategory = "AgeBucket"   },

            // 2B.11 — AQ Portfolio (Rpt_1364 — 8 sous-rapports)
            new MetricDefinition { MetricCode = "AQ_CALC_PCT",      ReportCode = "2B.11", Label = "% Portfolio AQ calculated in month",     Unit = "Pct",   ExtraDimCategory = "EUC" },
            new MetricDefinition { MetricCode = "AQ_INC_PCT",       ReportCode = "2B.11", Label = "% AQ in increase (rolling 12m)",          Unit = "Pct",   ExtraDimCategory = "EUC" },
            new MetricDefinition { MetricCode = "AQ_DEC_PCT",       ReportCode = "2B.11", Label = "% AQ in decrease (rolling 12m)",          Unit = "Pct",   ExtraDimCategory = "EUC" },
            new MetricDefinition { MetricCode = "AQ_CALC_FREQ_1M",  ReportCode = "2B.11", Label = "AQ Calculation Frequency – 1 month",      Unit = "Pct",   ExtraDimCategory = "EUC" },
            new MetricDefinition { MetricCode = "AQ_CALC_FREQ_4M",  ReportCode = "2B.11", Label = "AQ Calculation Frequency – 4 months",     Unit = "Pct",   ExtraDimCategory = "EUC" },
            new MetricDefinition { MetricCode = "AQ_CALC_FREQ_12M", ReportCode = "2B.11", Label = "AQ Calculation Frequency – 12 months",    Unit = "Pct",   ExtraDimCategory = "EUC" },
            new MetricDefinition { MetricCode = "AQ_CALC_FREQ_36M", ReportCode = "2B.11", Label = "AQ Calculation Frequency – 36+ months",   Unit = "Pct",   ExtraDimCategory = "EUC" },
            new MetricDefinition { MetricCode = "AQ_FAIL_COUNT",    ReportCode = "2B.11", Label = "Count of AQ calculation failures",        Unit = "Count", ExtraDimCategory = "EUC" }
        );
    }
}
