namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Catalog;
using PingMe.Application.Tenants;
using PingMe.Domain.Catalog;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/menus")]
[Authorize(Roles = "Owner")]
public class MenusController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public MenusController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<List<MenuDto>>> GetMenus()
    {
        var menus = await _dbContext.Menus
            .Select(m => new MenuDto(m.Id, m.Name))
            .ToListAsync();
        return Ok(menus);
    }

    [HttpPost]
    public async Task<ActionResult<MenuDto>> CreateMenu(CreateMenuRequest request)
    {
        var menu = new Menu(_currentTenantProvider.TenantId!.Value, request.Name);
        _dbContext.Menus.Add(menu);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new MenuDto(menu.Id, menu.Name));
    }

    [HttpPost("{menuId}/categories")]
    public async Task<ActionResult<CategoryDto>> CreateCategory(Guid menuId, CreateCategoryRequest request)
    {
        var menuExists = await _dbContext.Menus.AnyAsync(m => m.Id == menuId);
        if (!menuExists)
        {
            return NotFound();
        }

        var category = new Category(_currentTenantProvider.TenantId!.Value, menuId, request.Name, request.SortOrder);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new CategoryDto(category.Id, category.MenuId, category.Name, category.SortOrder));
    }
}
