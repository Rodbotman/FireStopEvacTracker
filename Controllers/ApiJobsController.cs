using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    private readonly IEmailService _emailService;
    private readonly EmailOptions _emailOptions;

    public JobsController(AppDbContext db, IHttpContextAccessor httpContextAccessor, AuthService authService, ReportService reportService, IEmailService emailService, IOptions<EmailOptions> emailOptions)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _authService = authService;
        _reportService = reportService;
        _emailService = emailService;
        _emailOptions = emailOptions.Value;
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

    [HttpPost("send-to-customer/{id}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SendToCustomer(int id, [FromBody] SendToCustomerRequest request)
    {
        if (!await _authService.IsUserAuthorizedAsync(_httpContextAccessor.HttpContext!, UserRole.Admin))
            return Unauthorized(new { error = "Admins only" });

        var clientEmail = request?.ClientEmail?.Trim();
        if (string.IsNullOrWhiteSpace(clientEmail))
            return BadRequest(new { error = "Client email is required" });

        var job = await _db.EvacJobs.FindAsync(id);
        if (job is null)
            return NotFound();

        if (string.IsNullOrEmpty(job.ShareCode))
            job.ShareCode = GenerateShareCode();

        job.ClientEmail = clientEmail;
        job.UpdatedAt = DateTime.UtcNow;

        var baseUrl = _emailOptions.AppBaseUrl?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            return StatusCode(500, new { error = "App base URL not configured" });

        var approvalLink = $"{baseUrl}/JobApprove/{job.ShareCode}";
        var safeClient = System.Net.WebUtility.HtmlEncode(job.ClientName);
        var safeAddress = System.Net.WebUtility.HtmlEncode(job.SiteAddress);

        var subject = $"Evacuation diagram approval - {job.ClientName}";
        var html = $@"<p>Hello {safeClient},</p>
<p>Your evacuation diagram for <strong>{safeAddress}</strong> is ready for your review.</p>
<p>Please click the link below to view the diagram and either approve it or request changes:</p>
<p><a href=""{approvalLink}"" style=""display:inline-block;padding:12px 24px;background:#e3382f;color:#fff;text-decoration:none;border-radius:4px;font-weight:600"">Review evacuation diagram</a></p>
<p style=""font-size:13px;color:#555"">Or copy this link into your browser:<br/>{approvalLink}</p>
<p>Thank you,<br/>FireStop</p>";
        var text = $"Hello {job.ClientName},\n\nYour evacuation diagram for {job.SiteAddress} is ready for review.\n\nApproval link: {approvalLink}\n\nThank you,\nFireStop";

        var result = await _emailService.SendAsync(clientEmail, subject, html, text, tag: "approval-link");
        if (!result.Success)
            return StatusCode(500, new { error = result.ErrorMessage ?? "Email send failed" });

        if (job.Status != JobStatus.SentToCustomer)
        {
            job.Status = JobStatus.SentToCustomer;
            job.StatusUpdatedAt = DateTime.UtcNow;
        }

        _db.JobNotes.Add(new JobNote
        {
            EvacJobId = job.Id,
            Content = $"Sent approval link to {clientEmail}",
            AddedBy = _httpContextAccessor.HttpContext?.Session.GetString("FullName") ?? "System",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new { messageId = result.MessageId, status = job.Status, shareCode = job.ShareCode });
    }

    private static string GenerateShareCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 16)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }

    [HttpPost("save-annotation")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveAnnotation([FromBody] SaveAnnotationRequest request)
    {
        if (request is null || request.JobApprovalId <= 0)
            return BadRequest(new { error = "Invalid approval ID" });

        // Normalize to a list of pages. The endpoint accepts either the new shape
        // ({ JobApprovalId, Pages: [{PageNumber, CanvasDataUrl}, ...] }) or the
        // legacy single-page shape ({ JobApprovalId, CanvasDataUrl }) which is
        // treated as page 1.
        var pages = (request.Pages ?? new List<AnnotationPagePayload>()).ToList();
        if (pages.Count == 0 && !string.IsNullOrWhiteSpace(request.CanvasDataUrl))
        {
            pages.Add(new AnnotationPagePayload
            {
                PageNumber = 1,
                CanvasDataUrl = request.CanvasDataUrl
            });
        }

        if (pages.Count == 0)
            return BadRequest(new { error = "No annotation data provided" });

        var approval = await _db.JobApprovals
            .Include(a => a.Annotations)
            .FirstOrDefaultAsync(a => a.Id == request.JobApprovalId);

        if (approval is null)
            return NotFound(new { error = "Approval not found" });

        var now = DateTime.UtcNow;
        var savedIds = new List<int>();

        foreach (var page in pages)
        {
            if (page.PageNumber < 1 || string.IsNullOrWhiteSpace(page.CanvasDataUrl))
                continue;

            var existing = approval.Annotations.FirstOrDefault(a => a.PageNumber == page.PageNumber);
            if (existing is null)
            {
                existing = new JobAnnotation
                {
                    JobApprovalId = approval.Id,
                    PageNumber = page.PageNumber,
                    CanvasDataUrl = page.CanvasDataUrl,
                    CreatedAt = now
                };
                _db.JobAnnotations.Add(existing);
                approval.Annotations.Add(existing);
            }
            else
            {
                existing.CanvasDataUrl = page.CanvasDataUrl;
                existing.CreatedAt = now;
            }
            savedIds.Add(existing.PageNumber);
        }

        approval.UpdatedAt = now;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, savedPages = savedIds });
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

public class SendToCustomerRequest
{
    public string? ClientEmail { get; set; }
}

public class SaveAnnotationRequest
{
    public int JobApprovalId { get; set; }

    // New multi-page shape
    public List<AnnotationPagePayload>? Pages { get; set; }

    // Legacy single-page shape (treated as page 1 when Pages is empty)
    public string? CanvasDataUrl { get; set; }
}

public class AnnotationPagePayload
{
    public int PageNumber { get; set; }
    public string? CanvasDataUrl { get; set; }
}
