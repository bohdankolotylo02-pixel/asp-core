using ClientVehicles.Web.Models;
using ClientVehicles.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientVehicles.Web.Pages.Clients;

public class DetailsModel : AppPageModel
{
    private readonly ClientService _clients;
    private readonly VehicleService _vehicles;

    public DetailsModel(ClientService clients, VehicleService vehicles)
    {
        _clients = clients;
        _vehicles = vehicles;
    }

    public Client Client { get; private set; } = null!;

    public IReadOnlyList<Vehicle> ActiveVehicles { get; private set; } = Array.Empty<Vehicle>();

    public IReadOnlyList<Vehicle> ArchivedVehicles { get; private set; } = Array.Empty<Vehicle>();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var client = await _clients.GetWithVehiclesAsync(id, ct);
        if (client is null)
        {
            return NotFound();
        }

        Client = client;
        ActiveVehicles = client.Vehicles.Where(v => !v.IsArchived).ToList();
        ArchivedVehicles = client.Vehicles.Where(v => v.IsArchived).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id, CancellationToken ct)
    {
        var result = await _clients.ArchiveAsync(id, ct);
        return Finish(result, id, "Client archived.");
    }

    public async Task<IActionResult> OnPostReactivateAsync(int id, CancellationToken ct)
    {
        var result = await _clients.ReactivateAsync(id, ct);
        return Finish(result, id, "Client reactivated.");
    }

    public async Task<IActionResult> OnPostArchiveVehicleAsync(int id, int vehicleId, CancellationToken ct)
    {
        // The route carries both ids, so the service is told which client the vehicle must belong to.
        var result = await _vehicles.ArchiveAsync(vehicleId, requiredClientId: id, ct: ct);
        return Finish(result, id, "Vehicle archived.");
    }

    public async Task<IActionResult> OnPostReactivateVehicleAsync(int id, int vehicleId, CancellationToken ct)
    {
        var result = await _vehicles.ReactivateAsync(vehicleId, requiredClientId: id, ct: ct);
        return Finish(result, id, "Vehicle reactivated.");
    }

    private IActionResult Finish(Common.OperationResult result, int clientId, string successMessage)
    {
        if (result.Succeeded)
        {
            FlashSuccess(successMessage);
        }
        else
        {
            FlashError(result.Errors[0].Message);
        }

        FlashWarnings(result.Warnings);

        // Redirect after POST: refreshing the page cannot repeat the action.
        return RedirectToPage("Details", new { id = clientId });
    }
}
