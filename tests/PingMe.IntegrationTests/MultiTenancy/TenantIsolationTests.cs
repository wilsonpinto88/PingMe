namespace PingMe.IntegrationTests.MultiTenancy;

using Microsoft.EntityFrameworkCore;
using PingMe.Domain.Catalog;
using PingMe.Infrastructure.Persistence;
using PingMe.Infrastructure.Tenants;
using Xunit;

public class TenantIsolationTests
{
    private static PingMeDbContext CreateContext(Guid? currentTenantId)
    {
        var options = new DbContextOptionsBuilder<PingMeDbContext>()
            .UseNpgsql(IntegrationTestDbConnection.ConnectionString)
            .Options;
        return new PingMeDbContext(options, new CurrentTenantProvider { TenantId = currentTenantId });
    }

    [Fact]
    public async Task TenantA_cannot_read_TenantB_product()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        await using (var seedContext = CreateContext(currentTenantId: null))
        {
            await seedContext.Database.EnsureCreatedAsync();

            var menuB = new Menu(tenantBId, "Menu B");
            seedContext.Menus.Add(menuB);
            await seedContext.SaveChangesAsync();

            var categoryB = new Category(tenantBId, menuB.Id, "Category B", 1);
            seedContext.Categories.Add(categoryB);
            await seedContext.SaveChangesAsync();

            var productB = new Product(tenantBId, categoryB.Id, "Product B", 9.99m);
            seedContext.Products.Add(productB);
            await seedContext.SaveChangesAsync();
        }

        await using var tenantAContext = CreateContext(currentTenantId: tenantAId);
        var visibleProducts = await tenantAContext.Products.ToListAsync();

        Assert.Empty(visibleProducts);
    }

    [Fact]
    public async Task TenantA_cannot_read_TenantB_location()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        await using (var seedContext = CreateContext(currentTenantId: null))
        {
            await seedContext.Database.EnsureCreatedAsync();

            var venueB = new PingMe.Domain.Locations.Venue(tenantBId, "Venue B");
            seedContext.Venues.Add(venueB);
            await seedContext.SaveChangesAsync();

            var locationB = new PingMe.Domain.Locations.Location(tenantBId, venueB.Id, "Table 1");
            seedContext.Locations.Add(locationB);
            await seedContext.SaveChangesAsync();
        }

        await using var tenantAContext = CreateContext(currentTenantId: tenantAId);
        var visibleLocations = await tenantAContext.Locations.ToListAsync();

        Assert.Empty(visibleLocations);
    }

    [Fact]
    public async Task TenantA_cannot_use_TenantB_customer_session()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        Guid sessionBId;

        await using (var seedContext = CreateContext(currentTenantId: null))
        {
            await seedContext.Database.EnsureCreatedAsync();

            var venueB = new PingMe.Domain.Locations.Venue(tenantBId, "Venue B");
            seedContext.Venues.Add(venueB);
            await seedContext.SaveChangesAsync();

            var locationB = new PingMe.Domain.Locations.Location(tenantBId, venueB.Id, "Table 1");
            seedContext.Locations.Add(locationB);
            await seedContext.SaveChangesAsync();

            var sessionB = new PingMe.Domain.Locations.CustomerSession(
                tenantBId, locationB.Id, DateTime.UtcNow, DateTime.UtcNow.AddHours(2));
            seedContext.CustomerSessions.Add(sessionB);
            await seedContext.SaveChangesAsync();
            sessionBId = sessionB.Id;
        }

        await using var tenantAContext = CreateContext(currentTenantId: tenantAId);
        var found = await tenantAContext.CustomerSessions.FirstOrDefaultAsync(s => s.Id == sessionBId);

        Assert.Null(found);
    }

    [Fact]
    public async Task TenantA_cannot_manipulate_TenantB_order()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        Guid orderBId;

        await using (var seedContext = CreateContext(currentTenantId: null))
        {
            await seedContext.Database.EnsureCreatedAsync();

            var venueB = new PingMe.Domain.Locations.Venue(tenantBId, "Venue B");
            seedContext.Venues.Add(venueB);
            await seedContext.SaveChangesAsync();

            var locationB = new PingMe.Domain.Locations.Location(tenantBId, venueB.Id, "Table 1");
            seedContext.Locations.Add(locationB);
            await seedContext.SaveChangesAsync();

            var sessionB = new PingMe.Domain.Locations.CustomerSession(
                tenantBId, locationB.Id, DateTime.UtcNow, DateTime.UtcNow.AddHours(2));
            seedContext.CustomerSessions.Add(sessionB);
            await seedContext.SaveChangesAsync();

            var orderB = new PingMe.Domain.Ordering.Order(tenantBId, sessionB.Id, DateTime.UtcNow);
            seedContext.Orders.Add(orderB);
            await seedContext.SaveChangesAsync();
            orderBId = orderB.Id;
        }

        await using var tenantAContext = CreateContext(currentTenantId: tenantAId);
        var orderVisibleToTenantA = await tenantAContext.Orders.FirstOrDefaultAsync(o => o.Id == orderBId);

        Assert.Null(orderVisibleToTenantA);
    }
}
