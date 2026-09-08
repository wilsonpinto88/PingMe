namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Ordering;
using PingMe.Domain.Ordering;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/orders")]
[Authorize(Roles = "Owner,Staff")]
public class AdminOrdersController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly IOrderNotifier _orderNotifier;

    public AdminOrdersController(PingMeDbContext dbContext, IOrderNotifier orderNotifier)
    {
        _dbContext = dbContext;
        _orderNotifier = orderNotifier;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminOrderDto>>> GetOrders()
    {
        var orders = await _dbContext.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        // Resolve every order's location in two queries rather than one per order.
        var locationLabelsBySessionId = await _dbContext.CustomerSessions
            .Join(
                _dbContext.Locations,
                session => session.LocationId,
                location => location.Id,
                (session, location) => new { session.Id, location.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        var result = orders
            .Select(o => new AdminOrderDto(
                o.Id,
                o.Status.ToString(),
                o.CreatedAt,
                o.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList(),
                o.PosDeliveryStatus.ToString(),
                locationLabelsBySessionId.TryGetValue(o.CustomerSessionId, out var label)
                    ? label
                    : OrdersController.UnknownLocationLabel))
            .ToList();

        return Ok(result);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request)
    {
        // Enum.TryParse also accepts the numeric form ("1" parses as Accepted), so match on
        // the declared names only. A numeric status from a client is always a mistake.
        var statusName = Enum.GetNames<OrderStatus>()
            .FirstOrDefault(name => string.Equals(name, request.Status, StringComparison.Ordinal));
        if (statusName is null)
        {
            return BadRequest($"'{request.Status}' is not a valid order status.");
        }

        var targetStatus = Enum.Parse<OrderStatus>(statusName);

        var order = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        try
        {
            order.TransitionTo(targetStatus);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        await _dbContext.SaveChangesAsync();

        var locationLabel = await _dbContext.CustomerSessions
            .Where(s => s.Id == order.CustomerSessionId)
            .Join(_dbContext.Locations, s => s.LocationId, l => l.Id, (s, l) => l.Name)
            .FirstOrDefaultAsync() ?? OrdersController.UnknownLocationLabel;

        var orderDto = new AdminOrderDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList(),
            order.PosDeliveryStatus.ToString(),
            locationLabel);
        await _orderNotifier.NotifyOrderStatusChangedAsync(order.TenantId, orderDto);

        return NoContent();
    }
}
