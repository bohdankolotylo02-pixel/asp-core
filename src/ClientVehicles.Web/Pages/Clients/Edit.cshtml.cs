using ClientVehicles.Web.Models.Input;
using ClientVehicles.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientVehicles.Web.Pages.Clients;

public class EditModel : AppPageModel
{
    private readonly ClientService _clients;

    public EditModel(ClientService clients)
    {
        _clients = clients;
    }

    [BindProperty]
    public ClientInput Input { get; set; } = new();

    public int ClientId { get; private set; }

    public string ClientName { get; private set; } = string.Empty;

    public bool IsArchived { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var client = await _clients.GetAsync(id, ct);
        if (client is null)
        {
            return NotFound();
        }

        ClientId = client.Id;
        ClientName = client.Name;
        IsArchived = client.IsArchived;
        Input = ClientInput.FromEntity(client);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        var client = await _clients.GetAsync(id, ct);
        if (client is null)
        {
            return NotFound();
        }

        ClientId = client.Id;
        ClientName = client.Name;
        IsArchived = client.IsArchived;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _clients.UpdateAsync(id, Input, ct);
        if (!result.Succeeded)
        {
            CopyErrors(result);
            return Page();
        }

        FlashSuccess("Client updated.");
        return RedirectToPage("Details", new { id });
    }
}
