using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using System.Security.Cryptography;
using System.Text;

namespace FireStopEvacTracker.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    public bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput.Equals(hash);
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = _db.Users.FirstOrDefault(u => u.Username == username && u.IsActive);
        if (user == null) return null;

        if (!VerifyPassword(password, user.PasswordHash))
            return null;

        return user;
    }

    public async Task<User?> CreateUserAsync(string username, string email, string fullName, string password, string role = UserRole.Viewer)
    {
        // Check if user exists
        if (_db.Users.Any(u => u.Username == username || u.Email == email))
            return null;

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = fullName,
            PasswordHash = HashPassword(password),
            Role = role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<bool> IsUserAuthorizedAsync(HttpContext context, string requiredRole)
    {
        var userId = context.Session.GetInt32("UserId");
        if (userId == null) return false;

        var user = _db.Users.FirstOrDefault(u => u.Id == userId && u.IsActive);
        if (user == null) return false;

        // Admin can access everything
        if (user.Role == UserRole.Admin) return true;

        return user.Role == requiredRole || requiredRole == UserRole.Viewer;
    }

    public User? GetCurrentUser(HttpContext context)
    {
        var userId = context.Session.GetInt32("UserId");
        if (userId == null) return null;

        return _db.Users.FirstOrDefault(u => u.Id == userId);
    }
}
