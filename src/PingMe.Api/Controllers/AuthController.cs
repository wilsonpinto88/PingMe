namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Auth;
using PingMe.Domain.Locations;
using PingMe.Domain.Tenants;
using PingMe.Infrastructure.Identity;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public AuthController(PingMeDbContext dbContext, UserManager<AppUser> userManager, JwtTokenGenerator tokenGenerator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
    }

    [HttpPost("register-tenant")]
    public async Task<ActionResult<AuthResponse>> RegisterTenant(RegisterTenantRequest request)
    {
        var normalizedEmail = request.OwnerEmail.ToUpperInvariant();
        var emailTaken = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail);

        if (emailTaken)
        {
            return Conflict("An account with this email already exists.");
        }

        var tenant = new Tenant(request.TenantName, DateTime.UtcNow);
        _dbContext.Tenants.Add(tenant);

        var venue = new Venue(tenant.Id, request.TenantName);
        _dbContext.Venues.Add(venue);

        await _dbContext.SaveChangesAsync();

        var owner = new AppUser
        {
            TenantId = tenant.Id,
            UserName = request.OwnerEmail,
            Email = request.OwnerEmail,
        };

        var createResult = await _userManager.CreateAsync(owner, request.OwnerPassword);
        if (!createResult.Succeeded)
        {
            return BadRequest(createResult.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(owner, "Owner");

        var token = _tokenGenerator.GenerateToken(owner, new List<string> { "Owner" });
        return Created(string.Empty, new AuthResponse(token));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (user is null)
        {
            return Unauthorized();
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenGenerator.GenerateToken(user, roles);
        return Ok(new AuthResponse(token));
    }
}
