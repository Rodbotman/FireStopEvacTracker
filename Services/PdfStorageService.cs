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

        // Convert every page to PNG for markup preview (fire-and-forget)
        _ = ConvertPdfToImagesAsync(fullPath, fileName);

        return (fileName, relativePath);
    }

    /// <summary>
    /// Converts every page of the PDF to a separate PNG named &lt;base&gt;_page_N.png
    /// (1-indexed). Also writes &lt;base&gt;.png pointing at page 1 for backward
    /// compatibility with code paths that still ask for the legacy single image.
    /// </summary>
    private async Task<int> ConvertPdfToImagesAsync(string pdfFullPath, string pdfFileName)
    {
        try
        {
            var pageCount = await GetPdfPageCountAsync(pdfFullPath);
            if (pageCount <= 0)
                return 0;

            var folder = Path.GetDirectoryName(pdfFullPath)!;
            var baseName = Path.GetFileNameWithoutExtension(pdfFileName);
            var converted = 0;

            for (var i = 0; i < pageCount; i++)
            {
                var pageNumber = i + 1;
                var pageImageName = $"{baseName}_page_{pageNumber}.png";
                var pageImagePath = Path.Combine(folder, pageImageName);

                if (await RunConvertAsync($"-density 200 \"{pdfFullPath}[{i}]\" -quality 85 \"{pageImagePath}\""))
                    converted++;
            }

            // Backward-compat: <base>.png mirrors page 1
            if (converted > 0)
            {
                var legacyPath = Path.Combine(folder, $"{baseName}.png");
                var firstPagePath = Path.Combine(folder, $"{baseName}_page_1.png");
                if (File.Exists(firstPagePath))
                {
                    try { File.Copy(firstPagePath, legacyPath, overwrite: true); }
                    catch { /* best-effort */ }
                }
            }

            return converted;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PDF to image conversion failed: {ex.Message}");
            return 0;
        }
    }

    private static async Task<int> GetPdfPageCountAsync(string pdfFullPath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "identify",
                    Arguments = $"-format \"%n\\n\" \"{pdfFullPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // `identify -format "%n\n"` prints the page count repeated once per page.
            // Taking the first line gives the total.
            var firstLine = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (int.TryParse(firstLine, out var count) && count > 0)
                return count;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Page count detection failed: {ex.Message}");
        }
        return 0;
    }

    private static async Task<bool> RunConvertAsync(string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "convert",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"convert failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns the ordered list of relative PNG paths (one per page) for a given
    /// PDF path. Lazily generates page PNGs from the source PDF if they don't
    /// exist yet (handles pre-multipage uploads).
    /// </summary>
    public async Task<List<string>> GetPageImagePathsAsync(string? pdfRelativePath)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(pdfRelativePath))
            return result;

        var pdfFullPath = Path.Combine(_environment.WebRootPath,
            pdfRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(pdfFullPath))
            return result;

        var folder = Path.GetDirectoryName(pdfFullPath)!;
        var baseName = Path.GetFileNameWithoutExtension(pdfFullPath);
        var pdfRelativeDir = pdfRelativePath.Substring(0, pdfRelativePath.LastIndexOf('/'));

        // Look for existing _page_N.png files
        var existing = Directory.GetFiles(folder, $"{baseName}_page_*.png")
            .Select(p => Path.GetFileName(p))
            .OrderBy(name => ExtractPageNumber(name, baseName))
            .ToList();

        if (existing.Count == 0)
        {
            // Lazy-generate for pre-multipage uploads (single-page legacy or no PNG yet)
            await ConvertPdfToImagesAsync(pdfFullPath, Path.GetFileName(pdfFullPath));
            existing = Directory.GetFiles(folder, $"{baseName}_page_*.png")
                .Select(p => Path.GetFileName(p))
                .OrderBy(name => ExtractPageNumber(name, baseName))
                .ToList();
        }

        foreach (var name in existing)
            result.Add($"{pdfRelativeDir}/{name}");

        return result;
    }

    private static int ExtractPageNumber(string fileName, string baseName)
    {
        var prefix = $"{baseName}_page_";
        var withoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (withoutExt.StartsWith(prefix) && int.TryParse(withoutExt.Substring(prefix.Length), out var n))
            return n;
        return int.MaxValue;
    }

    /// <summary>
    /// Copies the source page PNG for a given PDF + page number into
    /// /uploads/&lt;job&gt;/snapshots/&lt;approvalId&gt;_page_&lt;n&gt;.png and returns the
    /// resulting /uploads/... relative path (or null if the source doesn't
    /// exist). Overwrites an existing snapshot at the same name (idempotent
    /// on re-save). Snapshots live in a subdirectory so the regular
    /// DeletePdfIfExists glob doesn't sweep them up when a PDF is replaced.
    /// </summary>
    public async Task<string?> CreatePageSnapshotAsync(string? pdfRelativePath, int pageNumber, int approvalId)
    {
        if (string.IsNullOrWhiteSpace(pdfRelativePath) || pageNumber < 1 || approvalId <= 0)
            return null;

        // Make sure the page PNGs exist (lazy generation for pre-multipage uploads)
        var pages = await GetPageImagePathsAsync(pdfRelativePath);
        if (pageNumber > pages.Count)
            return null;

        var sourceRelative = pages[pageNumber - 1];
        var sourceFull = Path.Combine(_environment.WebRootPath,
            sourceRelative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(sourceFull))
            return null;

        var pdfFolderRelative = pdfRelativePath.Substring(0, pdfRelativePath.LastIndexOf('/'));
        var snapshotsFolderRelative = $"{pdfFolderRelative}/snapshots";
        var snapshotsFolderFull = Path.Combine(_environment.WebRootPath,
            snapshotsFolderRelative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(snapshotsFolderFull);

        var snapshotName = $"{approvalId}_page_{pageNumber}.png";
        var snapshotFull = Path.Combine(snapshotsFolderFull, snapshotName);

        try
        {
            File.Copy(sourceFull, snapshotFull, overwrite: true);
            return $"{snapshotsFolderRelative}/{snapshotName}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Snapshot copy failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Deletes the snapshot PNG referenced by a JobAnnotation (best-effort).</summary>
    public void DeleteSnapshotIfExists(string? snapshotRelativePath)
    {
        if (string.IsNullOrWhiteSpace(snapshotRelativePath))
            return;
        try
        {
            var fullPath = Path.Combine(_environment.WebRootPath,
                snapshotRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Snapshot delete failed: {ex.Message}");
        }
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

            // Also delete generated PNG previews (legacy + per-page)
            var folder = Path.GetDirectoryName(fullPath);
            var baseName = Path.GetFileNameWithoutExtension(fullPath);
            if (folder != null && Directory.Exists(folder))
            {
                foreach (var png in Directory.GetFiles(folder, $"{baseName}*.png"))
                {
                    try { File.Delete(png); } catch { /* best-effort */ }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Warning: Could not delete PDF file at {relativePath}: {ex.Message}");
        }
    }
}
