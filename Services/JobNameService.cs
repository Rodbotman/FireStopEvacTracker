using System.Text.RegularExpressions;
using FireStopEvacTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Services;

public class JobNameService
{
    private readonly AppDbContext _db;

    public JobNameService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateAsync(DateTime dateStarted, string clientName, string siteAddress)
    {
        var datePart = dateStarted.ToString("yyyyMMdd");
        var clientPart = Clean(clientName);
        var addressPart = Clean(siteAddress);

        if (clientPart.Length > 20)
            clientPart = clientPart[..20].Trim('_');

        if (addressPart.Length > 24)
            addressPart = addressPart[..24].Trim('_');

        var baseName = $"{datePart}_{clientPart}_{addressPart}".ToUpperInvariant();
        var finalName = baseName;
        var counter = 2;

        while (await _db.EvacJobs.AnyAsync(j => j.JobName == finalName))
        {
            finalName = $"{baseName}_{counter}";
            counter++;
        }

        return finalName;
    }

    private static string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "NA";

        value = value.Trim();
        value = Regex.Replace(value, @"[^a-zA-Z0-9]+", "_");
        value = Regex.Replace(value, "_+", "_");
        return value.Trim('_');
    }
}
