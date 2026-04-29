using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Pages.Admin
{
    public class UsersModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;

        public UsersModel(AppDbContext context, AuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        public List<User> Users { get; set; } = new();
        public string Message { get; set; } = "";
        public string MessageType { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is authenticated
            var userName = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(userName))
            {
                return RedirectToPage("/Login");
            }

            // Check if user is admin
            var userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToPage("/Index");
            }

            // Load all users
            Users = await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string action)
        {
            // Check if user is admin
            var userRole = HttpContext.Session.GetString("Role");
            if (userRole != "Admin")
            {
                return RedirectToPage("/Index");
            }

            try
            {
                switch (action)
                {
                    case "add":
                        return await AddUser();
                    case "edit":
                        return await EditUser();
                    case "delete":
                        return await DeleteUser();
                    case "resetPassword":
                        return await ResetPassword();
                    default:
                        Message = "Unknown action";
                        MessageType = "error";
                        break;
                }
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                MessageType = "error";
            }

            // Reload users
            Users = await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
            return Page();
        }

        private async Task<IActionResult> AddUser()
        {
            var username = Request.Form["newUsername"].ToString();
            var email = Request.Form["newEmail"].ToString();
            var fullName = Request.Form["newFullName"].ToString();
            var password = Request.Form["newPassword"].ToString();
            var role = Request.Form["newRole"].ToString();
            var isActiveStr = Request.Form["newIsActive"].ToString();

            // Validate input
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role))
            {
                Message = "All fields are required";
                MessageType = "error";
                return await OnGetAsync();
            }

            // Check if username already exists
            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                Message = $"Username '{username}' already exists";
                MessageType = "error";
                return await OnGetAsync();
            }

            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                Message = $"Email '{email}' already exists";
                MessageType = "error";
                return await OnGetAsync();
            }

            var newUser = new User
            {
                Username = username,
                Email = email,
                FullName = fullName,
                PasswordHash = _authService.HashPassword(password),
                Role = role,
                IsActive = !string.IsNullOrEmpty(isActiveStr),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            Message = $"User '{username}' created successfully";
            MessageType = "success";
            return await OnGetAsync();
        }

        private async Task<IActionResult> EditUser()
        {
            var userIdStr = Request.Form["userId"].ToString();
            if (!int.TryParse(userIdStr, out int userId))
            {
                Message = "Invalid user ID";
                MessageType = "error";
                return await OnGetAsync();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                Message = "User not found";
                MessageType = "error";
                return await OnGetAsync();
            }

            var fullName = Request.Form["editFullName"].ToString();
            var email = Request.Form["editEmail"].ToString();
            var role = Request.Form["editRole"].ToString();
            var isActiveStr = Request.Form["editIsActive"].ToString();

            // Validate
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
            {
                Message = "All fields are required";
                MessageType = "error";
                return await OnGetAsync();
            }

            // Check if new email already exists (and it's not the same user)
            if (email != user.Email && await _context.Users.AnyAsync(u => u.Email == email))
            {
                Message = $"Email '{email}' is already in use";
                MessageType = "error";
                return await OnGetAsync();
            }

            user.FullName = fullName;
            user.Email = email;
            user.Role = role;
            user.IsActive = !string.IsNullOrEmpty(isActiveStr);
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            Message = $"User '{user.Username}' updated successfully";
            MessageType = "success";
            return await OnGetAsync();
        }

        private async Task<IActionResult> DeleteUser()
        {
            var userIdStr = Request.Form["userId"].ToString();
            if (!int.TryParse(userIdStr, out int userId))
            {
                Message = "Invalid user ID";
                MessageType = "error";
                return await OnGetAsync();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                Message = "User not found";
                MessageType = "error";
                return await OnGetAsync();
            }

            // Prevent deleting the default admin user
            if (user.Username == "admin")
            {
                Message = "Cannot delete the default admin user";
                MessageType = "error";
                return await OnGetAsync();
            }

            var username = user.Username;
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            Message = $"User '{username}' deleted successfully";
            MessageType = "success";
            return await OnGetAsync();
        }

        private async Task<IActionResult> ResetPassword()
        {
            var userIdStr = Request.Form["userId"].ToString();
            if (!int.TryParse(userIdStr, out int userId))
            {
                Message = "Invalid user ID";
                MessageType = "error";
                return await OnGetAsync();
            }

            var newPassword = Request.Form["resetNewPassword"].ToString();
            var confirmPassword = Request.Form["resetConfirmPassword"].ToString();

            // Validate
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                Message = "Password is required";
                MessageType = "error";
                return await OnGetAsync();
            }

            if (newPassword != confirmPassword)
            {
                Message = "Passwords do not match";
                MessageType = "error";
                return await OnGetAsync();
            }

            if (newPassword.Length < 6)
            {
                Message = "Password must be at least 6 characters";
                MessageType = "error";
                return await OnGetAsync();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                Message = "User not found";
                MessageType = "error";
                return await OnGetAsync();
            }

            user.PasswordHash = _authService.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            Message = $"Password for '{user.Username}' reset successfully";
            MessageType = "success";
            return await OnGetAsync();
        }
    }
}
