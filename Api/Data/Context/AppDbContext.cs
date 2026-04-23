using AnimeTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Data.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<WatchHistory> UserWatched { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // UserWatched
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<WatchHistory>().ToTable("userwatched");

            modelBuilder.Entity<WatchHistory>()
                .HasOne(uw => uw.User)
                .WithMany(u => u.Watched)
                .HasForeignKey(uw => uw.UserId);

        }
    }
}