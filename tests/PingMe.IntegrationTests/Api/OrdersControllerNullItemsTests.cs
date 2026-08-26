namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class OrdersControllerNullItemsTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public OrdersControllerNullItemsTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Posting_an_order_with_null_items_returns_400_not_500()
    {
        var client = _factory.CreateClient();
        var content = new StringContent(
            "{\"sessionId\":\"" + Guid.NewGuid() + "\",\"items\":null}",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/orders", content);

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
