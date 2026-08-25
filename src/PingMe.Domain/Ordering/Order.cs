namespace PingMe.Domain.Ordering;

using PingMe.Domain.Common;

public class Order : Entity, ITenantOwned
{
    private readonly List<OrderItem> _items = new();

    private static readonly Dictionary<OrderStatus, OrderStatus> NextStatus = new()
    {
        [OrderStatus.Received] = OrderStatus.Accepted,
        [OrderStatus.Accepted] = OrderStatus.Preparing,
        [OrderStatus.Preparing] = OrderStatus.Ready,
        [OrderStatus.Ready] = OrderStatus.Delivered,
    };

    public Guid TenantId { get; private set; }
    public Guid CustomerSessionId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public Order(Guid tenantId, Guid customerSessionId, DateTime createdAt)
    {
        TenantId = tenantId;
        CustomerSessionId = customerSessionId;
        CreatedAt = createdAt;
        Status = OrderStatus.Received;
    }

    public void AddItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        _items.Add(new OrderItem(Id, productId, productName, unitPrice, quantity));
    }

    public void TransitionTo(OrderStatus next)
    {
        if (!NextStatus.TryGetValue(Status, out var allowedNext) || allowedNext != next)
        {
            throw new InvalidOperationException($"Cannot transition order from {Status} to {next}.");
        }

        Status = next;
    }
}
