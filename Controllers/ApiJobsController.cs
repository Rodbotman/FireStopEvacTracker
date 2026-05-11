using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthService _authService;

    public JobsController(AppDbContext db, IHttpContextAccessor httpContextAccessor, AuthService authService)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _authService = authService;
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

    [HttpPost("update-billed-amount/{id}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateBilledAmount(int id, [FromBody] UpdateBilledAmountRequest request)
    {
        // Check if user is authorized (admin only)
        if (!await _authService.IsUserAuthorizedAsync(_httpContextAccessor.HttpContext!, UserRole.Admin))
            return Unauthorized(new { error = "You do not have permission to update billing amounts" });

        if (request?.Amount < 0)
            return BadRequest(new { error = "Billed amount cannot be negative" });

        var job = await _db.EvacJobs.FindAsync(id);
        if (job is null)
            return NotFound();

        var oldAmount = job.BilledAmount;
        job.BilledAmount = request.Amount;
        job.UpdatedAt = DateTime.UtcNow;

        // Add a note recording the billing amount change
        var note = new JobNote
        {
            EvacJobId = id,
            Content = $"Billing amount updated to ${request.Amount:F2}",
            AddedBy = _httpContextAccessor.HttpContext?.Session.GetString("FullName") ?? "System",
            CreatedAt = DateTime.UtcNow
        };
        _db.JobNotes.Add(note);

        await _db.SaveChangesAsync();

        return Ok(new { billedAmount = job.BilledAmount });
    }
}

public class UpdateBilledAmountRequest
{
    public decimal Amount { get; set; }
}
