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

            // The test database's physical existence predates newer EF model changes (e.g. Identity
            // tables), and EnsureCreated() is a no-op once the database already exists. Applying
            // pending migrations keeps the schema in sync with the current model on every test run.
            ReconcileMigrationHistoryWithExistingSchema(dbContext);
            dbContext.Database.Migrate();
        });
    }

    // The pingme_test database was originally provisioned via EnsureCreated() before the
    // __EFMigrationsHistory table existed, so tables from migrations that predate this
    // reconciliation are already present on disk but are not recorded as applied. Without this,
    // Database.Migrate() would try to re-run every migration from scratch and fail with
    // "relation already exists". This stamps history rows for migrations whose tables already
    // exist so Migrate() only applies the genuinely pending ones.
    private static void ReconcileMigrationHistoryWithExistingSchema(PingMeDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """);

        dbContext.Database.ExecuteSqlRaw(
            """
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Categories')
                   AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260825163453_AddTenantIndexAndRenameOrderItemsTable') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
                        ('20260825161039_InitialCreate', '10.0.11'),
                        ('20260825161404_AddOrderItemUnitPricePrecision', '10.0.11'),
                        ('20260825162048_AddTenantQueryFilters', '10.0.11'),
                        ('20260825163453_AddTenantIndexAndRenameOrderItemsTable', '10.0.11')
                    ON CONFLICT ("MigrationId") DO NOTHING;
                END IF;
            END $$;
            """);
    }
}
