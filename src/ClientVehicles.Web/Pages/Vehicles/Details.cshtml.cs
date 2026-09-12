using ClientVehicles.Web.Common;
using ClientVehicles.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientVehicles.Web.Pages.Vehicles;

public class DetailsModel : AppPageModel
{
    private readonly VehicleService _vehicles;

    public DetailsModel(VehicleService vehicles)
    {
        _vehicles = vehicles;
    }

    public Models.Vehicle Vehicle { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var vehicle = await _vehicles.GetWithClientAsync(id, ct);
        if (vehicle is null)
        {
            return NotFound();
        }

        Vehicle = vehicle;
        return Page();
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id, CancellationToken ct)
    {
        var result = await _vehicles.ArchiveAsync(id, ct);
        return Finish(result, id, "Vehicle archived.");
    }

    public async Task<IActionResult> OnPostReactivateAsync(int id, CancellationToken ct)
    {
        var result = await _vehicles.ReactivateAsync(id, ct);
        return Finish(result, id, "Vehicle reactivated.");
    }

    private IActionResult Finish(OperationResult result, int vehicleId, string successMessage)
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
        return RedirectToPage("Details", new { id = vehicleId });
    }
}
