using ClientVehicles.Web.Data;
using ClientVehicles.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
                      ?? "Data Source=clientvehicles.db"));

// Only used when the app runs behind a reverse proxy (see PathBase below).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<ClientService>();

var app = builder.Build();

// Migrations are applied at startup so the reviewer only needs "dotnet run". In a real deployment
// this would be a deliberate release step instead (see HANDOVER.md).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

// Optional, unset by default: lets the app be hosted under a sub-path behind a reverse proxy
// (PathBase=/demo) so that generated links stay correct. No effect when the setting is absent.
var pathBase = builder.Configuration["PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

if (string.IsNullOrWhiteSpace(pathBase))
{
    // Behind a proxy TLS is terminated upstream, so the redirect is only useful when self hosted.
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
