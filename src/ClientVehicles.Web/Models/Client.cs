namespace ClientVehicles.Web.Models;

/// <summary>
/// A customer of the workshop. Clients are never physically deleted, only archived.
/// </summary>
public class Client
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Phone as typed by the user, used for display.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Digits only, used so a search for "912345678" also matches "+351 912 345 678".</summary>
    public string PhoneKey { get; set; } = string.Empty;

    public string? Address { get; set; }

    /// <summary>NIF / tax number.</summary>
    public string? TaxNumber { get; set; }

    /// <summary>Lower-cased, accent-stripped name used for search.</summary>
    public string NameKey { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }

    public List<Vehicle> Vehicles { get; set; } = new();
}
