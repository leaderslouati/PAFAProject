using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PAFA.Domain.Entities;
using System.Data.Entity.ModelConfiguration;

namespace PAFA.Infrastructure.EntityConfigurations
{
    public class IngestedFileConfiguration : IEntityTypeConfiguration<IngestedFile>
    {
        public void Configure(EntityTypeBuilder<IngestedFile> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);
            builder.Property(x => x.FileType).IsRequired().HasMaxLength(10);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        }
    }
}