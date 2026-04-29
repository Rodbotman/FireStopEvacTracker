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

    [BindProperty]
    public UpdateJobInput UpdateInput { get; set; } = new();

    [BindProperty]
    public string? NewNote { get; set; }

    [BindProperty]
    public string? NoteAddedBy { get; set; }

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
            .FirstOrDefaultAsync(j => j.Id == id);

        if (Job is null)
            return NotFound();

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

    public class UpdateJobInput
    {
        public string Status { get; set; } = JobStatus.New;
        public string? Notes { get; set; }
    }
}
