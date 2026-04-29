using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Pages.Jobs;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public List<EvacJob> Jobs { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if user is authenticated
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToPage("/Login");
        }

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
