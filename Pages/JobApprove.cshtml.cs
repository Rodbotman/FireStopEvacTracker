using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Pages;

public class JobApproveModel : PageModel
{
    private readonly AppDbContext _context;

    public JobApproveModel(AppDbContext context)
    {
        _context = context;
    }

    public EvacJob? Job { get; set; }
    public JobApproval? Approval { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsApproved { get; set; }

    [BindProperty]
    public bool? LayoutAccuracyApproved { get; set; }

    [BindProperty]
    public bool? FireEquipmentLocationsApproved { get; set; }

    [BindProperty]
    public bool? YouAreHereApproved { get; set; }

    [BindProperty]
    public bool? DiagramMountingLocationApproved { get; set; }

    [BindProperty]
    public string? ApproverName { get; set; }

    [BindProperty]
    public string? ChangesRequired { get; set; }

    public async Task<IActionResult> OnGetAsync(string shareCode)
    {
        if (string.IsNullOrEmpty(shareCode))
        {
            ErrorMessage = "Invalid approval link";
            return Page();
        }

        // Find job by share code
        Job = await _context.EvacJobs
            .Include(j => j.Approvals)
            .FirstOrDefaultAsync(j => j.ShareCode == shareCode);

        if (Job == null)
        {
            ErrorMessage = "Job not found or approval link has expired";
            return Page();
        }

        // Get or create approval record
        Approval = await _context.JobApprovals.FirstOrDefaultAsync(a => a.JobId == Job.Id);

        if (Approval == null)
        {
            Approval = new JobApproval
            {
                JobId = Job.Id,
                ClientName = Job.ClientName,
                ClientEmail = "",
                ApproverName = ""
            };
        }
        else
        {
            // Populate form with existing data
            LayoutAccuracyApproved = Approval.LayoutAccuracyApproved;
            FireEquipmentLocationsApproved = Approval.FireEquipmentLocationsApproved;
            YouAreHereApproved = Approval.YouAreHereApproved;
            DiagramMountingLocationApproved = Approval.DiagramMountingLocationApproved;
            ApproverName = Approval.ApproverName;
            ChangesRequired = Approval.ChangesRequired;
            IsApproved = Approval.IsApproved;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string shareCode)
    {
        if (string.IsNullOrEmpty(shareCode))
        {
            ErrorMessage = "Invalid approval link";
            return Page();
        }

        // Find job by share code
        Job = await _context.EvacJobs
            .Include(j => j.Approvals)
            .FirstOrDefaultAsync(j => j.ShareCode == shareCode);

        if (Job == null)
        {
            ErrorMessage = "Job not found or approval link has expired";
            return Page();
        }

        // Get or create approval
        Approval = await _context.JobApprovals.FirstOrDefaultAsync(a => a.JobId == Job.Id);

        if (Approval == null)
        {
            Approval = new JobApproval
            {
                JobId = Job.Id,
                ClientName = Job.ClientName,
                ClientEmail = "",
                ApproverName = ApproverName
            };
            _context.JobApprovals.Add(Approval);
        }

        // Update approval data
        Approval.LayoutAccuracyApproved = LayoutAccuracyApproved;
        Approval.FireEquipmentLocationsApproved = FireEquipmentLocationsApproved;
        Approval.YouAreHereApproved = YouAreHereApproved;
        Approval.DiagramMountingLocationApproved = DiagramMountingLocationApproved;
        Approval.ApproverName = ApproverName;
        Approval.ChangesRequired = ChangesRequired;
        Approval.UpdatedAt = DateTime.UtcNow;

        // If all items are checked, mark as approved
        if (Approval.IsApproved)
        {
            Approval.ApprovedAt = DateTime.UtcNow;
            // Update job status
            Job.Status = JobStatus.Approved;
            Job.UpdatedAt = DateTime.UtcNow;
            Job.StatusUpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        IsApproved = Approval.IsApproved;

        return Page();
    }

    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnPostChangesAsync(string shareCode)
    {
        if (string.IsNullOrEmpty(shareCode))
        {
            ErrorMessage = "Invalid approval link";
            return Page();
        }

        // Find job by share code
        Job = await _context.EvacJobs
            .Include(j => j.Approvals)
            .FirstOrDefaultAsync(j => j.ShareCode == shareCode);

        if (Job == null)
        {
            ErrorMessage = "Job not found or approval link has expired";
            return Page();
        }

        // Check if changes were provided
        if (string.IsNullOrWhiteSpace(ChangesRequired))
        {
            ErrorMessage = "Please enter changes before submitting.";
            // Reload existing data
            Approval = await _context.JobApprovals.FirstOrDefaultAsync(a => a.JobId == Job.Id);
            return Page();
        }

        // Create a note from the changes
        var note = new JobNote
        {
            EvacJobId = Job.Id,
            Content = $"Client Changes Required:\n\n{ChangesRequired}",
            AddedBy = ApproverName ?? "Client Feedback"
        };

        _context.JobNotes.Add(note);
        await _context.SaveChangesAsync();

        // Get updated data to display
        Approval = await _context.JobApprovals.FirstOrDefaultAsync(a => a.JobId == Job.Id);
        SuccessMessage = "✓ Changes submitted successfully and added to job notes!";

        return Page();
    }
