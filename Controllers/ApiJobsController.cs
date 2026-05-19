using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mime;

namespace FireStopEvacTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthService _authService;
    private readonly ReportService _reportService;

    public JobsController(AppDbContext db, IHttpContextAccessor httpContextAccessor, AuthService authService, ReportService reportService)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _authService = authService;
        _reportService = reportService;
    }

    [HttpPost("toggle-billed/{id}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ToggleBilled(int id)
    {
        // Check if user is authorized (admin only)
        if (!await _authService.IsUserAuthorizedAsync(_httpContextAccessor.HttpContext!, UserRole.Admin))
            return Unauthorized(new { error = "You do not have permission to mark jobs as billed" });

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

    [HttpGet("generate-status-report")]
    public async Task<IActionResult> GenerateStatusReport()
    {
        // Check if user is authenticated
        var userId = _httpContextAccessor.HttpContext?.Session.GetInt32("UserId");
        if (userId == null)
            return Unauthorized(new { error = "You must be logged in to generate reports" });

        try
        {
            var pdfBytes = await _reportService.GenerateStatusReportAsync();
            var fileName = $"Job_Status_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            
            return File(pdfBytes, MediaTypeNames.Application.Pdf, fileName);
        }
        catch (Exception ex)
        {
            var errorMessage = "Error generating report: " + ex.Message;
            var host = Request.Host.Host;
            if (host.Contains("134.199.146.192") || host.Contains("staging.firestopevacs"))
            {
                errorMessage = ex.ToString();
            }
            return StatusCode(500, new { error = errorMessage });
        }
    }
}

public class UpdateBilledAmountRequest
{
    public decimal Amount { get; set; }
}
