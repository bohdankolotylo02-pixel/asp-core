using ClientVehicles.Web.Models.Input;
using ClientVehicles.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientVehicles.Web.Pages.Vehicles;

public class CreateModel : AppPageModel
{
    private readonly ClientService _clients;
    private readonly VehicleService _vehicles;

    public CreateModel(ClientService clients, VehicleService vehicles)
    {
        _clients = clients;
        _vehicles = vehicles;
    }

    [BindProperty]
    public VehicleInput Input { get; set; } = new();

    public int ClientId { get; private set; }

    public string ClientName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int clientId, CancellationToken ct)
    {
        var client = await _clients.GetAsync(clientId, ct);
        if (client is null)
        {
            return NotFound();
        }

        if (client.IsArchived)
        {
            FlashError("This client is archived. Reactivate the client before adding a vehicle.");
            return RedirectToPage("/Clients/Details", new { id = clientId });
        }

        ClientId = client.Id;
        ClientName = client.Name;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int clientId, CancellationToken ct)
    {
        var client = await _clients.GetAsync(clientId, ct);
        if (client is null)
        {
            return NotFound();
        }

        ClientId = client.Id;
        ClientName = client.Name;

        // Same guard as the GET: if the client was archived in another tab while this form was
        // open, say so instead of showing field errors for a form that can never be saved.
        if (client.IsArchived)
        {
            FlashError("This client is archived. Reactivate the client before adding a vehicle.");
            return RedirectToPage("/Clients/Details", new { id = clientId });
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _vehicles.CreateAsync(clientId, Input, ct);
        if (!result.Succeeded)
        {
            CopyErrors(result);
            return Page();
        }

        FlashSuccess("Vehicle added.");
        return RedirectToPage("/Clients/Details", new { id = clientId });
    }
}
