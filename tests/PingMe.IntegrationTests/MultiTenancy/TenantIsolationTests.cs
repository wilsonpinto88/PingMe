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
}
