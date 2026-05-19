using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Colors;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
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
        try
        {
            // Get the cutoff date for completed jobs (4 weeks ago)
            var fourWeeksAgo = DateTime.Today.AddDays(-28);

            // Fetch all jobs with their notes
            var jobs = await _db.EvacJobs
                .Include(j => j.JobNotes)
                .Where(j => j.Status != JobStatus.Complete || j.DateStarted >= fourWeeksAgo)
                .OrderByDescending(j => j.DateStarted)
                .ThenByDescending(j => j.Id)
                .ToListAsync();

            foreach (var job in jobs)
            {
                job.JobNotes = job.JobNotes
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();
            }

            // Create PDF
            using var stream = new MemoryStream();
            var writer = new PdfWriter(stream);
            var pdfDoc = new PdfDocument(writer);
            var document = new Document(pdfDoc);

            // Add title
            document.Add(new Paragraph("Job Status Report")
                .SetFontSize(24)
                .SetBold()
                .SetMarginBottom(5));

            // Add report date
            document.Add(new Paragraph($"Generated: {DateTime.Now:MMMM dd, yyyy h:mm tt}")
                .SetFontSize(10)
                .SetMarginBottom(20)
                .SetTextAlignment(TextAlignment.RIGHT));

            // Add summary
            var completedCount = jobs.Count(j => j.Status == JobStatus.Complete);
            var changesNeededCount = jobs.Count(j => j.Status == "Changes Needed");
            document.Add(new Paragraph($"Total Jobs: {jobs.Count} | Completed (Last 4 Weeks): {completedCount} | Changes Needed: {changesNeededCount}")
                .SetFontSize(11)
                .SetBold()
                .SetMarginBottom(20));

            // Add jobs
            foreach (var job in jobs)
            {
                AddJobSection(document, job);
            }

            document.Close();
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            throw new Exception($"PDF Generation Error: {ex.GetType().Name} - {ex.Message}", ex);
        }
    }

    private void AddJobSection(Document document, EvacJob job)
    {
        try
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
            var detailsTable = new Table(2)
                .UseAllAvailableWidth()
                .SetMarginBottom(10);

            detailsTable.AddCell(CreateCell("Client:", job.ClientName));
            detailsTable.AddCell(CreateCell("Address:", job.SiteAddress));
            detailsTable.AddCell(CreateCell("Status:", job.Status));
            detailsTable.AddCell(CreateCell("Date Started:", job.DateStarted.ToString("MMMM dd, yyyy")));

            document.Add(detailsTable);

            // Only show notes if status is "Changes Needed"
            if (job.Status == "Changes Needed" && job.JobNotes.Any())
            {
                document.Add(new Paragraph("Notes:")
                    .SetBold()
                    .SetFontSize(11)
                    .SetMarginTop(5)
                    .SetMarginBottom(5));

                // Add notes
                foreach (var note in job.JobNotes)
                {
                    var noteText = new Paragraph()
                        .SetFontSize(10)
                        .SetMarginLeft(10)
                        .SetMarginBottom(5);

                    noteText.Add($"• {note.Content}\n");
                    noteText.Add(new Text($"  - {note.AddedBy} ({note.CreatedAt:MMM dd, yyyy h:mm tt})")
                        .SetFontSize(9));

                    document.Add(noteText);
                }
            }

            // Add spacing between jobs
            document.Add(new Paragraph("\n"));
        }
        catch (Exception ex)
        {
            throw new Exception($"Error adding job section for job {job.Id}: {ex.Message}", ex);
        }
    }

    private Cell CreateCell(string label, string value)
    {
        try
        {
            var cell = new Cell();
            cell.Add(new Paragraph(label)
                .SetBold()
                .SetFontSize(10));
            cell.Add(new Paragraph(value)
                .SetFontSize(10));
            return cell;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error creating cell: {ex.Message}", ex);
        }
    }

    private Color GetStatusColor(string status)
    {
        return status switch
        {
            "New" => new DeviceRgb(200, 220, 255), // Light blue
            "Drafting" => new DeviceRgb(255, 240, 200), // Light orange
            "Sent to Office" => new DeviceRgb(255, 250, 200), // Light yellow
            "Sent to Customer" => new DeviceRgb(255, 230, 230), // Light red
            "Changes Needed" => new DeviceRgb(255, 200, 200), // Red
            "Approved" => new DeviceRgb(200, 255, 200), // Light green
            "Complete" => new DeviceRgb(150, 220, 150), // Green
            _ => new DeviceRgb(230, 230, 230) // Gray
        };
    }
}
