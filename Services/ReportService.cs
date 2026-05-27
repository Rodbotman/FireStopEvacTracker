using FireStopEvacTracker.Data;
using FireStopEvacTracker.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Colors;
using iText.Layout;
using iText.Layout.Borders;
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
                .ToListAsync();

            // Sort in memory: non-completed first (by date desc), then completed (by date desc)
            jobs = jobs
                .OrderBy(j => j.Status == JobStatus.Complete ? 1 : 0)
                .ThenByDescending(j => j.DateStarted)
                .ThenByDescending(j => j.Id)
                .ToList();

            foreach (var job in jobs)
            {
                job.JobNotes = job.JobNotes
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();
            }

            // Create PDF
            using var stream = new MemoryStream();
            using var writer = new PdfWriter(stream);
            using var pdfDoc = new PdfDocument(writer);
            using var document = new Document(pdfDoc);

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
            var changesNeededCount = jobs.Count(j => j.Status == JobStatus.ChangesNeeded);
            document.Add(new Paragraph($"Total Jobs: {jobs.Count} | Completed (Last 4 Weeks): {completedCount} | Changes Needed: {changesNeededCount}")
                .SetFontSize(11)
                .SetBold()
                .SetMarginBottom(20));

            // Add job table header
            var table = new Table(UnitValue.CreatePercentArray(new float[] { 1.2f, 2f, 2.5f, 3f, 1.1f }))
                .UseAllAvailableWidth()
                .SetMarginBottom(20);

            foreach (var header in new[] { "Date", "Job Name", "Client", "Address", "Status" })
            {
                table.AddHeaderCell(new Cell().Add(new Paragraph(header)
                        .SetFontSize(10)
                        .SetBold()
                        .SetFontColor(new DeviceRgb(255, 255, 255)))
                    .SetBackgroundColor(new DeviceRgb(40, 44, 52))
                    .SetPadding(6));
            }

            foreach (var job in jobs)
            {
                table.AddCell(CreateBodyCell(job.DateStarted.ToString("dd/MM/yyyy")));
                table.AddCell(CreateBodyCell(job.JobName));
                table.AddCell(CreateBodyCell(job.ClientName));
                table.AddCell(CreateBodyCell(job.SiteAddress));
                table.AddCell(CreateStatusCell(job.Status));

                if (job.Status == JobStatus.ChangesNeeded && job.JobNotes.Any())
                {
                    var notesText = string.Join("\n", job.JobNotes.Select(note => $"• {note.Content} ({note.AddedBy} {note.CreatedAt:MMM dd, yyyy h:mm tt})"));
                    table.AddCell(new Cell(1, 5)
                        .Add(new Paragraph("Notes:")
                            .SetFontSize(10)
                            .SetBold())
                        .Add(new Paragraph(notesText)
                            .SetFontSize(9)
                            .SetMarginTop(4))
                        .SetBorder(new SolidBorder(ColorConstants.LIGHT_GRAY, 0.5f))
                        .SetBackgroundColor(new DeviceRgb(245, 245, 245))
                        .SetPadding(8));
                }
            }

            document.Add(table);

            document.Close();
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            throw new Exception($"PDF Generation Error: {ex.GetType().Name} - {ex.Message}", ex);
        }
    }

    private Cell CreateBodyCell(string text)
    {
        return new Cell().Add(new Paragraph(text)
            .SetFontSize(10))
            .SetPadding(6)
            .SetBorder(new SolidBorder(new DeviceRgb(211, 211, 211), 0.5f));
    }

    private Cell CreateStatusCell(string status)
    {
        var statusColor = GetStatusColor(status);
        var textColor = GetStatusTextColor(status);
        return new Cell().Add(new Paragraph(status)
            .SetFontSize(9)
            .SetBold()
            .SetFontColor(textColor)
            .SetTextAlignment(TextAlignment.CENTER))
            .SetBackgroundColor(statusColor)
            .SetPadding(6)
            .SetBorder(new SolidBorder(new DeviceRgb(211, 211, 211), 0.5f));
    }

    private Color GetStatusTextColor(string status)
    {
        // Match CSS: dark text only on the light Amber/Yellow pills; white elsewhere.
        return status switch
        {
            "Sent to Office" => new DeviceRgb(51, 51, 51),
            "Sent to Customer" => new DeviceRgb(51, 51, 51),
            _ => new DeviceRgb(255, 255, 255)
        };
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
            "New" => new DeviceRgb(244, 67, 54), // Red (#f44336)
            "Drafting" => new DeviceRgb(255, 152, 0), // Orange (#ff9800)
            "Sent to Office" => new DeviceRgb(255, 193, 7), // Amber (#ffc107)
            "Sent to Customer" => new DeviceRgb(255, 235, 59), // Light Yellow (#ffeb3b)
            "Changes Needed" => new DeviceRgb(255, 87, 34), // Deep Orange (#ff5722)
            "Changes Submitted" => new DeviceRgb(128, 0, 0), // Maroon (#800000)
            "Approved" => new DeviceRgb(139, 195, 74), // Light Green (#8bc34a)
            "Complete" => new DeviceRgb(156, 39, 176), // Purple (#9c27b0)
            _ => new DeviceRgb(230, 230, 230) // Gray
        };
    }
}
