using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;

namespace PAFA.Infrastructure.Data.EntityConfigurations
{
    public class MetricValueConfiguration : IEntityTypeConfiguration<MetricValue>
    {
        public void Configure(EntityTypeBuilder<MetricValue> builder)
        {
            // Nom de la table et schéma (optionnel, "dbo" ou "public" selon ta config PostgreSQL)
            builder.ToTable("MetricValues");

            // Clé primaire
            builder.HasKey(m => m.Id);

            // Propriétés héritées de BaseEntity
            builder.Property(m => m.CreatedAt)
                   .IsRequired()
                   .HasDefaultValueSql("NOW()"); 

            builder.Property(m => m.IsDeleted)
                   .IsRequired()
                   .HasDefaultValue(false);

            builder.HasQueryFilter(m => !m.IsDeleted);

            builder.Property(m => m.ShipperShortCode)
                   .IsRequired()
                   .HasMaxLength(10); 

            builder.Property(m => m.MetricKey)
                   .IsRequired()
                   .HasMaxLength(100); // Ex: PC1_READ_PERF 

            builder.Property(m => m.Value)
                   .IsRequired()
                   .HasColumnType("decimal(18,4)"); // Précision importante pour les métriques de gaz 

            builder.Property(m => m.PeriodYear)
                   .IsRequired();

            builder.Property(m => m.PeriodMonth)
                   .IsRequired();

            // Index pour optimiser les requêtes PowerBI (qui filtreront souvent par année/mois)
            builder.HasIndex(m => new { m.PeriodYear, m.PeriodMonth })
                   .HasDatabaseName("IX_MetricValue_Period");

            // Index pour la recherche par Shipper
            builder.HasIndex(m => m.ShipperShortCode)
                   .HasDatabaseName("IX_MetricValue_Shipper");
        }
    }
}