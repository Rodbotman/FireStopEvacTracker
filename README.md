# FireStop Evac Tracker

Simple ASP.NET Core Razor Pages website for tracking evacuation diagram jobs.

## Features

- Add evacuation diagram jobs
- Auto-generate job names
- Upload draft PDF
- Open / replace / remove PDF
- Track job status
- Search jobs by client, address, or job name
- PostgreSQL database
- FireStop branded layout

## Required Software

- Visual Studio 2022
- .NET 8 SDK
- PostgreSQL

## Setup

1. Open `FireStopEvacTracker.csproj` in Visual Studio.
2. Edit `appsettings.json` and change the PostgreSQL password:

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=firestop_evac_tracker;Username=postgres;Password=YOUR_PASSWORD_HERE"
```

3. Open **Package Manager Console** in Visual Studio.
4. Run:

```powershell
Add-Migration InitialCreate
Update-Database
```

5. Press **F5** to run.

## Notes

PDFs are stored in:

```text
wwwroot/uploads/
```

For a hosted website later, move PDF storage to a protected server folder or cloud storage.
