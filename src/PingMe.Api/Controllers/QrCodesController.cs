namespace PingMe.Api.Controllers;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Locations;
using PingMe.Application.Tenants;
using PingMe.Domain.Locations;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/qrcodes")]
[Authorize(Roles = "Owner")]
public class QrCodesController : ControllerBase
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public QrCodesController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<List<QrCodeDto>>> GetQrCodes()
    {
        var qrCodes = await _dbContext.QrCodes
            .Select(q => new QrCodeDto(q.Id, q.LocationId, q.Code))
            .ToListAsync();
        return Ok(qrCodes);
    }

    [HttpPost]
    public async Task<ActionResult<QrCodeDto>> CreateQrCode(CreateQrCodeRequest request)
    {
        var locationExists = await _dbContext.Locations.AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
        {
            return NotFound();
        }

        const int maxInsertAttempts = 5;
        for (var attempt = 0; attempt < maxInsertAttempts; attempt++)
        {
            var code = await GenerateUniqueCodeAsync();
            var qrCode = new QrCode(_currentTenantProvider.TenantId!.Value, request.LocationId, code);
            _dbContext.QrCodes.Add(qrCode);
            try
            {
                await _dbContext.SaveChangesAsync();
                return Created(string.Empty, new QrCodeDto(qrCode.Id, qrCode.LocationId, qrCode.Code));
            }
            catch (DbUpdateException)
            {
                // The Code unique index rejected a race with a concurrent insert of the same
                // candidate — detach and retry with a freshly generated code.
                _dbContext.Entry(qrCode).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException($"Failed to insert a unique QR code after {maxInsertAttempts} attempts.");
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = GenerateCode();
            var exists = await _dbContext.QrCodes.IgnoreQueryFilters().AnyAsync(q => q.Code == candidate);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Failed to generate a unique QR code after {maxAttempts} attempts.");
    }

    private static string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var builder = new StringBuilder(8);
        foreach (var b in bytes)
        {
            builder.Append(CodeAlphabet[b % CodeAlphabet.Length]);
        }

        return builder.ToString();
    }
}
