namespace PingMe.IntegrationTests.MultiTenancy;

internal static class IntegrationTestDbConnection
{
    public const string ConnectionString =
        "Host=localhost;Port=5432;Database=pingme_test;Username=postgres;Password=postgres";
}
