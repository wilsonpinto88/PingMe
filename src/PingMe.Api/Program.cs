using Microsoft.EntityFrameworkCore;
using PingMe.Application.Tenants;
using PingMe.Infrastructure.Persistence;
using PingMe.Infrastructure.Tenants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<PingMeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PingMe")));

builder.Services.AddScoped<CurrentTenantProvider>();
builder.Services.AddScoped<ICurrentTenantProvider>(sp => sp.GetRequiredService<CurrentTenantProvider>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
