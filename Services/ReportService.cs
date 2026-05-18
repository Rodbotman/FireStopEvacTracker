using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Services;

public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> GenerateStatusReportAsync()
    {
        // Get the cutoff date for completed jobs (4 weeks ago)
        var fourWeeksAgo = DateTime.Today.AddDays(-28);

        // Fetch all jobs with their notes
        var jobs = await _db.EvacJobs
            .Include(j => j.JobNotes.OrderByDescending(n => n.CreatedAt))
            .Where(j => j.Status != JobStatus.Complete || j.DateStarted >= fourWeeksAgo)
            .OrderByDescending(j => j.DateStarted)
            .ThenByDescending(j => j.Id)
            .ToListAsync();

        // Create PDF
        using var stream = new MemoryStream();
        var writer = new PdfWriter(stream);
        var pdfDoc = new PdfDocument(writer);
        var document = new Document(pdfDoc);

        // Add title
        var title = new Paragraph("Job Status Report")
            .SetFontSize(24)
            .SetBold()
            .SetMarginBottom(5);
        document.Add(title);

        // Add report date
        var reportDate = new Paragraph($"Generated: {DateTime.Now:MMMM dd, yyyy h:mm tt}")
            .SetFontSize(10)
            .SetMarginBottom(20)
            .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);
        document.Add(reportDate);

        // Add summary
        var completedCount = jobs.Count(j => j.Status == JobStatus.Complete);
        var changesNeededCount = jobs.Count(j => j.Status == "Changes Needed");
        var summary = new Paragraph($"Total Jobs: {jobs.Count} | Completed (Last 4 Weeks): {completedCount} | Changes Needed: {changesNeededCount}")
            .SetFontSize(11)
            .SetBold()
            .SetMarginBottom(20);
        document.Add(summary);

        // Add jobs
        foreach (var job in jobs)
        {
            AddJobSection(document, job);
        }

        document.Close();
        return stream.ToArray();
    }

    private void AddJobSection(Document document, EvacJob job)
    {
        // Job header with status
        var statusColor = GetStatusColor(job.Status);
        var jobHeader = new Paragraph()
            .SetBold()
            .SetFontSize(12)
            .SetPadding(10)
            .SetBackgroundColor(statusColor);

        jobHeader.Add($"{job.JobName} - {job.ClientName}");
        document.Add(jobHeader);

        // Job details
        var details = new Table(2)
            .UseAllAvailableWidth()
            .SetMarginBottom(10);

        details.AddCell(CreateCell("Client:", job.ClientName));
        details.AddCell(CreateCell("Address:", job.SiteAddress));
        details.AddCell(CreateCell("Status:", job.Status));
        details.AddCell(CreateCell("Date Started:", job.DateStarted.ToString("MMMM dd, yyyy")));

        document.Add(details);

        // Only show notes if status is "Changes Needed"
        if (job.Status == "Changes Needed" && job.JobNotes.Any())
        {
            var notesHeader = new Paragraph("Notes:")
                .SetBold()
                .SetFontSize(11)
                .SetMarginTop(5)
                .SetMarginBottom(5);
            document.Add(notesHeader);

            // Add notes
            foreach (var note in job.JobNotes)
            {
                var noteText = new Paragraph()
                    .SetFontSize(10)
                    .SetMarginLeft(10)
                    .SetMarginBottom(5);

                noteText.Add($"• {note.Content}\n");
                noteText.Add(new Text($"  - {note.AddedBy} ({note.CreatedAt:MMM dd, yyyy h:mm tt})")
                    .SetFontSize(9)
                    .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT));

                document.Add(noteText);
            }
        }

        // Add spacing between jobs
        document.Add(new Paragraph("\n"));
    }

    private Cell CreateCell(string label, string value)
    {
        var cell = new Cell();
        cell.Add(new Paragraph(label)
            .SetBold()
            .SetFontSize(10));
        cell.Add(new Paragraph(value)
            .SetFontSize(10));
        return cell;
    }

    private iText.Kernel.Colors.Color GetStatusColor(string status)
    {
        return status switch
        {
            "New" => new iText.Kernel.Colors.DeviceRgb(200, 220, 255), // Light blue
            "Drafting" => new iText.Kernel.Colors.DeviceRgb(255, 240, 200), // Light orange
            "Sent to Office" => new iText.Kernel.Colors.DeviceRgb(255, 250, 200), // Light yellow
            "Sent to Customer" => new iText.Kernel.Colors.DeviceRgb(255, 230, 230), // Light red
            "Changes Needed" => new iText.Kernel.Colors.DeviceRgb(255, 200, 200), // Red
            "Approved" => new iText.Kernel.Colors.DeviceRgb(200, 255, 200), // Light green
            "Complete" => new iText.Kernel.Colors.DeviceRgb(150, 220, 150), // Green
            _ => new iText.Kernel.Colors.DeviceRgb(230, 230, 230) // Gray
        };
    }
}
