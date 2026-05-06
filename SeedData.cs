using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;

namespace FireStopEvacTracker;

public class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        Console.WriteLine("[SEED] Starting seed initialization...");
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        await db.Database.EnsureCreatedAsync();
        Console.WriteLine("[SEED] Database ensured created");

        // Create default users if they don't exist
        var demoUsers = new[]
        {
            new { Username = "rod@surepro.com.au", Email = "rod@surepro.com.au", FullName = "Rod Shaw", Password = "Shawrod1", Role = UserRole.Admin },
            new { Username = "rodshaw", Email = "rodshaw@example.com", FullName = "Rod Shaw", Password = "Shawrod1", Role = UserRole.Admin },
            new { Username = "admin", Email = "admin@example.com", FullName = "Administrator", Password = "admin123", Role = UserRole.Admin }
        };

        foreach (var userData in demoUsers)
        {
            // Only add if the user doesn't exist
            if (db.Users.Any(u => u.Username == userData.Username))
                continue;

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
        Console.WriteLine("[SEED] Users seeded");

        // Create demo jobs if they don't exist
        Console.WriteLine($"[SEED] Checking if jobs exist. Any jobs? {db.EvacJobs.Any()}");
        if (!db.EvacJobs.Any())
        {
            Console.WriteLine("[SEED] Creating demo jobs...");
            var demoJobs = new[]
            {
                new EvacJob
                {
                    JobName = "T-36101",
                    ClientName = "Life Medical",
                    SiteAddress = "113 Stuart St, Mullumbimby NSW 2482",
                    Status = "Complete",
                    DateStarted = DateTime.Now.AddDays(-7),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    StatusUpdatedAt = DateTime.Now.AddDays(-1),
                    Notes = "Completed successfully. Client approved."
                },
                new EvacJob
                {
                    JobName = "T-36102",
                    ClientName = "Sydney Medical Centre",
                    SiteAddress = "245 Elizabeth St, Sydney NSW 2000",
                    Status = "Drafting",
                    DateStarted = DateTime.Now.AddDays(-3),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    StatusUpdatedAt = DateTime.Now.AddDays(-2),
                    Notes = "Currently in draft phase. Awaiting client review."
                },
                new EvacJob
                {
                    JobName = "T-36103",
                    ClientName = "Coastal Wellness",
                    SiteAddress = "42-44 Murwillumbah St, Murwillumbah NSW 2484",
                    Status = "New",
                    DateStarted = DateTime.Now,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    StatusUpdatedAt = DateTime.UtcNow,
                    Notes = "New job - awaiting initial survey."
                }
            };

            foreach (var job in demoJobs)
            {
                db.EvacJobs.Add(job);
            }

            await db.SaveChangesAsync();
            Console.WriteLine("[SEED] Demo jobs created successfully");
        }
        else
        {
            Console.WriteLine("[SEED] Jobs already exist, skipping seed");
        }
    }
}
