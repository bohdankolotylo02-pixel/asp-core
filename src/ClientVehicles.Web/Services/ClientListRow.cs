namespace ClientVehicles.Web.Services;

/// <summary>One row of the clients list, shaped by the query so the page does not load full graphs.</summary>
public class ClientListRow
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string? TaxNumber { get; init; }

    public bool IsArchived { get; init; }

    public int ActiveVehicleCount { get; init; }

    public int ArchivedVehicleCount { get; init; }

    /// <summary>Plates shown in the list, active vehicles first.</summary>
    public List<PlateChip> Plates { get; init; } = new();

    /// <summary>Plates of vehicles that matched the search term, so the user sees why a client was returned.</summary>
    public List<string> MatchedPlates { get; init; } = new();
}

/// <summary>A plate shown in the clients list, with enough state to style it.</summary>
public class PlateChip
{
    public string Plate { get; init; } = string.Empty;

    public bool IsArchived { get; init; }
}
