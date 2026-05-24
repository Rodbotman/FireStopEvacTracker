using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using Microsoft.EntityFrameworkCore;

// This is a test to verify markup feature
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=data/firestop_evac_tracker.db")
    .Options;

using (var context = new AppDbContext(options))
{
    // Get first job with PDF
    var jobWithPdf = context.EvacJobs.FirstOrDefault(j => !string.IsNullOrEmpty(j.DraftPdfPath));
    if (jobWithPdf == null)
    {
        Console.WriteLine("No jobs with PDF found. Creating test job...");
        jobWithPdf = new EvacJob
        {
            ClientName = "Test Client",
            SiteAddress = "123 Test Street",
            JobName = "20260524_TEST_MARKUP",
            Status = JobStatus.SentToCustomer,
            DraftPdfPath = "/uploads/test.pdf",
            ShareCode = "TEST1234MARKUP56"
        };
        context.EvacJobs.Add(jobWithPdf);
        context.SaveChanges();
    }

    // Create or update approval
    var approval = context.JobApprovals.FirstOrDefault(a => a.JobId == jobWithPdf.Id)
        ?? new JobApproval
        {
            JobId = jobWithPdf.Id,
            ClientName = jobWithPdf.ClientName,
            ClientEmail = "test@example.com"
        };

    if (approval.Id == 0)
        context.JobApprovals.Add(approval);

    context.SaveChanges();

    // Create test annotation
    var testAnnotation = new JobAnnotation
    {
        JobApprovalId = approval.Id,
        CanvasDataUrl = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="
    };

    context.JobAnnotations.Add(testAnnotation);
    context.SaveChanges();

    Console.WriteLine($"✅ Test job created: {jobWithPdf.Id}");
    Console.WriteLine($"✅ Approval created: {approval.Id}");
    Console.WriteLine($"✅ Annotation created: {testAnnotation.Id}");
    Console.WriteLine($"Share code: {jobWithPdf.ShareCode}");
    Console.WriteLine($"JobApprove URL: /JobApprove/{jobWithPdf.ShareCode}");

    // Verify annotation can be loaded
    var loadedApproval = context.JobApprovals
        .Include(a => a.Annotation)
        .FirstOrDefault(a => a.Id == approval.Id);

    if (loadedApproval?.Annotation != null)
        Console.WriteLine("✅ Annotation successfully loaded from database");
    else
        Console.WriteLine("❌ Annotation failed to load");
}
