using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClientVehicles.Web.Pages;

/// <summary>The app has a single entry point, so the root sends the user to the clients list.</summary>
public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Clients/Index");
}
