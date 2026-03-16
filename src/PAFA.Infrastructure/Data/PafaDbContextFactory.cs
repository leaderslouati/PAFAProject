using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PAFA.Infrastructure.Persistence;
using System.IO;
using System.Text.Json;

namespace PAFA.Infrastructure.EfContexts
{
    public class PafaDbContextFactory : IDesignTimeDbContextFactory<PafaDbContext>
    {
        public PafaDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PafaDbContext>();

            // 1) Prefer explicit environment variable
            var conn = Environment.GetEnvironmentVariable("PAFA_CONNECTION");

            // 2) Try to read API project appsettings.json (useful when running CLI from repo root)
            if (string.IsNullOrEmpty(conn))
            {
                try
                {
                    var apiSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "PAFA.Api", "appsettings.json");
                    if (File.Exists(apiSettingsPath))
                    {
                        var json = File.ReadAllText(apiSettingsPath);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                            cs.TryGetProperty("DefaultConnection", out var defaultConn))
                        {
                            conn = defaultConn.GetString();
                        }
                    }
                }
                catch
                {
                    // ignore and fall back to default
                }
            }

            // 3) Final default fallback
            if (string.IsNullOrEmpty(conn))
            {
                conn = "Host=localhost;Port=5432;Database=pafadb;Username=postgres;Password=postgres";
            }

            optionsBuilder.UseNpgsql(conn);

            return new PafaDbContext(optionsBuilder.Options);
        }
    }
}