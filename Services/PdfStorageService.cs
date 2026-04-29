using System.Text.RegularExpressions;

namespace FireStopEvacTracker.Services;

public class PdfStorageService
{
    private readonly IWebHostEnvironment _environment;

    public PdfStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<(string fileName, string relativePath)> SaveDraftPdfAsync(IFormFile pdfFile, string jobName)
    {
        if (pdfFile.Length == 0)
            throw new InvalidOperationException("PDF file is empty.");

        var extension = Path.GetExtension(pdfFile.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only PDF files are allowed.");

        var safeJobName = Regex.Replace(jobName, @"[^a-zA-Z0-9_\-]+", "_");
        var safeOriginalName = Regex.Replace(Path.GetFileNameWithoutExtension(pdfFile.FileName), @"[^a-zA-Z0-9_\-]+", "_");

        var folder = Path.Combine(_environment.WebRootPath, "uploads", safeJobName);
        Directory.CreateDirectory(folder);

        var fileName = $"{safeJobName}_DRAFT_{DateTime.Now:yyyyMMdd_HHmmss}_{safeOriginalName}.pdf";
        var fullPath = Path.Combine(folder, fileName);

        await using var stream = File.Create(fullPath);
        await pdfFile.CopyToAsync(stream);

        var relativePath = $"/uploads/{safeJobName}/{fileName}";
        return (fileName, relativePath);
    }

    public void DeletePdfIfExists(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;

        try
        {
            var cleanPath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_environment.WebRootPath, cleanPath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Could not delete PDF file at {relativePath}: {ex.Message}");
            // Don't throw - this is a best-effort cleanup operation
        }
    }
}
