using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClientVehicles.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    public int? ErrorCode { get; private set; }

    public string Title { get; private set; } = "Something went wrong";

    public string Message { get; private set; } = "The page could not be shown. Please go back to the clients list and try again.";

    public void OnGet(int? code)
    {
        ErrorCode = code;

        if (code == 404)
        {
            Title = "Not found";
            Message = "That record does not exist. It may have been opened from an old link.";
        }
    }
}
