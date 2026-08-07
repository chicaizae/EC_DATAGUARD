using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using EcDataguard.Infrastructure;
using EcDataguard.Infrastructure.Security;
using EcDataguard.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EC DATAGUARD API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Name = "Authorization"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthClaims.AgentPolicy, p =>
    {
        p.RequireAuthenticatedUser();
        p.RequireClaim(AuthClaims.Scope, AuthClaims.AgentScope);
    });
    options.AddPolicy(AuthClaims.ConsolePolicy, p =>
    {
        p.RequireAuthenticatedUser();
        p.RequireClaim(AuthClaims.Scope, AuthClaims.ConsoleScope);
    });
    options.AddPolicy(AuthClaims.SuperAdminPolicy, p =>
    {
        p.RequireAuthenticatedUser();
        p.RequireClaim(AuthClaims.Role, AuthClaims.SuperAdminRole);
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHealthChecks();

var app = builder.Build();

await InitializeDatabaseAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Binarios de agente publicados con deploy/agent/*.sh|ps1 (montado en /app/agents).
var agentsDir = Path.Combine(app.Environment.ContentRootPath, "agents");
if (Directory.Exists(agentsDir))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(agentsDir),
        RequestPath = "/agents"
    });
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EcDataguard.Infrastructure.EfCore.AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    var adminEmail = app.Configuration["Admin:Email"] ?? "admin@ecodataguard.local";
    var adminPassword = app.Configuration["Admin:InitialPassword"] ?? "Admin*EcDataguard2026";
    var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
    var hash = hasher.HashPassword(new object(), adminPassword);

    await EcDataguard.Infrastructure.Seeding.DemoSeeder.SeedAsync(app.Services, adminEmail, hash);
    Console.WriteLine("[EcDatag] Base de datos lista.");
}

public partial class Program { }