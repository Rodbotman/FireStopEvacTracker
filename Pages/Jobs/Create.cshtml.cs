using System.ComponentModel.DataAnnotations;
using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using FireStopEvacTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FireStopEvacTracker.Pages.Jobs;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly JobNameService _jobNameService;
    private readonly PdfStorageService _pdfStorage;

    public CreateModel(AppDbContext db, JobNameService jobNameService, PdfStorageService pdfStorage)
    {
        _db = db;
        _jobNameService = jobNameService;
        _pdfStorage = pdfStorage;
    }

    [BindProperty]
    public JobInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? DraftPdf { get; set; }

    public void OnGet()
    {
        Input.DateStarted = DateTime.Today;
        Input.Status = JobStatus.New;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var jobName = await _jobNameService.GenerateAsync(Input.DateStarted, Input.ClientName, Input.SiteAddress);

        var job = new EvacJob
        {
            DateStarted = Input.DateStarted,
            ClientName = Input.ClientName.Trim(),
            SiteAddress = Input.SiteAddress.Trim(),
            Status = Input.Status,
            Notes = Input.Notes,
            JobName = jobName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (DraftPdf is not null)
        {
            var saved = await _pdfStorage.SaveDraftPdfAsync(DraftPdf, jobName);
            job.DraftPdfFileName = saved.fileName;
            job.DraftPdfPath = saved.relativePath;
        }

        _db.EvacJobs.Add(job);
        await _db.SaveChangesAsync();

        return RedirectToPage("Details", new { id = job.Id });
    }

    public class JobInput
    {
        [Required]
        [DataType(DataType.Date)]
        public DateTime DateStarted { get; set; } = DateTime.Today;

        [Required]
        public string ClientName { get; set; } = string.Empty;

        [Required]
        public string SiteAddress { get; set; } = string.Empty;

        public string Status { get; set; } = JobStatus.New;

        public string? Notes { get; set; }
    }
}
