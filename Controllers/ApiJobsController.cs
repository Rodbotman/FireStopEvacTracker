using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JobsController(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpPost("toggle-billed/{id}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ToggleBilled(int id)
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
                AddedBy = _httpContextAccessor.HttpContext?.Session.GetString("FullName") ?? "System",
                CreatedAt = DateTime.UtcNow
            };
            _db.JobNotes.Add(note);
        }

        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { isBilled = job.IsBilled });
    }
}
