// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Persistence/PafaDbContext_ADDITIONS.cs
//
// CE FICHIER N'EST PAS UN FICHIER À CRÉER TEL QUEL.
// Il montre exactement ce qu'il faut AJOUTER dans votre PafaDbContext.cs existant.
//
// ÉTAPE 1 : Ajouter les 6 DbSets manquants
// ÉTAPE 2 : Enregistrer les 6 nouvelles configurations dans OnModelCreating
// ═══════════════════════════════════════════════════════════

/*
 * ────────────────────────────────────────────────────────────
 * SECTION 1 — DbSets à ajouter dans PafaDbContext.cs
 * (après les DbSets existants)
 * ────────────────────────────────────────────────────────────
 *
 * // ── Nouvelles entités référentiel ──────────────────────
 * public DbSet<EucBand>              EucBands              => Set<EucBand>();
 * public DbSet<ReportDefinition>     ReportDefinitions     => Set<ReportDefinition>();
 * public DbSet<MetricDefinition>     MetricDefinitions     => Set<MetricDefinition>();
 * public DbSet<LookupValue>          LookupValues          => Set<LookupValue>();
 *
 * // ── Nouvelles tables de faits ──────────────────────────
 * public DbSet<AqCorrectionByReason> AqCorrectionsByReason => Set<AqCorrectionByReason>();
 * public DbSet<SupplyPointSnapshot>  SupplyPointSnapshots  => Set<SupplyPointSnapshot>();
 *
 * ────────────────────────────────────────────────────────────
 * SECTION 2 — Configurations à ajouter dans OnModelCreating
 * (après les ApplyConfiguration existants)
 * ────────────────────────────────────────────────────────────
 *
 * // ── Nouvelles configurations ────────────────────────────
 * modelBuilder.ApplyConfiguration(new EucBandConfiguration());
 * modelBuilder.ApplyConfiguration(new ReportDefinitionConfiguration());
 * modelBuilder.ApplyConfiguration(new MetricDefinitionConfiguration());
 * modelBuilder.ApplyConfiguration(new LookupValueConfiguration());
 * modelBuilder.ApplyConfiguration(new AqCorrectionByReasonConfiguration());
 * modelBuilder.ApplyConfiguration(new SupplyPointSnapshotConfiguration());
 *
 * // ── Modifications des configurations EXISTANTES ─────────
 * // Dans ShipperConfiguration.cs — ajouter :
 * //   b.Property(x => x.AnonymisedLabel).HasMaxLength(100);
 * //   b.HasIndex(x => x.AnonymisedLabel).IsUnique();
 * //   (si AnonymisedLabel n'existe pas encore, l'ajouter dans Shipper.cs aussi)
 *
 * // Dans MetricValueConfiguration.cs — ajouter les colonnes manquantes :
 * //   b.Property(e => e.ReportCode).HasMaxLength(10);        // FK vers report_definitions
 * //   b.Property(e => e.EucCode).HasMaxLength(10);           // pour 2A.7, 2A.9, 2A.10
 * //   b.Property(e => e.LookupValueId).IsRequired(false);    // FK vers lookup_values (nullable)
 * //
 * //   + Ajouter les 3 propriétés dans MetricValue.cs :
 * //   public string? ReportCode      { get; set; }
 * //   public string? EucCode         { get; set; }
 * //   public int?    LookupValueId   { get; set; }
 * //   public LookupValue? LookupValue { get; set; }
 *
 * // Dans ValidationErrorConfiguration.cs — ajouter :
 * //   b.Property(x => x.RowNumber);   // pour inclure les 10 premiers exemples dans l'email (US6)
 * //   + Ajouter dans ValidationError.cs : public int? RowNumber { get; set; }
 *
 * // Dans IngestionJobConfiguration.cs — ajouter :
 * //   b.Property(x => x.AnonMode);   // bool — true = Anonymisé (2A), false = Non-anonymisé (2B)
 * //   + Ajouter dans IngestionJob.cs : public bool AnonMode { get; set; } = true;
 *
 * ────────────────────────────────────────────────────────────
 * SECTION 3 — Commandes EF Core à lancer après toutes les modifications
 * ────────────────────────────────────────────────────────────
 *
 * // Créer la migration
 * dotnet ef migrations add AddParrReportingLayer --context PafaDbContext -o Data/Migrations
 *
 * // Appliquer en local (développement)
 * dotnet ef database update --context PafaDbContext
 *
 * // Générer le script SQL pour la production
 * dotnet ef migrations script --output migrations_parr_reporting_layer.sql --idempotent
 *
 * ────────────────────────────────────────────────────────────
 * SECTION 4 — Tables créées par cette migration
 * ────────────────────────────────────────────────────────────
 * Nouvelles tables :
 *   - euc_bands              (9 lignes seed)
 *   - report_definitions     (23 lignes seed)
 *   - metric_definitions     (~45 lignes seed)
 *   - lookup_values          (26 lignes seed)
 *   - aq_corrections_by_reason
 *   - supply_point_snapshots
 *
 * Colonnes ajoutées aux tables existantes :
 *   - metric_values.ReportCode    VARCHAR(10)
 *   - metric_values.EucCode       VARCHAR(10)
 *   - metric_values.LookupValueId INT (nullable)
 *   - validation_errors.RowNumber INT (nullable)
 *   - ingestion_jobs.AnonMode     BIT/BOOLEAN
 *   - shippers.AnonymisedLabel    VARCHAR(100) UNIQUE (si pas encore présent)
 */
