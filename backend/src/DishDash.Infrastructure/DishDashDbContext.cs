using DishDash.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace DishDash.Infrastructure;

public sealed class AppUser : IdentityUser;
public sealed class DishDashDbContext(DbContextOptions<DishDashDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<UserPreference> Preferences => Set<UserPreference>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Rating> Ratings => Set<Rating>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<UserPreference>().HasKey(x => x.UserId);
        b.Entity<Favorite>().HasKey(x => new { x.UserId, x.DishId });
        b.Entity<Favorite>().HasOne<Dish>().WithMany().HasForeignKey(x => x.DishId);
        b.Entity<CartItem>().HasOne<Dish>().WithMany().HasForeignKey(x => x.DishId);
        b.Entity<Order>().HasIndex(x => new { x.UserId, x.CheckoutKey }).IsUnique();
        b.Entity<Order>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Rating>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<Rating>().HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId);
        b.Entity<Notification>().HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId);
        b.Entity<UserPreference>().HasOne<AppUser>().WithOne().HasForeignKey<UserPreference>(x => x.UserId);
        b.Entity<Favorite>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<CartItem>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<Order>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<Rating>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<Notification>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId);
        foreach (var property in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal)))
        { property.SetPrecision(12); property.SetScale(2); }
    }
}
