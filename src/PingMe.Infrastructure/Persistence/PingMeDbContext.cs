namespace PingMe.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PingMe.Application.Tenants;
using PingMe.Domain.Catalog;
using PingMe.Domain.Common;
using PingMe.Domain.Integrations;
using PingMe.Domain.Locations;
using PingMe.Domain.Ordering;
using PingMe.Domain.Tenants;
using PingMe.Infrastructure.Identity;

public class PingMeDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public PingMeDbContext(DbContextOptions<PingMeDbContext> options, ICurrentTenantProvider currentTenantProvider)
        : base(options)
    {
        _currentTenantProvider = currentTenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();
    public DbSet<QrCode> QrCodes => Set<QrCode>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<TenantPosIntegrationSettings> TenantPosIntegrationSettings => Set<TenantPosIntegrationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(10, 2);
        modelBuilder.Entity<ProductOption>().Property(p => p.PriceDelta).HasPrecision(10, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(10, 2);
        modelBuilder.Entity<OrderItem>().ToTable("OrderItems");

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        modelBuilder.Entity<Location>()
            .HasOne<Location>()
            .WithMany()
            .HasForeignKey(l => l.ParentLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<QrCode>()
            .HasIndex(q => q.Code)
            .IsUnique();

        modelBuilder.Entity<TenantPosIntegrationSettings>()
            .HasIndex(t => t.TenantId)
            .IsUnique();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(PingMeDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _currentTenantProvider.TenantId);
        modelBuilder.Entity<TEntity>().HasIndex(e => e.TenantId);
    }
}
