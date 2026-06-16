// ═══════════════════════════════════════════════════════════
// MODIFICATION À APPORTER dans MetricValues.cs (fichier existant)
//
// Ajouter les 3 propriétés ci-dessous dans la classe MetricValue.
// Ces propriétés permettent de lier une metric_value à :
//   1. Un rapport (ReportCode → report_definitions)
//   2. Une bande EUC (EucCode → euc_bands) pour 2A.7, 2A.9, 2A.10
//   3. Une valeur de lookup (LookupValueId → lookup_values)
//      pour AgeBucket, ObligationType, YearBand, MRECode, PeriodCode, ReasonCode
// ═══════════════════════════════════════════════════════════

// Dans la classe MetricValue, après la propriété ProductClassCode, ajouter :

/*
    // ── Nouvelles propriétés PARR ────────────────────────────────────────────

    /// <summary>
    /// Code du rapport source.
    /// FK nullable vers report_definitions.ReportCode.
    /// Exemples : "2A.1", "2A.5", "2B.11".
    /// NULL pendant la migration des données existantes.
    /// </summary>
    public string? ReportCode { get; set; }

    /// <summary>
    /// Code de la bande EUC pour les métriques dimensionnées par EUC.
    /// FK nullable vers euc_bands.EucCode.
    /// Renseigné pour : 2A.7 (No Reads), 2A.9 (Standard CF), 2A.10 (Replaced Reads).
    /// NULL pour les métriques sans dimension EUC.
    /// </summary>
    public string? EucCode { get; set; }

    /// <summary>
    /// FK nullable vers lookup_values.LookupId.
    /// Renseigné pour les métriques ayant une dimension secondaire :
    ///   - AgeBucket (2A.17, 2A.19)
    ///   - ObligationType (2A.12, 2A.13)
    ///   - YearBand (2A.7)
    ///   - MRECode (2A.6)
    ///   - PeriodCode (2A.14)
    /// NULL pour les métriques simples sans dimension de lookup.
    /// </summary>
    public int? LookupValueId { get; set; }

    // ── Navigation pour LookupValue ──────────────────────────────────────────
    public LookupValue? LookupValue { get; set; }
*/

// ── Et dans MetricValueConfiguration.cs, ajouter dans Configure() ────────────

/*
    // Nouvelles colonnes
    b.Property(e => e.ReportCode).HasMaxLength(10);
    b.Property(e => e.EucCode).HasMaxLength(10);
    b.Property(e => e.LookupValueId).IsRequired(false);

    // FK → ReportDefinition
    b.HasOne<ReportDefinition>()
        .WithMany()
        .HasForeignKey(e => e.ReportCode)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // FK → LookupValue
    b.HasOne(e => e.LookupValue)
        .WithMany()
        .HasForeignKey(e => e.LookupValueId)
        .IsRequired(false)
        .OnDelete(DeleteBehavior.Restrict);

    // Index étendu pour couvrir les nouvelles requêtes de reporting
    b.HasIndex(e => new { e.ReportCode, e.ReportingPeriod, e.ShipperId, e.ProductClassCode, e.EucCode, e.LookupValueId })
        .HasDatabaseName("IX_metric_values_Report_Period_Shipper_PC_EUC_Lookup");
*/
