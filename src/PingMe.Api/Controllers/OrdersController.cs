namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Integrations;
using PingMe.Application.Ordering;
using PingMe.Domain.Ordering;
using PingMe.Infrastructure.Persistence;
using PingMe.Infrastructure.Tenants;

[ApiController]
[Route("orders")]
[AllowAnonymous]
public class OrdersController : ControllerBase
{
    /// <summary>Shown to staff when a location row cannot be read; never blank.</summary>
    public const string UnknownLocationLabel = "Unknown location";

    private readonly PingMeDbContext _dbContext;
    private readonly CurrentTenantProvider _currentTenantProvider;
    private readonly IOrderNotifier _orderNotifier;
    private readonly IPosOrderDispatcher _posOrderDispatcher;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        PingMeDbContext dbContext,
        CurrentTenantProvider currentTenantProvider,
        IOrderNotifier orderNotifier,
        IPosOrderDispatcher posOrderDispatcher,
        ILogger<OrdersController> logger)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
        _orderNotifier = orderNotifier;
        _posOrderDispatcher = posOrderDispatcher;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<CreateOrderResponse>> Create(CreateOrderRequest request)
    {
        var session = await _dbContext.CustomerSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId);
        if (session is null)
        {
            return NotFound("Session not found.");
        }

        var now = DateTime.UtcNow;
        if (session.ClosedAt is not null)
        {
            return Conflict("This session has been closed.");
        }

        if (session.ExpiresAt <= now)
        {
            return StatusCode(StatusCodes.Status410Gone, "This session has expired.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("An order must contain at least one item.");
        }

        if (request.Items.Any(i => i.Quantity < 1))
        {
            return BadRequest("Quantity must be at least 1 for every item.");
        }

        _currentTenantProvider.TenantId = session.TenantId;

        var order = new Order(session.TenantId, session.Id, now);
        foreach (var item in request.Items)
        {
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product is null)
            {
                return NotFound($"Product {item.ProductId} was not found.");
            }

            if (!product.IsAvailable)
            {
                return Conflict($"Product '{product.Name}' is not currently available.");
            }

            order.AddItem(product.Id, product.Name, product.Price, item.Quantity);
        }

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Staff cannot deliver an order they cannot locate, so the label is resolved once
        // here and used for both the realtime broadcast and the POS payload. The order is
        // already committed, so a lookup failure degrades the label instead of failing.
        string locationLabel;
        try
        {
            var sessionLocation = await _dbContext.Locations.FirstOrDefaultAsync(l => l.Id == session.LocationId);
            locationLabel = sessionLocation?.Name ?? UnknownLocationLabel;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve the location for order {OrderId}", order.Id);
            locationLabel = UnknownLocationLabel;
        }

        var orderDto = new AdminOrderDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList(),
            order.PosDeliveryStatus.ToString(),
            locationLabel);
        await _orderNotifier.NotifyOrderReceivedAsync(order.TenantId, orderDto);

        try
        {
            // Deliberately not HttpContext.RequestAborted: the order is already committed above,
            // so a customer disconnecting mid-request must not cancel POS delivery and record a
            // false Failed. The dispatcher's own HttpClient.Timeout (Program.cs) already bounds this.
            var posDeliveryStatus = await _posOrderDispatcher.TryDispatchAsync(order, locationLabel, CancellationToken.None);
            order.RecordPosDeliveryStatus(posDeliveryStatus);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // The order itself is already committed above — a failure here (location lookup,
            // or persisting the POS status) must never turn a successful order into an HTTP
            // error for the customer. Worst case, PosDeliveryStatus stays at its NotConfigured
            // default and staff can complete the POS entry manually.
            _logger.LogWarning(ex, "Failed to record POS delivery status for order {OrderId}", order.Id);
        }

        return Created(string.Empty, new CreateOrderResponse(order.Id, order.Status.ToString()));
    }

    [HttpGet("{id}/status")]
    public async Task<ActionResult<OrderStatusResponse>> GetStatus(Guid id, [FromQuery] Guid sessionId)
    {
        var session = await _dbContext.CustomerSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
        {
            return NotFound();
        }

        _currentTenantProvider.TenantId = session.TenantId;

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null || order.CustomerSessionId != session.Id)
        {
            return NotFound();
        }

        return Ok(new OrderStatusResponse(order.Id, order.Status.ToString()));
    }
}
