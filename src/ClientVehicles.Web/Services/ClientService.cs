using ClientVehicles.Web.Common;
using ClientVehicles.Web.Data;
using ClientVehicles.Web.Models;
using ClientVehicles.Web.Models.Input;
using Microsoft.EntityFrameworkCore;

namespace ClientVehicles.Web.Services;

public class ClientService
{
    private readonly AppDbContext _db;
    private readonly VehicleService _vehicles;
    private readonly TimeProvider _clock;

    public ClientService(AppDbContext db, VehicleService vehicles, TimeProvider clock)
    {
        _db = db;
        _vehicles = vehicles;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ClientListRow>> SearchAsync(string? search, ArchiveFilter filter, CancellationToken ct = default)
    {
        var nameKey = TextKeys.SearchKey(search);
        var digits = TextKeys.DigitsKey(search);
        var alphaNumeric = TextKeys.AlphaNumericKey(search);
        var hasDigits = digits.Length > 0;
        var hasAlphaNumeric = alphaNumeric.Length > 0;

        var query = _db.Clients.AsNoTracking();

        query = filter switch
        {
            ArchiveFilter.Active => query.Where(c => !c.IsArchived),
            ArchiveFilter.Archived => query.Where(c => c.IsArchived),
            _ => query
        };

        if (nameKey.Length > 0)
        {
            // One box searches client name, phone, NIF, plate and VIN. Comparisons run against the
            // normalised key columns, so punctuation and accents typed in the search box do not matter.
            query = query.Where(c =>
                c.NameKey.Contains(nameKey)
                || (hasDigits && c.PhoneKey.Contains(digits))
                || (hasDigits && c.TaxNumber != null && c.TaxNumber.Contains(digits))
                || (hasAlphaNumeric && c.Vehicles.Any(v =>
                        v.LicensePlateKey.Contains(alphaNumeric)
                        || (v.VinKey != null && v.VinKey.Contains(alphaNumeric)))));
        }

        return await query
            .OrderBy(c => c.IsArchived)
            .ThenBy(c => c.NameKey)
            .Select(c => new ClientListRow
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                TaxNumber = c.TaxNumber,
                IsArchived = c.IsArchived,
                ActiveVehicleCount = c.Vehicles.Count(v => !v.IsArchived),
                ArchivedVehicleCount = c.Vehicles.Count(v => v.IsArchived),
                Plates = c.Vehicles
                    .OrderBy(v => v.IsArchived)
                    .ThenBy(v => v.LicensePlate)
                    .Select(v => new PlateChip { Plate = v.LicensePlate, IsArchived = v.IsArchived })
                    .Take(4)
                    .ToList(),
                MatchedPlates = hasAlphaNumeric
                    ? c.Vehicles
                        .Where(v => v.LicensePlateKey.Contains(alphaNumeric)
                                    || (v.VinKey != null && v.VinKey.Contains(alphaNumeric)))
                        .Select(v => v.LicensePlate)
                        .Take(4)
                        .ToList()
                    : new List<string>()
            })
            .ToListAsync(ct);
    }

    /// <summary>Client with its vehicles, active ones first. Null when the id does not exist.</summary>
    public Task<Client?> GetWithVehiclesAsync(int id, CancellationToken ct = default) =>
        _db.Clients
            .AsNoTracking()
            .Include(c => c.Vehicles.OrderBy(v => v.IsArchived).ThenBy(v => v.LicensePlate))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Client?> GetAsync(int id, CancellationToken ct = default) =>
        _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <summary>
    /// Creates a client and, optionally, their first vehicle. Both rows are written by a single
    /// SaveChanges call, which EF runs inside one transaction: if the vehicle insert fails,
    /// the client is not created either.
    /// </summary>
    public async Task<OperationResult> CreateAsync(NewClientInput input, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var client = new Client
        {
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        ApplyInput(client, input.Client);

        if (input.AddVehicle)
        {
            var check = await _vehicles.ValidateForSaveAsync(input.Vehicle, vehicleIdToIgnore: null, prefix: "Vehicle.", ct);
            if (!check.Succeeded)
            {
                return check;
            }

            var vehicle = new Vehicle { CreatedAtUtc = now, UpdatedAtUtc = now };
            VehicleService.ApplyInput(vehicle, input.Vehicle);
            client.Vehicles.Add(vehicle);
        }

        _db.Clients.Add(client);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (VehicleService.IsUniqueViolation(ex))
        {
            // Safety net for the gap between the check above and the insert (double submit, or two
            // people saving at the same time). The partial unique indexes are the real guarantee.
            _db.ChangeTracker.Clear();
            return OperationResult.Failure("Vehicle.LicensePlate",
                "That license plate or VIN was just registered by another active vehicle. Nothing was saved.");
        }

        return OperationResult.Success(client.Id);
    }

    public async Task<OperationResult> UpdateAsync(int id, ClientInput input, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null)
        {
            return OperationResult.Failure(string.Empty, "That client no longer exists.");
        }

        ApplyInput(client, input);
        client.UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime;

        // Vehicle rows are never touched here, so editing a client cannot break its associations.
        await _db.SaveChangesAsync(ct);
        return OperationResult.Success(client.Id);
    }

    /// <summary>
    /// Archives a client together with every vehicle of theirs that is still active. Those vehicles are
    /// flagged as archived-with-client so reactivating the client brings back exactly those, and nothing
    /// is ever deleted.
    /// </summary>
    public async Task<OperationResult> ArchiveAsync(int id, CancellationToken ct = default)
    {
        var client = await _db.Clients.Include(c => c.Vehicles).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null)
        {
            return OperationResult.Failure(string.Empty, "That client no longer exists.");
        }

        if (client.IsArchived)
        {
            return OperationResult.Success(client.Id).AddWarning($"{client.Name} was already archived.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        client.IsArchived = true;
        client.ArchivedAtUtc = now;
        client.UpdatedAtUtc = now;

        var archivedVehicles = 0;
        foreach (var vehicle in client.Vehicles.Where(v => !v.IsArchived))
        {
            vehicle.IsArchived = true;
            vehicle.ArchivedWithClient = true;
            vehicle.ArchivedAtUtc = now;
            vehicle.UpdatedAtUtc = now;
            archivedVehicles++;
        }

        await _db.SaveChangesAsync(ct);

        var result = OperationResult.Success(client.Id);
        if (archivedVehicles > 0)
        {
            result.AddWarning($"{archivedVehicles} vehicle(s) were archived together with this client.");
        }

        return result;
    }

    /// <summary>
    /// Reactivates a client and the vehicles that were archived together with them. A vehicle whose plate
    /// or VIN has been taken by another active vehicle in the meantime stays archived and is reported back,
    /// instead of silently breaking the uniqueness rule.
    /// </summary>
    public async Task<OperationResult> ReactivateAsync(int id, CancellationToken ct = default)
    {
        var client = await _db.Clients.Include(c => c.Vehicles).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null)
        {
            return OperationResult.Failure(string.Empty, "That client no longer exists.");
        }

        if (!client.IsArchived)
        {
            return OperationResult.Success(client.Id).AddWarning($"{client.Name} is already active.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        client.IsArchived = false;
        client.ArchivedAtUtc = null;
        client.UpdatedAtUtc = now;

        var result = OperationResult.Success(client.Id);

        foreach (var vehicle in client.Vehicles.Where(v => v.IsArchived && v.ArchivedWithClient).ToList())
        {
            var conflict = await _vehicles.FindActiveConflictAsync(vehicle.LicensePlateKey, vehicle.VinKey, vehicle.Id, ct);
            if (conflict is not null)
            {
                result.AddWarning(
                    $"Vehicle {vehicle.LicensePlate} stayed archived because {conflict} is already used by another active vehicle.");
                continue;
            }

            vehicle.IsArchived = false;
            vehicle.ArchivedWithClient = false;
            vehicle.ArchivedAtUtc = null;
            vehicle.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    private static void ApplyInput(Client client, ClientInput input)
    {
        client.Name = TextKeys.CleanRequired(input.Name);
        client.NameKey = TextKeys.SearchKey(client.Name);
        client.Phone = TextKeys.CleanRequired(input.Phone);
        client.PhoneKey = TextKeys.DigitsKey(client.Phone);
        client.Address = TextKeys.Clean(input.Address);
        client.TaxNumber = TextKeys.Clean(input.TaxNumber);
    }
}
