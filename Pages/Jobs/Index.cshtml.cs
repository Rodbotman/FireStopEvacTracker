using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace FireStopEvacTracker.Pages.Jobs;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;

    public IndexModel(AppDbContext db, AuthService authService)
    {
        _db = db;
        _authService = authService;
    }

    public List<EvacJob> Jobs { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public bool IsAdmin { get; set; } = false;

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if user is authenticated
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToPage("/Login");
        }

        // Check if user is admin
        var user = _authService.GetCurrentUser(HttpContext);
        IsAdmin = user?.Role == UserRole.Admin;

        var query = _db.EvacJobs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = Search.Trim().ToLower();
            query = query.Where(j =>
                j.ClientName.ToLower().Contains(search) ||
                j.SiteAddress.ToLower().Contains(search) ||
                j.JobName.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(Status))
        {
            query = query.Where(j => j.Status == Status);
        }

        Jobs = await query
            .OrderByDescending(j => j.DateStarted)
            .ThenByDescending(j => j.Id)
            .ToListAsync();

        return Page();
    }
}
