using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using EcDataguard.Web.Components;
using EcDataguard.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Backplane de SignalR con Redis: habilita replicar la consola sin estado.
var redis = builder.Configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(redis))
{
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(redis, options => options.Configuration.AbortOnConnectFail = false);
}

builder.Services.AddScoped<ConsoleSession>();
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Api:BaseUrl"] ?? "http://localhost:8080";
    return new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
});
builder.Services.AddScoped<ConsoleApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();