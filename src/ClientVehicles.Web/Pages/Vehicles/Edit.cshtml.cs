using ClientVehicles.Web.Models.Input;
using ClientVehicles.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientVehicles.Web.Pages.Vehicles;

public class EditModel : AppPageModel
{
    private readonly VehicleService _vehicles;

    public EditModel(VehicleService vehicles)
    {
        _vehicles = vehicles;
    }

    [BindProperty]
    public VehicleInput Input { get; set; } = new();

    public int VehicleId { get; private set; }

    public int ClientId { get; private set; }

    public string ClientName { get; private set; } = string.Empty;

    public string Plate { get; private set; } = string.Empty;

    public bool IsArchived { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var vehicle = await _vehicles.GetWithClientAsync(id, ct);
        if (vehicle is null)
        {
            return NotFound();
        }

        Load(vehicle);
        Input = VehicleInput.FromEntity(vehicle);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        var vehicle = await _vehicles.GetWithClientAsync(id, ct);
        if (vehicle is null)
        {
            return NotFound();
        }

        Load(vehicle);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _vehicles.UpdateAsync(id, Input, ct);
        if (!result.Succeeded)
        {
            CopyErrors(result);
            return Page();
        }

        FlashSuccess("Vehicle updated.");
        return RedirectToPage("Details", new { id });
    }

    private void Load(Models.Vehicle vehicle)
    {
        VehicleId = vehicle.Id;
        ClientId = vehicle.ClientId;
        ClientName = vehicle.Client.Name;
        Plate = vehicle.LicensePlate;
        IsArchived = vehicle.IsArchived;
    }
}
