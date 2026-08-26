namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Ordering;
using PingMe.Domain.Locations;
using PingMe.Infrastructure.Persistence;
using PingMe.Infrastructure.Tenants;

[ApiController]
[Route("p")]
[AllowAnonymous]
public class QrResolutionController : ControllerBase
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(4);

    private readonly PingMeDbContext _dbContext;
    private readonly CurrentTenantProvider _currentTenantProvider;

    public QrResolutionController(PingMeDbContext dbContext, CurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<ResolveQrCodeResponse>> Resolve(string code)
    {
        var qrCode = await _dbContext.QrCodes.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Code == code);
        if (qrCode is null)
        {
            return NotFound();
        }

        _currentTenantProvider.TenantId = qrCode.TenantId;

        var location = await _dbContext.Locations.FirstAsync(l => l.Id == qrCode.LocationId);
        var venue = await _dbContext.Venues.FirstAsync();

        var now = DateTime.UtcNow;
        var session = await _dbContext.CustomerSessions
            .FirstOrDefaultAsync(s => s.LocationId == location.Id && s.ClosedAt == null && s.ExpiresAt > now);
        if (session is null)
        {
            session = new CustomerSession(qrCode.TenantId, location.Id, now, now.Add(SessionDuration));
            _dbContext.CustomerSessions.Add(session);
            await _dbContext.SaveChangesAsync();
        }

        var menus = await _dbContext.Menus.ToListAsync();
        var categories = await _dbContext.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        var products = await _dbContext.Products.Where(p => p.IsAvailable).ToListAsync();

        var menuDtos = menus
            .Select(m => new CustomerMenuDto(
                m.Id,
                m.Name,
                categories
                    .Where(c => c.MenuId == m.Id)
                    .Select(c => new CustomerCategoryDto(
                        c.Id,
                        c.Name,
                        c.SortOrder,
                        products.Where(p => p.CategoryId == c.Id)
                            .Select(p => new CustomerProductDto(p.Id, p.Name, p.Price))
                            .ToList()))
                    .ToList()))
            .ToList();

        return Ok(new ResolveQrCodeResponse(session.Id, venue.Name, location.Name, menuDtos));
    }
}
