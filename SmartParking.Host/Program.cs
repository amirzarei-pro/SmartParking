using Microsoft.AspNetCore.Authentication.Cookies;
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

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/api/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();


builder.Services.AddDbContext<SmartParkingDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SmartParkingDb"));
});

builder.Services.AddScoped<ISlotService, SlotService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IIotService, IotService>();
builder.Services.AddScoped<ITelemetryLogService, TelemetryLogService>();


builder.Services.Configure<OfflineOptions>(builder.Configuration.GetSection("Offline"));
builder.Services.AddHostedService<OfflineMonitor>();

builder.Services.AddScoped<AuthService>();


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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapHub<SmartParking.Host.Hubs.ParkingHub>("/hubs/parking");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();



try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        await SmartParking.Host.Seed.SeedRunner.EnsureSeedAsync(db, CancellationToken.None);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not seed database: {ex.Message}");
    Console.WriteLine("Application will continue without database. Some features may not work.");
}

app.Run();
