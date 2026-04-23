using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AnimeTracker.Data.Context;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseNpgsql("Host=yamanote.proxy.rlwy.net;Port=34354;Database=railway;Username=postgres;Password=SznrgZBzfsXZtRQqcdnaoQEBEguAngQB;");

        return new AppDbContext(optionsBuilder.Options);
    }
}