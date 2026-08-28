namespace PingMe.UnitTests.Integrations;

using Microsoft.Extensions.Logging.Abstractions;
using PingMe.Application.Integrations;
using PingMe.Domain.Integrations;
using PingMe.Domain.Ordering;
using PingMe.Infrastructure.Integrations;
using Xunit;

public class PosOrderDispatcherTests
{
    private class FakeResolver : IPosIntegrationResolver
    {
        private readonly IPosIntegration? _integration;

        public FakeResolver(IPosIntegration? integration)
        {
            _integration = integration;
        }

        public Task<IPosIntegration?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(_integration);
    }

    private class FakeIntegration : IPosIntegration
    {
        private readonly Exception? _exceptionToThrow;

        public FakeIntegration(Exception? exceptionToThrow = null)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public Task SendOrderAsync(Order order, string locationLabel, CancellationToken cancellationToken)
        {
            if (_exceptionToThrow is not null)
            {
                throw _exceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Returns_NotConfigured_when_no_integration_is_resolved()
    {
        var dispatcher = new PosOrderDispatcher(new FakeResolver(null), NullLogger<PosOrderDispatcher>.Instance);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var status = await dispatcher.TryDispatchAsync(order, "Table 1", CancellationToken.None);

        Assert.Equal(PosDeliveryStatus.NotConfigured, status);
    }

    [Fact]
    public async Task Returns_Failed_when_the_integration_throws()
    {
        var dispatcher = new PosOrderDispatcher(
            new FakeResolver(new FakeIntegration(new InvalidOperationException("boom"))),
            NullLogger<PosOrderDispatcher>.Instance);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var status = await dispatcher.TryDispatchAsync(order, "Table 1", CancellationToken.None);

        Assert.Equal(PosDeliveryStatus.Failed, status);
    }

    [Fact]
    public async Task Returns_Sent_when_the_integration_succeeds()
    {
        var dispatcher = new PosOrderDispatcher(
            new FakeResolver(new FakeIntegration()),
            NullLogger<PosOrderDispatcher>.Instance);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var status = await dispatcher.TryDispatchAsync(order, "Table 1", CancellationToken.None);

        Assert.Equal(PosDeliveryStatus.Sent, status);
    }
}
