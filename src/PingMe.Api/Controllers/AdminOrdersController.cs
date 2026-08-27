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

    public AdminOrdersController(PingMeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminOrderDto>>> GetOrders()
    {
        var orders = await _dbContext.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders
            .Select(o => new AdminOrderDto(
                o.Id,
                o.Status.ToString(),
                o.CreatedAt,
                o.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList()))
            .ToList();

        return Ok(result);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, out var targetStatus))
        {
            return BadRequest($"'{request.Status}' is not a valid order status.");
        }

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
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
        return NoContent();
    }
}
