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

        // Check if default user already exists
        if (db.Users.Any(u => u.Username == "rodshaw"))
            return;

        // Create default user
        var demoUsers = new[]
        {
            new { Username = "rodshaw", Email = "rodshaw@example.com", FullName = "Rod Shaw", Password = "Shawrod1", Role = UserRole.Admin }
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
