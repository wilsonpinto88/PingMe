namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Locations;
using PingMe.Application.Tenants;
using PingMe.Domain.Locations;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/locations")]
[Authorize(Roles = "Owner")]
public class LocationsController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public LocationsController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<List<LocationDto>>> GetLocations()
    {
        var locations = await _dbContext.Locations
            .Select(l => new LocationDto(l.Id, l.Name, l.ParentLocationId))
            .ToListAsync();
        return Ok(locations);
    }

    [HttpPost]
    public async Task<ActionResult<LocationDto>> CreateLocation(CreateLocationRequest request)
    {
        if (request.ParentLocationId is not null)
        {
            var parentExists = await _dbContext.Locations.AnyAsync(l => l.Id == request.ParentLocationId);
            if (!parentExists)
            {
                return NotFound("The specified parent location was not found.");
            }
        }

        var venue = await _dbContext.Venues.FirstAsync();
        var location = new Location(_currentTenantProvider.TenantId!.Value, venue.Id, request.Name, request.ParentLocationId);
        _dbContext.Locations.Add(location);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new LocationDto(location.Id, location.Name, location.ParentLocationId));
    }

    [HttpPost("{id}/close-session")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> CloseSession(Guid id)
    {
        var session = await _dbContext.CustomerSessions
            .FirstOrDefaultAsync(s => s.LocationId == id && s.ClosedAt == null);
        if (session is null)
        {
            return NotFound();
        }

        session.Close(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }
}
