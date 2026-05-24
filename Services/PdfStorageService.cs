using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FireStopEvacTracker.Services;

public class PdfStorageService
{
    private readonly IWebHostEnvironment _environment;

    public PdfStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string GetUploadsPath()
    {
        return Path.Combine(_environment.WebRootPath, "uploads");
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

        // Convert first page to PNG for markup preview
        _ = ConvertPdfToImageAsync(fullPath, fileName);

        return (fileName, relativePath);
    }

    private async Task<string?> ConvertPdfToImageAsync(string pdfFullPath, string pdfFileName)
    {
        try
        {
            var imageFileName = Path.GetFileNameWithoutExtension(pdfFileName) + ".png";
            var imageFullPath = Path.Combine(Path.GetDirectoryName(pdfFullPath)!, imageFileName);

            // Use ImageMagick convert command (must be installed on system)
            // Format: convert input.pdf[0] output.png (converts first page only)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "convert",
                    Arguments = $"\"{pdfFullPath}[0]\" -density 150 -quality 85 \"{imageFullPath}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(imageFullPath))
                return imageFileName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PDF to image conversion failed: {ex.Message}");
        }

        return null;
    }

    public string? GetPreviewImagePath(string? pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        var imagePath = Path.ChangeExtension(pdfPath, ".png");
        var fullImagePath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        return File.Exists(fullImagePath) ? imagePath : null;
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
