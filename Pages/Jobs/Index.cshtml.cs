using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

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

    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> OnPostToggleBilledAsync(int id)
    {
        var job = await _db.EvacJobs.FindAsync(id);

        if (job is null)
            return NotFound();

        job.IsBilled = !job.IsBilled;

        // If marking as billed, add a note
        if (job.IsBilled)
        {
            var note = new JobNote
            {
                EvacJobId = id,
                Content = "billed to Firestop this week",
                AddedBy = HttpContext.Session.GetString("FullName") ?? "System",
                CreatedAt = DateTime.UtcNow
            };
            _db.JobNotes.Add(note);
        }

        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new JsonResult(new { isBilled = job.IsBilled });
    }
}
