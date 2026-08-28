namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Integrations;
using PingMe.Application.Tenants;
using PingMe.Domain.Integrations;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/pos-integration")]
[Authorize(Roles = "Owner")]
public class PosIntegrationSettingsController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public PosIntegrationSettingsController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<PosIntegrationSettingsDto>> Get()
    {
        var settings = await _dbContext.TenantPosIntegrationSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            return NoContent();
        }

        return Ok(new PosIntegrationSettingsDto(settings.ProviderType.ToString(), settings.WebhookUrl, settings.IsEnabled));
    }

    [HttpPut]
    public async Task<ActionResult<PosIntegrationSettingsDto>> Upsert(UpsertPosIntegrationSettingsRequest request)
    {
        if (!Enum.TryParse<ProviderType>(request.ProviderType, out var providerType))
        {
            return BadRequest($"'{request.ProviderType}' is not a valid provider type.");
        }

        var settings = await _dbContext.TenantPosIntegrationSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new TenantPosIntegrationSettings(
                _currentTenantProvider.TenantId!.Value, providerType, request.WebhookUrl, request.IsEnabled);
            _dbContext.TenantPosIntegrationSettings.Add(settings);
        }
        else
        {
            settings.UpdateSettings(providerType, request.WebhookUrl, request.IsEnabled);
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new PosIntegrationSettingsDto(settings.ProviderType.ToString(), settings.WebhookUrl, settings.IsEnabled));
    }
}
