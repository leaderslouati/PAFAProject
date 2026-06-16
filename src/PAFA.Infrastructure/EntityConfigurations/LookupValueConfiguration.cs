// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/EntityConfigurations/LookupValueConfiguration.cs
//
// MANQUANT — À CRÉER
// Configuration EF Core + seed de toutes les valeurs de référence
// multi-catégories (26 lignes : ReasonCode 01-09, AgeBucket, 
// ObligationType, YearBand, MRECode, PeriodCode).
// ═══════════════════════════════════════════════════════════
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.EntityConfigurations;

public class LookupValueConfiguration : IEntityTypeConfiguration<LookupValue>
{
    public void Configure(EntityTypeBuilder<LookupValue> b)
    {
        b.ToTable("lookup_values");

        b.HasKey(x => x.LookupId);

        b.Property(x => x.LookupId).ValueGeneratedNever();

        b.Property(x => x.Category)
            .HasMaxLength(50)
            .IsRequired();

        b.Property(x => x.Code)
            .HasMaxLength(50)
            .IsRequired();

        b.Property(x => x.Label)
            .HasMaxLength(200)
            .IsRequired();

        b.Property(x => x.SortOrder).IsRequired();

        // ── Index unique pour éviter les doublons (Category, Code) ──────────
        b.HasIndex(x => new { x.Category, x.Code })
            .IsUnique()
            .HasDatabaseName("IX_lookup_values_Category_Code");

        // ── Seed 26 valeurs (toutes catégories) ─────────────────────────────
        b.HasData(

            // ── ReasonCode (Rapport 2A.8 — AQ Correction by Reason Code) ────
            new LookupValue { LookupId = 1,  Category = "ReasonCode", Code = "01", Label = "Confirmed Theft",                                    SortOrder = 1 },
            new LookupValue { LookupId = 2,  Category = "ReasonCode", Code = "02", Label = "Change in Consumer Plant",                           SortOrder = 2 },
            new LookupValue { LookupId = 3,  Category = "ReasonCode", Code = "03", Label = "Commencement of New Business Activity",              SortOrder = 3 },
            new LookupValue { LookupId = 4,  Category = "ReasonCode", Code = "04", Label = "Tolerance Change",                                   SortOrder = 4 },
            new LookupValue { LookupId = 5,  Category = "ReasonCode", Code = "05", Label = "Winter Consumption",                                 SortOrder = 5 }, // ← NOUVEAU (demandé changement 2A.8)
            new LookupValue { LookupId = 6,  Category = "ReasonCode", Code = "06", Label = "Erroneous AQ based on Read History",                 SortOrder = 6 },
            new LookupValue { LookupId = 7,  Category = "ReasonCode", Code = "07", Label = "Erroneous AQ – Change in operation and/or use",      SortOrder = 7 },
            new LookupValue { LookupId = 8,  Category = "ReasonCode", Code = "08", Label = "AQ decrease due to the site being Vacant",           SortOrder = 8 },
            new LookupValue { LookupId = 9,  Category = "ReasonCode", Code = "09", Label = "AQ increase due to the site no longer being Vacant", SortOrder = 9 },

            // ── AgeBucket (Rapports 2A.17, 2A.19 — Must Read / Vacant Sites) ─
            new LookupValue { LookupId = 10, Category = "AgeBucket", Code = "a_0_6",    Label = "a. 0 – 6 months",   SortOrder = 1 },
            new LookupValue { LookupId = 11, Category = "AgeBucket", Code = "b_6_12",   Label = "b. 6 – 12 months",  SortOrder = 2 },
            new LookupValue { LookupId = 12, Category = "AgeBucket", Code = "c_12_plus", Label = "c. 12 months +",   SortOrder = 3 },

            // ── ObligationType (Rapports 2A.12a/b/c, 2A.13) ─────────────────
            new LookupValue { LookupId = 13, Category = "ObligationType", Code = "MONTHLY_293K", Label = "Monthly Read – AQ ≥ 293,000 kWh",          SortOrder = 1 },
            new LookupValue { LookupId = 14, Category = "ObligationType", Code = "SMART_AMR",    Label = "Monthly Read – SMART/AMR < 293,000 kWh",    SortOrder = 2 },
            new LookupValue { LookupId = 15, Category = "ObligationType", Code = "ANNUAL",       Label = "Annual Read – non-Smart < 293,000 kWh",     SortOrder = 3 },

            // ── YearBand (Rapport 2A.7 — No Reads 1,2,3,4 Years) ────────────
            new LookupValue { LookupId = 16, Category = "YearBand", Code = "1YR", Label = "1 Year",  SortOrder = 1 },
            new LookupValue { LookupId = 17, Category = "YearBand", Code = "2YR", Label = "2 Years", SortOrder = 2 },
            new LookupValue { LookupId = 18, Category = "YearBand", Code = "3YR", Label = "3 Years", SortOrder = 3 },
            new LookupValue { LookupId = 19, Category = "YearBand", Code = "4YR", Label = "4 Years", SortOrder = 4 },

            // ── MRECode (Rapport 2A.6 — Meter Read Validity Monitoring) ──────
            new LookupValue { LookupId = 20, Category = "MRECode", Code = "MRE01026", Label = "Reading breached the Lower Outer Tolerance",                               SortOrder = 1 },
            new LookupValue { LookupId = 21, Category = "MRECode", Code = "MRE01027", Label = "Reading breached the Upper Outer Tolerance",                               SortOrder = 2 },
            new LookupValue { LookupId = 22, Category = "MRECode", Code = "MRE01028", Label = "Reading breached the Lower Inner Tolerance – no override flag provided",   SortOrder = 3 },
            new LookupValue { LookupId = 23, Category = "MRECode", Code = "MRE01029", Label = "Reading breached the Upper Inner Tolerance – no override flag provided",   SortOrder = 4 },
            new LookupValue { LookupId = 24, Category = "MRECode", Code = "MRE01030", Label = "Override Tolerance passed & override flag provided",                       SortOrder = 5 },

            // ── PeriodCode (Rapport 2A.14 — Energy Theft P41/P106) ───────────
            new LookupValue { LookupId = 25, Category = "PeriodCode", Code = "P41",  Label = "UNC Modification P41",  SortOrder = 1 },
            new LookupValue { LookupId = 26, Category = "PeriodCode", Code = "P106", Label = "UNC Modification P106", SortOrder = 2 }
        );
    }
}
