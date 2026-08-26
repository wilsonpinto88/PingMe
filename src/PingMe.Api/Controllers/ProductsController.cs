namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Catalog;
using PingMe.Application.Tenants;
using PingMe.Domain.Catalog;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/products")]
[Authorize(Roles = "Owner")]
public class ProductsController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public ProductsController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        return Ok(new ProductDto(product.Id, product.CategoryId, product.Name, product.Price, product.IsAvailable));
    }

    [HttpPost("categories/{categoryId}")]
    public async Task<ActionResult<ProductDto>> Create(Guid categoryId, CreateProductRequest request)
    {
        var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == categoryId);
        if (!categoryExists)
        {
            return NotFound();
        }

        var product = new Product(_currentTenantProvider.TenantId!.Value, categoryId, request.Name, request.Price);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new ProductDto(product.Id, product.CategoryId, product.Name, product.Price, product.IsAvailable));
    }

    [HttpPut("{id}/availability")]
    public async Task<IActionResult> UpdateAvailability(Guid id, UpdateProductAvailabilityRequest request)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        product.SetAvailability(request.IsAvailable);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{productId}/options")]
    public async Task<ActionResult<ProductOptionDto>> AddOption(Guid productId, CreateProductOptionRequest request)
    {
        var productExists = await _dbContext.Products.AnyAsync(p => p.Id == productId);
        if (!productExists)
        {
            return NotFound();
        }

        var option = new ProductOption(productId, request.Name, request.PriceDelta);
        _dbContext.ProductOptions.Add(option);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new ProductOptionDto(option.Id, option.ProductId, option.Name, option.PriceDelta));
    }
}
