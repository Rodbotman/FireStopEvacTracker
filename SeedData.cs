using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;

namespace FireStopEvacTracker;

public class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        await db.Database.EnsureCreatedAsync();

        // Check if demo users already exist
        if (db.Users.Any(u => u.Username == "admin"))
            return;

        // Create demo users
        var demoUsers = new[]
        {
            new { Username = "admin", Email = "admin@example.com", FullName = "Administrator", Password = "admin123", Role = UserRole.Admin },
            new { Username = "manager", Email = "manager@example.com", FullName = "Manager User", Password = "manager123", Role = UserRole.Manager },
            new { Username = "viewer", Email = "viewer@example.com", FullName = "Viewer User", Password = "viewer123", Role = UserRole.Viewer }
        };

        foreach (var userData in demoUsers)
        {
            var user = new User
            {
                Username = userData.Username,
                Email = userData.Email,
                FullName = userData.FullName,
                PasswordHash = authService.HashPassword(userData.Password),
                Role = userData.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
    }
}
