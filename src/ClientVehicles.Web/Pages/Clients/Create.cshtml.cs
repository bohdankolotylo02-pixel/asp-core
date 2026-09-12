using ClientVehicles.Web.Models.Input;
using ClientVehicles.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientVehicles.Web.Pages.Clients;

public class CreateModel : AppPageModel
{
    private readonly ClientService _clients;

    public CreateModel(ClientService clients)
    {
        _clients = clients;
    }

    [BindProperty]
    public NewClientInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // The vehicle block is optional. When it is switched off its fields must not be validated,
        // otherwise an untouched block would block the form.
        if (!Input.AddVehicle)
        {
            Input.Vehicle = new VehicleInput();
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Input.Vehicle.", StringComparison.Ordinal)).ToList())
            {
                ModelState.Remove(key);
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _clients.CreateAsync(Input, ct);
        if (!result.Succeeded)
        {
            CopyErrors(result);
            return Page();
        }

        FlashSuccess(Input.AddVehicle
            ? "Client and vehicle created."
            : "Client created.");

        // Redirect after POST, so a refresh cannot create the same client twice.
        return RedirectToPage("Details", new { id = result.EntityId });
    }
}
