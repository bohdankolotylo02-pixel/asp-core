using ClientVehicles.Web.Models;
using ClientVehicles.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientVehicles.Web.Pages.Clients;

public class IndexModel : AppPageModel
{
    private readonly ClientService _clients;

    public IndexModel(ClientService clients)
    {
        _clients = clients;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "show")]
    public ArchiveFilter Filter { get; set; } = ArchiveFilter.Active;

    public IReadOnlyList<ClientListRow> Rows { get; private set; } = Array.Empty<ClientListRow>();

    public bool HasSearch => !string.IsNullOrWhiteSpace(Search);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Rows = await _clients.SearchAsync(Search, Filter, ct);
    }
}
