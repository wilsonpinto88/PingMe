namespace PingMe.UnitTests.Ordering;

using PingMe.Domain.Ordering;
using Xunit;

public class OrderTransitionTests
{
    [Fact]
    public void New_order_starts_as_Received()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(OrderStatus.Received, order.Status);
    }

    [Fact]
    public void Received_order_can_transition_to_Accepted()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        order.TransitionTo(OrderStatus.Accepted);

        Assert.Equal(OrderStatus.Accepted, order.Status);
    }

    [Fact]
    public void Received_order_cannot_skip_to_Preparing()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() => order.TransitionTo(OrderStatus.Preparing));

        Assert.Contains("Received", ex.Message);
        Assert.Contains("Preparing", ex.Message);
    }

    [Fact]
    public void Delivered_order_cannot_transition_further()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        order.TransitionTo(OrderStatus.Accepted);
        order.TransitionTo(OrderStatus.Preparing);
        order.TransitionTo(OrderStatus.Ready);
        order.TransitionTo(OrderStatus.Delivered);

        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(OrderStatus.Delivered));
    }
}
