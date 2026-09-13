# Clients & Vehicles

Small client and vehicle management application: ASP.NET Core Razor Pages, EF Core, SQLite.

Clients own vehicles, both are archived instead of deleted, and one search box finds a client by
name, phone, NIF, license plate or VIN.

## Requirements

- .NET SDK 10.0 or newer (`dotnet --version`). Download: https://dotnet.microsoft.com/download
- No database server to install: SQLite is a file created on first run.

## Run it

```bash
cd src/ClientVehicles.Web
dotnet run
```

Then open the URL printed in the console (by default `https://localhost:7168` or `http://localhost:5168`).

On startup the app applies the EF Core migrations and, if the database is empty, inserts demo data
(9 clients, 12 vehicles, one archived client and one archived vehicle). The database file is
`src/ClientVehicles.Web/clientvehicles.db` and is not committed.

To start again from clean demo data, stop the app, delete `clientvehicles.db` and run it again.

## Run the tests

```bash
dotnet test
```

34 tests covering the business rules: relational integrity, plate uniqueness among active vehicles,
VIN uniqueness across all vehicles, ownership checks, archive and reactivate behaviour, atomic creation of client + first vehicle, text
normalisation and search. They run against a real SQLite database built from the migrations.

## What the app does

| Area | Behaviour |
| --- | --- |
| Clients | List, search, create, view, edit, archive, reactivate |
| Vehicles | Add to a client, view, edit, archive, reactivate |
| Combined flow | Create a client and their first vehicle in one form, saved in one transaction |
| Search | One field: client name, phone, NIF, license plate, VIN |
| Filters | Active (default), Archived, All |

## Project layout

```
src/ClientVehicles.Web
  Common/          text normalisation, result type
  Data/            DbContext, migrations, demo data seeder
  Models/          entities and form input models
  Pages/           Razor Pages (Clients, Vehicles, shared layout)
  Services/        ClientService, VehicleService - the business rules
  wwwroot/         one stylesheet, one small script
tests/ClientVehicles.Tests
```

## Database commands

The EF tool is pinned as a local tool, so no global install is needed:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/ClientVehicles.Web --output-dir Data/Migrations
dotnet ef database update --project src/ClientVehicles.Web
```

See `HANDOVER.md` for the design decisions, the points that were open in the specification and how
they were resolved, the testing that was done, and the known limitations.
