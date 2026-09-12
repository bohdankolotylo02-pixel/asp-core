# Handover notes

Clients & Vehicles assessment. ASP.NET Core Razor Pages, EF Core, SQLite, migrations, seeded demo
data. This document covers the structure, the decisions I made, what I interpreted, what I tested
and what is deliberately not there.

---

## 1. Structure

```
src/ClientVehicles.Web
  Common/     TextKeys (normalisation), OperationResult (service result)
  Data/       AppDbContext, Migrations, DbSeeder
  Models/     Client, Vehicle entities + Input/ form models
  Pages/      Razor Pages: Clients (Index, Create, Details, Edit), Vehicles (Create, Details, Edit)
  Services/   ClientService, VehicleService
  wwwroot/    site.css, site.js
tests/ClientVehicles.Tests
```

The app is small, so the layering is deliberately shallow: **page → service → DbContext**. No
repository layer, no MediatR, no AutoMapper, no generic base classes. Two things were still worth
separating:

- **Services hold the business rules** (uniqueness, archive cascade, reactivation conflicts). They
  are independent of the web stack, which is what makes them straightforward to test.
- **Input models are separate from entities.** Forms bind to `ClientInput` / `VehicleInput`, never to
  the entity. That is what prevents a crafted POST from setting `IsArchived`, `Id`, timestamps or the
  normalised key columns, and it keeps the display annotations out of the persistence model.

Everything else lives in the page models, which are thin: validate, call a service, redirect.

## 2. Data model

**Client**: Name, Phone, Address?, TaxNumber (NIF)?, IsArchived, timestamps
**Vehicle**: ClientId, LicensePlate, Brand, Model, Vin?, Mileage?, IsArchived, ArchivedWithClient, timestamps

Each entity also stores normalised "key" columns next to the values the user typed:

| Column | Purpose |
| --- | --- |
| `Client.NameKey` | lower case, accents stripped - name search |
| `Client.PhoneKey` | digits only - so `912345678` finds `+351 912 345 678` |
| `Vehicle.LicensePlateKey` | letters and digits only, upper case - duplicate check and search |
| `Vehicle.VinKey` | same treatment for the VIN |

This is the core design decision of the app: the user sees exactly what they typed, while comparisons
happen on a canonical form. It is also why search is predictable: `12-AB-34`, `12 ab 34` and `12ab34`
all find the same vehicle.

### Constraints in the database, not only in C#

- Foreign key `Vehicle.ClientId` is required and configured `OnDelete(Restrict)`, so a client with
  vehicles can never be deleted out from under them, even from outside the app.
- Unique **partial** index on `LicensePlateKey` where `IsArchived = 0`, and the same for `VinKey`
  (where it is not null). This is what makes "two active vehicles cannot share a plate" a guarantee
  rather than a hope: it also holds against double submits and concurrent users. Archived vehicles
  are outside the index, which is what frees a plate for reuse.
- Check constraints: `Mileage IS NULL OR Mileage >= 0`, and non-empty required text on both tables.
- `NOT NULL` and max lengths on every required column.

## 3. Decisions worth knowing

**Archiving a client archives their vehicles.** Otherwise an archived client's plate would keep
blocking a new active vehicle, which is the wrong behaviour for a workshop. Those vehicles are
flagged `ArchivedWithClient`, so reactivating the client brings back exactly those and leaves a
vehicle that had been archived on its own (sold earlier) archived.

**Reactivation can be partial, and says so.** If a plate or VIN was taken by another active vehicle
while the client was archived, the client is reactivated and that vehicle stays archived, with a
message naming it. The alternative would be to fail the whole operation or to break the uniqueness
rule; neither is useful to the person at the desk.

**The combined create is one transaction.** Client and first vehicle are inserted by a single
`SaveChanges`, which EF wraps in one transaction. If the vehicle is rejected, no client row is
created. There is no half-saved state to clean up.

**Double submits.** Every POST redirects afterwards (PRG), so a refresh never repeats an action. The
submit button disables itself on submit. Underneath both, the partial unique indexes mean a duplicate
insert fails at the database and is reported as a normal validation message rather than a stack trace.

**Validation runs in three places, on purpose**: data annotations on the input models (required,
lengths, mileage range), service checks for the rules that need the database (duplicate plate, duplicate
VIN), and the database constraints above as the last line. The small amount of JavaScript only mirrors
the first layer for faster feedback.

**A vehicle keeps its owner.** The edit form shows the owner read-only. The specification lists a
vehicle as belonging to one client and warns against edits breaking associations, so moving a vehicle
between clients is not exposed. It is a small change if you want it (a dropdown plus one line in
`VehicleService.UpdateAsync`).

**A vehicle cannot be added to, or reactivated under, an archived client.** The UI hides the action and
both the page and the service refuse it, so a stale tab cannot create an active vehicle under an
archived client.

**No CSS framework.** About ten screens did not justify pulling in Bootstrap, so the UI is one
stylesheet of design tokens and small components. No jQuery either: confirmations, the optional
vehicle block and the double submit guard are around 150 lines of plain JavaScript. Confirmations use
an in-app modal, never `confirm()`.

**Migrations are applied at startup** so that `dotnet run` is all you need. For a real deployment I
would move this to an explicit release step and leave the app read-only at startup; the seeder already
no-ops when the database has data.

## 4. Points that were open, and how I resolved them

| Open point | Decision |
| --- | --- |
| Visual design | Compact business look: 14px base, dense tables, one accent colour, no large cards |
| Page structure | Clients list is the home page; vehicles live under their client; no separate vehicles list, since the search already finds a client by plate or VIN |
| CSS approach | One hand written stylesheet with CSS variables, no framework |
| Combined client + vehicle flow | One form with an "Add the first vehicle now" checkbox that reveals the vehicle block. One page, one POST, one transaction. The block's required rules switch off when it is hidden |
| Archived records | Not shown by default. A segmented Active / Archived / All filter on the list, archived vehicles shown in a client's table with a badge and muted plate |
| Plate normalisation | Stored upper case as typed (`12-ab-34` → `12-AB-34`); duplicates compared on letters and digits only |
| Phone normalisation | Stored as typed, digits only copy used for search. No country specific formatting, since the format is a business decision |
| VIN normalisation | Same as plates: upper case for display, letters and digits for comparison. Empty VIN is stored as NULL, so many vehicles without a VIN are fine |
| Search scope | Name, phone, NIF, plate, VIN. NIF was not in the list but belongs in the same box for an SME workshop |
| Uniqueness of VIN | Treated like the plate: unique among **active** vehicles only |
| Client archive cascade | Archives the client's active vehicles (see above) |

If any of these should behave differently, they are all one small change each.

## 5. Testing I ran

**Automated: 28 tests, all passing** (`dotnet test`). They run against a real SQLite database created
from the migrations, not the EF in-memory provider, because the in-memory provider ignores unique
indexes and check constraints, which are exactly the guarantees being tested. Coverage:

- client created with and without a first vehicle, association correct
- combined create rejected by a duplicate plate leaves **no** client behind
- duplicate plate, duplicate VIN, case and punctuation variants all rejected
- empty VIN is not treated as a duplicate
- negative mileage rejected by the service **and** by the database when the service is bypassed
- duplicate active plate rejected by the database when the service is bypassed
- archiving a vehicle frees its plate for reuse
- archiving a client archives its vehicles and deletes nothing
- reactivating a client restores only the vehicles archived with it
- reactivating leaves a vehicle archived when its plate was taken, with a warning
- vehicle cannot be reactivated while its client is archived, or added to an archived client
- editing a client keeps vehicle associations; editing a vehicle updates one row and cannot take another active plate
- search by name, phone, plate, VIN, accents, and the archived filter
- whitespace trimmed, empty optional fields stored as NULL

**Functional pass: 75 checks against the running app, all passing.** Driven over HTTP against the real
pages (forms, antiforgery tokens, redirects), covering the normal workflows you listed and the edge
cases: create client, add vehicle, combined create, edits persisted, several vehicles on one client,
search by every supported field, archive and reactivate at both levels, plate reuse after archiving,
negative mileage, duplicate plate, duplicate VIN, missing required fields, non-numeric mileage,
unknown and non-numeric ids, and a POST without an antiforgery token.

**Also checked by hand:** data survives stopping and restarting the app, the seeder does not duplicate
data on a restart, layout at 1280px, 820px and 430px, the optional vehicle block reveal and its
required-field switching, and the confirmation modal markup.

Two issues were found and fixed during this pass: posting a vehicle to an archived client showed field
errors instead of a clear message, and the row action buttons wrapped onto two lines in the vehicles
table.

## 6. Known limitations

- **Search is a `LIKE` scan.** Fine for thousands of records on SQLite; for hundreds of thousands, or
  on PostgreSQL, this wants a trigram index or full text search.
- **No pagination.** The clients list returns everything that matches. With the expected size of an SME
  client base this is the simpler and faster option, but it is the first thing to add if the list grows.
- **No authentication, roles or audit trail**, per the scope. `CreatedAtUtc` / `UpdatedAtUtc` are stored
  but not shown; they are the foundation for an audit trail later.
- **Optimistic concurrency is not implemented.** Two people editing the same client at the same time,
  last write wins. Uniqueness is still safe, since the database enforces it. A `RowVersion` column would
  be the fix.
- **Client side validation is intentionally minimal** (required fields and mileage). The server always
  re-validates and is the source of truth.
- **SQLite specifics:** the partial index filters and check constraints are written in SQL that also works
  on PostgreSQL, but moving providers still needs a fresh migration and a test pass.
- The UI is in English only.
