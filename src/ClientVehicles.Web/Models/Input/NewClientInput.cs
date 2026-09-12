namespace ClientVehicles.Web.Models.Input;

/// <summary>
/// The combined "new client + optional first vehicle" form. Both halves are saved in a single
/// transaction: either the client and the vehicle are both created, or nothing is.
/// </summary>
public class NewClientInput
{
    public ClientInput Client { get; set; } = new();

    public VehicleInput Vehicle { get; set; } = new();

    /// <summary>Set by the checkbox that reveals the vehicle block.</summary>
    public bool AddVehicle { get; set; }
}
