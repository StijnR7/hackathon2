using Microsoft.EntityFrameworkCore;

namespace HitchTracker.Web.Data;

public static class DatabaseInitializer
{
    /// <summary>Voert openstaande migraties uit en vult een lege database met dummydata.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HitchTrackerDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }
}
