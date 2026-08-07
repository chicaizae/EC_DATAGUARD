using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EcDataguard.Domain.Entities;
using EcDataguard.Domain.Enums;

namespace EcDataguard.Infrastructure.Seeding;

public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services, string adminEmail, string adminPasswordHash)
    {
        var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EfCore.AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seeding");

        // No se usan migraciones EF en esta fase: el esquema se crea con EnsureCreatedAsync desde Program.
        await db.Database.EnsureCreatedAsync();

        if (!await db.Tenants.AnyAsync())
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Codigo = "DEMO",
                Nombre = "Empresa Demo S.A.",
                Plan = TenantPlan.Enterprise,
                CreadoUtc = DateTime.UtcNow
            };
            db.Tenants.Add(tenant);

            var consoleUser = new ConsoleUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Email = adminEmail,
                DisplayName = "Administrador EC DATAGUARD",
                PasswordHash = adminPasswordHash,
                Role = Role.SuperAdmin,
                CreatedUtc = DateTime.UtcNow
            };
            db.ConsoleUsers.Add(consoleUser);

            db.Policies.Add(new Policy
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Bloquear USB con información sensible",
                Kind = PolicyKind.Data,
                Enabled = true,
                Priority = 1,
                Action = PolicyAction.Block,
                ConditionsJson = "{\"destinations\":[\"external_storage\"],\"classifications\":[\"PII\",\"Financiero\"]}",
                ScopeJson = "{}",
                InsightTrigger = "Always"
            });

            db.Policies.Add(new Policy
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Auditar copias a red",
                Kind = PolicyKind.Auditing,
                Enabled = true,
                Priority = 2,
                Action = PolicyAction.Log,
                ConditionsJson = "{\"destinations\":[\"network_path\"]}",
                ScopeJson = "{}",
                InsightTrigger = "Default"
            });

            db.Classifications.AddRange(
                new Classification
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "PII",
                    Rules = new List<ClassificationRule>
                    {
                        new() { Id = Guid.NewGuid(), Type = RuleType.Content, IsRegex = true, Pattern = @"\b\d{13}\b" },
                        new() { Id = Guid.NewGuid(), Type = RuleType.Content, IsRegex = false, Pattern = "cedula" }
                    }
                },
                new Classification
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Financiero",
                    Rules = new List<ClassificationRule>
                    {
                        new() { Id = Guid.NewGuid(), Type = RuleType.Content, IsRegex = true, Pattern = @"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14})\b" }
                    }
                });

            db.Destinations.AddRange(
                new Destination { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "USB corporativo", Type = "external_storage", Tier = DestinationTier.Safe },
                new Destination { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "ChatGPT", Type = "web_upload", Tier = DestinationTier.Untrusted },
                new Destination { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Correo personal", Type = "email", Tier = DestinationTier.Untrusted });

            await db.SaveChangesAsync();
            logger.LogInformation("Seed completado: tenant {Tenant}, admin {Admin}", tenant.Nombre, adminEmail);
        }
        else
        {
            logger.LogInformation("Seed omitido: ya existen datos.");
        }
    }
}