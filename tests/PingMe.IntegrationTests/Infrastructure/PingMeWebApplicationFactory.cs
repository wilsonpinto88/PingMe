namespace PingMe.IntegrationTests.Infrastructure;

using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PingMe.Infrastructure.Persistence;

public class PingMeWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PingMeDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<PingMeDbContext>(options =>
                options.UseNpgsql(
                    "Host=localhost;Port=5432;Database=pingme_test;Username=postgres;Password=postgres"));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PingMeDbContext>();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.Migrate();
        });
    }
}
