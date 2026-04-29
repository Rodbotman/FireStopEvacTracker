using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Pages.Jobs;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly PdfStorageService _pdfStorage;

    public DetailsModel(AppDbContext db, PdfStorageService pdfStorage)
    {
        _db = db;
        _pdfStorage = pdfStorage;
    }

    public EvacJob? Job { get; set; }
    public JobApproval? Approval { get; set; }

    [BindProperty]
    public UpdateJobInput UpdateInput { get; set; } = new();

    [BindProperty]
    public string? NewNote { get; set; }

    [BindProperty]
    public string? NoteAddedBy { get; set; }

    [BindProperty]
    public int? EditNoteId { get; set; }

    [BindProperty]
    public string? EditNoteContent { get; set; }

    [BindProperty]
    public IFormFile? NewDraftPdf { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        // Check if user is authenticated
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToPage("/Login");
        }

        Job = await _db.EvacJobs
            .Include(j => j.JobNotes.OrderByDescending(n => n.CreatedAt))
            .Include(j => j.Approvals)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (Job is null)
            return NotFound();

        Approval = await _db.JobApprovals.FirstOrDefaultAsync(a => a.JobId == id);

        UpdateInput.Status = Job.Status;
        UpdateInput.Notes = Job.Notes;

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        var job = await _db.EvacJobs.FindAsync(id);

        if (job is null)
            return NotFound();

        // Set StatusUpdatedAt if status is being changed
        if (job.Status != UpdateInput.Status)
        {
            job.StatusUpdatedAt = DateTime.UtcNow;
        }

        job.Status = UpdateInput.Status;
        job.Notes = UpdateInput.Notes;
        job.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReplacePdfAsync(int id)
    {
        var job = await _db.EvacJobs.FindAsync(id);

        if (job is null)
            return NotFound();

        if (NewDraftPdf is null)
            return RedirectToPage(new { id });

        _pdfStorage.DeletePdfIfExists(job.DraftPdfPath);

        var saved = await _pdfStorage.SaveDraftPdfAsync(NewDraftPdf, job.JobName);
        job.DraftPdfFileName = saved.fileName;
        job.DraftPdfPath = saved.relativePath;
        job.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemovePdfAsync(int id)
    {
        var job = await _db.EvacJobs.FindAsync(id);

        if (job is null)
            return NotFound();

        _pdfStorage.DeletePdfIfExists(job.DraftPdfPath);

        job.DraftPdfFileName = null;
        job.DraftPdfPath = null;
        job.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddNoteAsync(int id)
    {
        var job = await _db.EvacJobs.FindAsync(id);

        if (job is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(NewNote) || string.IsNullOrWhiteSpace(NoteAddedBy))
            return RedirectToPage(new { id });

        var note = new JobNote
        {
            EvacJobId = id,
            Content = NewNote,
            AddedBy = NoteAddedBy,
            CreatedAt = DateTime.UtcNow
        };

        _db.JobNotes.Add(note);
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        NewNote = null;
        NoteAddedBy = null;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEditNoteAsync(int id)
    {
        // Check if user is authenticated
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToPage("/Login");
        }

        // Check if user is admin
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin")
        {
            return Unauthorized();
        }

        if (EditNoteId == null || string.IsNullOrWhiteSpace(EditNoteContent))
            return RedirectToPage(new { id });

        var note = await _db.JobNotes.FindAsync(EditNoteId);
        if (note is null)
            return NotFound();

        note.Content = EditNoteContent;
        note.CreatedAt = DateTime.UtcNow; // Update timestamp to show it was edited

        await _db.SaveChangesAsync();

        EditNoteId = null;
        EditNoteContent = null;

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteNoteAsync(int id, int noteId)
    {
        // Check if user is authenticated
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToPage("/Login");
        }

        // Check if user is admin
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin")
        {
            return Unauthorized();
        }

        var note = await _db.JobNotes.FindAsync(noteId);
        if (note is null)
            return NotFound();

        _db.JobNotes.Remove(note);
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostGenerateShareCodeAsync(int id)
    {
        var job = await _db.EvacJobs.FindAsync(id);

        if (job is null)
            return NotFound();

        // Generate a unique share code if one doesn't exist
        if (string.IsNullOrEmpty(job.ShareCode))
        {
            job.ShareCode = GenerateShareCode();
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    private string GenerateShareCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 16)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        // Check if user is authenticated
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return RedirectToPage("/Login");
        }

        // Check if user is admin
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin")
        {
            return Unauthorized();
        }

        try
        {
            var job = await _db.EvacJobs
                .Include(j => j.Approvals)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job is null)
                return NotFound();

            // Delete associated PDF if exists
            if (!string.IsNullOrWhiteSpace(job.DraftPdfPath))
            {
                try
                {
                    _pdfStorage.DeletePdfIfExists(job.DraftPdfPath);
                }
                catch (Exception ex)
                {
                    // Log error but continue with job deletion
                    System.Diagnostics.Debug.WriteLine($"Error deleting PDF: {ex.Message}");
                }
            }

            // Delete the job (cascade delete will remove related approvals and notes)
            _db.EvacJobs.Remove(job);
            await _db.SaveChangesAsync();

            return RedirectToPage("/Jobs/Index");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting job: {ex.Message}");
            throw;
        }
    }

    public class UpdateJobInput
    {
        public string Status { get; set; } = JobStatus.New;
        public string? Notes { get; set; }
    }
}
