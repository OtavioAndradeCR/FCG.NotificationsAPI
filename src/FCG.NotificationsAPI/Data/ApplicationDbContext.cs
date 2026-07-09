using FCG.NotificationsAPI.Data.Configurations;
using FCG.NotificationsAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCG.NotificationsAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<NotificationUser> NotificationUsers => Set<NotificationUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new NotificationUserConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
