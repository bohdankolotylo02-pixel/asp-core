using ClientVehicles.Web.Common;
using ClientVehicles.Web.Data;
using ClientVehicles.Web.Models;
using ClientVehicles.Web.Models.Input;
using Microsoft.EntityFrameworkCore;

namespace ClientVehicles.Web.Services;

public class VehicleService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public VehicleService(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task<Vehicle?> GetWithClientAsync(int id, CancellationToken ct = default) =>
        _db.Vehicles.AsNoTracking().Include(v => v.Client).FirstOrDefaultAsync(v => v.Id == id, ct);

    /// <summary>Adds a vehicle to an existing, active client.</summary>
    public async Task<OperationResult> CreateAsync(int clientId, VehicleInput input, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct);
        if (client is null)
        {
            return OperationResult.Failure(string.Empty, "That client no longer exists.");
        }

        if (client.IsArchived)
        {
            return OperationResult.Failure(string.Empty,
                "This client is archived. Reactivate the client before adding a vehicle.");
        }

        var check = await ValidateForSaveAsync(input, vehicleIdToIgnore: null, prefix: string.Empty, ct);
        if (!check.Succeeded)
        {
            return check;
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var vehicle = new Vehicle
        {
            ClientId = client.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        ApplyInput(vehicle, input);
        _db.Vehicles.Add(vehicle);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _db.ChangeTracker.Clear();
            return OperationResult.Failure("LicensePlate",
                "That license plate or VIN was just registered by another active vehicle. Nothing was saved.");
        }

        return OperationResult.Success(vehicle.Id);
    }

    /// <summary>
    /// Updates a vehicle in place. The owning client is deliberately not changeable here, so an edit
    /// can never move a vehicle to the wrong client or create a second copy of it.
    /// </summary>
    public async Task<OperationResult> UpdateAsync(int id, VehicleInput input, CancellationToken ct = default)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null)
        {
            return OperationResult.Failure(string.Empty, "That vehicle no longer exists.");
        }

        // Uniqueness is a rule about active vehicles, so it is only enforced while the record is active.
        if (!vehicle.IsArchived)
        {
            var check = await ValidateForSaveAsync(input, vehicleIdToIgnore: vehicle.Id, prefix: string.Empty, ct);
            if (!check.Succeeded)
            {
                return check;
            }
        }

        ApplyInput(vehicle, input);
        vehicle.UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _db.ChangeTracker.Clear();
            return OperationResult.Failure("LicensePlate",
                "That license plate or VIN is already used by another active vehicle. Nothing was saved.");
        }

        return OperationResult.Success(vehicle.Id);
    }

    public async Task<OperationResult> ArchiveAsync(int id, CancellationToken ct = default)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null)
        {
            return OperationResult.Failure(string.Empty, "That vehicle no longer exists.");
        }

        if (vehicle.IsArchived)
        {
            return OperationResult.Success(vehicle.Id).AddWarning($"{vehicle.LicensePlate} was already archived.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        vehicle.IsArchived = true;
        // Archived on its own, so reactivating the client later must not bring it back.
        vehicle.ArchivedWithClient = false;
        vehicle.ArchivedAtUtc = now;
        vehicle.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        return OperationResult.Success(vehicle.Id);
    }

    public async Task<OperationResult> ReactivateAsync(int id, CancellationToken ct = default)
    {
        var vehicle = await _db.Vehicles.Include(v => v.Client).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null)
        {
            return OperationResult.Failure(string.Empty, "That vehicle no longer exists.");
        }

        if (!vehicle.IsArchived)
        {
            return OperationResult.Success(vehicle.Id).AddWarning($"{vehicle.LicensePlate} is already active.");
        }

        if (vehicle.Client.IsArchived)
        {
            return OperationResult.Failure(string.Empty,
                "The owner of this vehicle is archived. Reactivate the client first.");
        }

        var conflict = await FindActiveConflictAsync(vehicle.LicensePlateKey, vehicle.VinKey, vehicle.Id, ct);
        if (conflict is not null)
        {
            return OperationResult.Failure(string.Empty,
                $"This vehicle cannot be reactivated: {conflict} is already used by another active vehicle.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        vehicle.IsArchived = false;
        vehicle.ArchivedWithClient = false;
        vehicle.ArchivedAtUtc = null;
        vehicle.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(ct);
        return OperationResult.Success(vehicle.Id);
    }

    /// <summary>
    /// Business rules that the data annotations on the input model cannot express, because they
    /// need the database: plate and VIN must be unique among active vehicles.
    /// The <paramref name="prefix"/> keeps the error keys aligned with the combined create form.
    /// </summary>
    internal async Task<OperationResult> ValidateForSaveAsync(
        VehicleInput input,
        int? vehicleIdToIgnore,
        string prefix,
        CancellationToken ct = default)
    {
        var result = OperationResult.Success();
        var plateKey = TextKeys.AlphaNumericKey(input.LicensePlate);
        var vinKey = TextKeys.AlphaNumericKey(input.Vin);
        vinKey = vinKey.Length == 0 ? null : vinKey;

        if (plateKey.Length == 0)
        {
            result.AddError($"{prefix}LicensePlate", "License plate is required.");
        }
        else
        {
            var plateTaken = await _db.Vehicles.AnyAsync(
                v => !v.IsArchived && v.LicensePlateKey == plateKey && (vehicleIdToIgnore == null || v.Id != vehicleIdToIgnore),
                ct);

            if (plateTaken)
            {
                result.AddError($"{prefix}LicensePlate", "Another active vehicle already uses this license plate.");
            }
        }

        if (vinKey is not null)
        {
            var vinTaken = await _db.Vehicles.AnyAsync(
                v => !v.IsArchived && v.VinKey == vinKey && (vehicleIdToIgnore == null || v.Id != vehicleIdToIgnore),
                ct);

            if (vinTaken)
            {
                result.AddError($"{prefix}Vin", "Another active vehicle already uses this VIN.");
            }
        }

        if (input.Mileage is < 0)
        {
            result.AddError($"{prefix}Mileage", "Mileage cannot be negative.");
        }

        return result;
    }

    /// <summary>Returns a description of what blocks a vehicle from being active, or null when nothing does.</summary>
    internal async Task<string?> FindActiveConflictAsync(string plateKey, string? vinKey, int vehicleId, CancellationToken ct = default)
    {
        var plateTaken = await _db.Vehicles.AnyAsync(
            v => !v.IsArchived && v.Id != vehicleId && v.LicensePlateKey == plateKey, ct);

        if (plateTaken)
        {
            return "the license plate";
        }

        if (vinKey is not null)
        {
            var vinTaken = await _db.Vehicles.AnyAsync(
                v => !v.IsArchived && v.Id != vehicleId && v.VinKey == vinKey, ct);

            if (vinTaken)
            {
                return "the VIN";
            }
        }

        return null;
    }

    internal static void ApplyInput(Vehicle vehicle, VehicleInput input)
    {
        vehicle.LicensePlate = TextKeys.UpperDisplay(input.LicensePlate);
        vehicle.LicensePlateKey = TextKeys.AlphaNumericKey(input.LicensePlate);
        vehicle.Brand = TextKeys.CleanRequired(input.Brand);
        vehicle.Model = TextKeys.CleanRequired(input.Model);

        var vin = TextKeys.UpperDisplay(input.Vin);
        vehicle.Vin = vin.Length == 0 ? null : vin;
        var vinKey = TextKeys.AlphaNumericKey(input.Vin);
        vehicle.VinKey = vinKey.Length == 0 ? null : vinKey;

        vehicle.Mileage = input.Mileage;
    }

    /// <summary>True when the database rejected the write because of a unique index.</summary>
    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
}
