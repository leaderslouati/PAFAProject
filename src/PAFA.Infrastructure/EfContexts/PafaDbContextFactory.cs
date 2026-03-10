using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PAFA.Infrastructure.EfContexts
{
    public class PafaDbContextFactory : IDesignTimeDbContextFactory<PafaDbContext>
    {
        public PafaDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PafaDbContext>();

            optionsBuilder.UseNpgsql(
                "Server=localhost;Database=PafaDb;Trusted_Connection=True;TrustServerCertificate=True");

            return new PafaDbContext(optionsBuilder.Options);
        }
    }
}