using System.ComponentModel.DataAnnotations;

namespace FireStopEvacTracker.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Username")]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Email")]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Full Name")]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = UserRole.Viewer; // Default to Viewer

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class UserRole
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";

    public static readonly List<string> All = new() { Admin, Manager, Viewer };
}
