using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PriceRadar.Data.Context;

public class PriceRadarDbContextFactory
    : IDesignTimeDbContextFactory<PriceRadarDbContext>
{
    public PriceRadarDbContext CreateDbContext(string[] args)
    {
        string connectionString = GetConnectionString();

        DbContextOptions<PriceRadarDbContext> options =
            new DbContextOptionsBuilder<PriceRadarDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new PriceRadarDbContext(options);
    }

    private static string GetConnectionString()
    {
        string? environmentConnectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection");

        if (!string.IsNullOrWhiteSpace(environmentConnectionString))
        {
            return environmentConnectionString;
        }

        string appSettingsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "appsettings.json");

        if (File.Exists(appSettingsPath))
        {
            using JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(appSettingsPath));

            if (document.RootElement.TryGetProperty(
                    "ConnectionStrings",
                    out JsonElement connectionStrings)
                && connectionStrings.TryGetProperty(
                    "DefaultConnection",
                    out JsonElement defaultConnection)
                && defaultConnection.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(
                    defaultConnection.GetString()))
            {
                return defaultConnection.GetString()!;
            }
        }

        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is required for EF design-time operations.");
    }
}
