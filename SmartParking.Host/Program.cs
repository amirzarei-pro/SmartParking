using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Services;
using SmartParking.Host.Components;
using SmartParking.Host.Services;
using SmartParking.Infrastructure;
using SmartParking.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();
builder.Services.AddControllers();


builder.Services.AddDbContext<SmartParkingDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SmartParkingDb"));
});

builder.Services.AddScoped<ISlotService, SlotService>();
builder.Services.AddScoped<IIotService, IotService>();
builder.Services.AddScoped<ITelemetryLogService, TelemetryLogService>();


builder.Services.Configure<OfflineOptions>(builder.Configuration.GetSection("Offline"));
builder.Services.AddHostedService<OfflineMonitor>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapHub<SmartParking.Host.Hubs.ParkingHub>("/hubs/parking");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();



using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
    await SmartParking.Host.Seed.SeedRunner.EnsureSeedAsync(db, CancellationToken.None);
}

app.Run();
