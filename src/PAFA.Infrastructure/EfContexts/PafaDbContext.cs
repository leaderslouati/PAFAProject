using Microsoft.EntityFrameworkCore;
using PAFA.Domain.Entities;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace PAFA.Infrastructure.EfContexts
{
    public class PafaDbContext : DbContext
    {
        public PafaDbContext(DbContextOptions<PafaDbContext> options) : base(options) { }

        public DbSet<IngestedFile> IngestedFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Applique les configurations (Fluent API)
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(PafaDbContext).Assembly);
        }
    }
}