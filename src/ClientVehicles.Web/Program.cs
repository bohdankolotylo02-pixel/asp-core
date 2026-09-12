using ClientVehicles.Web.Data;
using ClientVehicles.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
                      ?? "Data Source=clientvehicles.db"));

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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
