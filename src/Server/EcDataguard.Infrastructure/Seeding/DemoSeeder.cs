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

            var now = DateTime.UtcNow;
            var deviceOnline = Guid.NewGuid();
            var deviceDegraded = Guid.NewGuid();
            var deviceOffline = Guid.NewGuid();

            db.Devices.AddRange(
                new Device
                {
                    Id = deviceOnline,
                    TenantId = tenant.Id,
                    Hostname = "PC-SALES-01",
                    Os = OsType.Windows,
                    OsVersion = "11 Pro",
                    AgentVersion = "1.0.0",
                    ProtectionState = ProtectionState.Protected,
                    ProtectionDetails = "DLP activo (archivos, portapapeles, USB)",
                    LastHeartbeatUtc = now.AddMinutes(-2),
                    LastUser = "maria.paez",
                    IpAddress = "10.0.0.21"
                },
                new Device
                {
                    Id = deviceDegraded,
                    TenantId = tenant.Id,
                    Hostname = "PC-ACC-02",
                    Os = OsType.Windows,
                    OsVersion = "10 Pro",
                    AgentVersion = "1.0.0",
                    ProtectionState = ProtectionState.Degraded,
                    ProtectionDetails = "Sin monitoreo de USB",
                    LastHeartbeatUtc = now.AddMinutes(-6),
                    LastUser = "pedro.lopez",
                    IpAddress = "10.0.0.34"
                },
                new Device
                {
                    Id = deviceOffline,
                    TenantId = tenant.Id,
                    Hostname = "LT-DIR-07",
                    Os = OsType.Linux,
                    OsVersion = "Ubuntu 24.04",
                    AgentVersion = "1.0.0",
                    ProtectionState = ProtectionState.Unprotected,
                    ProtectionDetails = "Sin agente en línea",
                    LastHeartbeatUtc = now.AddDays(-2),
                    LastUser = "carlos.mendez",
                    IpAddress = "10.0.2.11"
                });

            db.EndpointUsers.Add(new EndpointUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserName = "maria.paez",
                FullName = "María Páez",
                TeamRef = "Ventas",
                Licensed = true,
                LastActivityUtc = now.AddMinutes(-15)
            });

            db.Events.AddRange(
                new EventRecord
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, DeviceId = deviceOnline,
                    ExternalId = "ev-demo-0001",
                    Kind = EventKind.FileOp, OccurredUtc = now.AddMinutes(-40), IngestedUtc = now.AddMinutes(-39),
                    UserName = "maria.paez", ProcessName = "WINWORD.EXE", Operation = "write",
                    FilePath = @"C:\Users\maria.paez\Documents\Contrato_2026.docx",
                    FileSizeBytes = 48213, Classifications = "file:document|documento",
                    Blocked = false
                },
                new EventRecord
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, DeviceId = deviceOnline,
                    ExternalId = "ev-demo-0002",
                    Kind = EventKind.App, OccurredUtc = now.AddMinutes(-28), IngestedUtc = now.AddMinutes(-27),
                    UserName = "maria.paez", ProcessName = "chrome.exe", Operation = "clipboard_copy",
                    DestinationType = "clipboard", FileSizeBytes = 128,
                    Classifications = "PII|Financiero", Blocked = true,
                    AppliedAction = PolicyAction.Block, AppliedPolicyId = Guid.NewGuid(),
                    Detail = "Texto copiado al portapapeles con número de tarjeta."
                },
                new EventRecord
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, DeviceId = deviceOnline,
                    ExternalId = "ev-demo-0003",
                    Kind = EventKind.Usb, OccurredUtc = now.AddMinutes(-20), IngestedUtc = now.AddMinutes(-19),
                    UserName = "jorge.ruiz", ProcessName = "ecdataguard-agent", Operation = "usb_attach",
                    DestinationType = "usb", DestinationDetail = "SanDisk Ultra 32 GB (E:\\)",
                    Classifications = "dest:usb|file:*", Blocked = false
                },
                new EventRecord
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, DeviceId = deviceDegraded,
                    ExternalId = "ev-demo-0004",
                    Kind = EventKind.FileOp, OccurredUtc = now.AddMinutes(-12), IngestedUtc = now.AddMinutes(-11),
                    UserName = "pedro.lopez", ProcessName = "outlook.exe", Operation = "read",
                    FilePath = @"C:\Users\pedro.lopez\Desktop\reporte_ventas.xlsx",
                    FileSizeBytes = 204800, Classifications = "Financiero|file:spreadsheet",
                    Blocked = false
                },
                new EventRecord
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, DeviceId = deviceOnline,
                    ExternalId = "ev-demo-0005",
                    Kind = EventKind.App, OccurredUtc = now.AddMinutes(-6), IngestedUtc = now.AddMinutes(-5),
                    UserName = "maria.paez", ProcessName = "notepad.exe", Operation = "file_write",
                    FilePath = @"C:\Users\maria.paez\Desktop\notas_cedula.txt",
                    FileSizeBytes = 512, Classifications = "PII",
                    Blocked = true, AppliedAction = PolicyAction.Block, AppliedPolicyId = Guid.NewGuid(),
                    Detail = "Número de cédula detectado en archivo de texto."
                });

            db.Insights.AddRange(
                new Insight
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id,
                    Reason = "Posible exfiltración: tarjeta de crédito enviada al portapapeles",
                    Severity = InsightSeverity.High, Status = InsightStatus.Open,
                    RelatedEventCount = 2, CreatedUtc = now.AddMinutes(-27), LastActivityUtc = now.AddMinutes(-26)
                },
                new Insight
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id,
                    Reason = "Documento con números de cédula en escritorio",
                    Severity = InsightSeverity.Medium, Status = InsightStatus.Closed,
                    RelatedEventCount = 1, CreatedUtc = now.AddMinutes(-40), LastActivityUtc = now.AddMinutes(-35),
                    ClosureReason = "Revisado por el responsable de datos",
                    ClosedUtc = now.AddMinutes(-35)
                });

            db.ScheduledReports.Add(new ScheduledReport
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id,
                Title = "Resumen diario de eventos",
                ReportType = "eventos", HourOfDayUtc = 6,
                RecipientsCsv = "security@empresa.com;admin@empresa.com",
                Enabled = true,
                NextRunUtc = now.Date.AddDays(1).AddHours(6)
            });

            await db.SaveChangesAsync();
            logger.LogInformation("Seed completado: tenant {Tenant}, admin {Admin}", tenant.Nombre, adminEmail);
        }
        else
        {
            logger.LogInformation("Seed omitido: ya existen datos.");
        }
    }
}