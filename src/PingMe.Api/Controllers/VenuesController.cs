namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Venues;
using PingMe.Domain.Locations;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/venues")]
[Authorize(Roles = "Owner")]
public class VenuesController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;

    public VenuesController(PingMeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<VenueDto>>> GetVenues()
    {
        var venues = await _dbContext.Venues.ToListAsync();
        return Ok(venues.Select(v => new VenueDto(v.Id, v.Name, v.ToThemeDto())).ToList());
    }

    [HttpPut("{id}/branding")]
    public async Task<ActionResult<VenueDto>> UpdateBranding(Guid id, UpdateVenueBrandingRequest request)
    {
        var venue = await _dbContext.Venues.FirstOrDefaultAsync(v => v.Id == id);
        if (venue is null)
        {
            return NotFound();
        }

        // Enum.TryParse also accepts the numeric form ("1" parses as Dark), so match on the
        // declared names only. A numeric theme mode from a client is always a mistake.
        var themeModeName = Enum.GetNames<VenueThemeMode>()
            .FirstOrDefault(name => string.Equals(name, request.ThemeMode, StringComparison.OrdinalIgnoreCase));
        if (themeModeName is null)
        {
            return BadRequest("ThemeMode must be either 'Light' or 'Dark'.");
        }

        var themeMode = Enum.Parse<VenueThemeMode>(themeModeName);

        try
        {
            venue.UpdateBranding(
                request.PrimaryColor,
                request.AccentColor,
                request.CurrencyCode,
                themeMode,
                request.LogoUrl,
                request.HeroImageUrl,
                request.Tagline);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new VenueDto(venue.Id, venue.Name, venue.ToThemeDto()));
    }
}
