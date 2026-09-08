using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PingMe.Api.Middleware;
using PingMe.Api.Realtime;
using PingMe.Application.Integrations;
using PingMe.Application.Ordering;
using PingMe.Application.Tenants;
using PingMe.Infrastructure.Identity;
using PingMe.Infrastructure.Integrations;
using PingMe.Infrastructure.Persistence;
using PingMe.Infrastructure.Tenants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by /auth/login (no \"Bearer \" prefix needed)."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

builder.Services.AddDbContext<PingMeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PingMe")));

builder.Services.AddScoped<CurrentTenantProvider>();
builder.Services.AddScoped<ICurrentTenantProvider>(sp => sp.GetRequiredService<CurrentTenantProvider>());
builder.Services.AddScoped<JwtTokenGenerator>();

builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequiredLength = 8;
    // Identity's built-in email-uniqueness check silently stops working once the
    // global tenant query filter is active (it looks up existing users scoped to
    // the *current* tenant, which is null during anonymous registration). Disabled
    // here; AuthController.RegisterTenant enforces global uniqueness manually.
    options.User.RequireUniqueEmail = false;
})
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<PingMeDbContext>();

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/orders"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddScoped<IOrderNotifier, SignalROrderNotifier>();
builder.Services.AddHttpClient(nameof(WebhookPosIntegration), client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<IPosIntegrationResolver, PosIntegrationResolver>();
builder.Services.AddScoped<IPosOrderDispatcher, PosOrderDispatcher>();

const string FrontendDevCorsPolicy = "FrontendDevCorsPolicy";

// Origins come from configuration so a device on the local network (a phone
// testing the customer app, a tablet on the pass) can be allowed without a
// code change. Set Cors:AllowedOrigins in appsettings.Development.json.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:5173", "http://localhost:5174" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendDevCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Applies any pending migration on startup rather than requiring a manual
    // `dotnet ef database update` step against the production database — the
    // free-tier deploy path (Render + Neon) has no separate step to run one.
    var dbContext = scope.ServiceProvider.GetRequiredService<PingMeDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in new[] { "Owner", "Staff" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Render (and most PaaS hosts) terminate TLS at their edge and forward plain
// http to the container, tagging the original scheme in X-Forwarded-Proto.
// Without trusting that header, UseHttpsRedirection below sees "http" on
// every request and redirects, producing a loop the edge proxy itself can't
// resolve. KnownProxies/KnownNetworks are cleared because the proxy sits
// outside the container on infrastructure we don't control the IP of; the
// header is only reachable through that edge, so trusting it here is safe.
if (!app.Environment.IsDevelopment())
{
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    // Cleared, not left at their loopback-only defaults: the proxy that sets
    // these headers is the host's edge, not a known local address.
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

// Skipped in Development: a phone on the local network reaches the API over
// plain http, and redirecting it to a host-only dev certificate it does not
// trust would break testing on a real device.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendDevCorsPolicy);
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");

// Unauthenticated on purpose: the host's health check has no JWT to send.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program
{
}
