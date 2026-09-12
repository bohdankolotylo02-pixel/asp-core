namespace ClientVehicles.Web.Models;

/// <summary>
/// A vehicle. Always belongs to exactly one client; a client can have many.
/// Like clients, vehicles are archived rather than deleted.
/// </summary>
public class Vehicle
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public Client Client { get; set; } = null!;

    /// <summary>Plate in display form, e.g. "12-AB-34".</summary>
    public string LicensePlate { get; set; } = string.Empty;

    /// <summary>Letters and digits only, e.g. "12AB34". Uniqueness and search run against this.</summary>
    public string LicensePlateKey { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? Vin { get; set; }

    /// <summary>Letters and digits only form of the VIN, null when no VIN was supplied.</summary>
    public string? VinKey { get; set; }

    public int? Mileage { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>
    /// True when this vehicle was archived as a side effect of archiving its client, rather than
    /// individually. Reactivating the client only brings back vehicles flagged this way, so a vehicle
    /// that was already archived on its own stays archived.
    /// </summary>
    public bool ArchivedWithClient { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }
}
