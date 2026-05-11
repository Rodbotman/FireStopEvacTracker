using FireStopEvacTracker.Data;
using FireStopEvacTracker.Services;
using FireStopEvacTracker;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<JobNameService>();
builder.Services.AddScoped<PdfStorageService>();
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

// Apply any pending migrations (with error handling)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
        // Seed initial data
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        // Log migration/seeding errors but don't crash the app
        Console.WriteLine($"Migration/Seeding Error: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseSession();
app.UseRouting();
app.MapControllers();
app.MapRazorPages();
app.MapGet("/", context =>
{
    var userId = context.Session.GetInt32("UserId");
    if (userId == null)
    {
        context.Response.Redirect("/Login");
    }
    else
    {
        context.Response.Redirect("/Jobs");
    }
    return Task.CompletedTask;
});

// Initialize seed data
await SeedData.InitializeAsync(app.Services);

app.Run();
