using ClientVehicles.Web.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClientVehicles.Web.Pages;

/// <summary>
/// Small shared base for the pages: flash messages that survive the redirect after a POST,
/// and one place that maps service errors onto ModelState.
/// </summary>
public abstract class AppPageModel : PageModel
{
    protected void FlashSuccess(string message) => TempData["FlashSuccess"] = message;

    protected void FlashError(string message) => TempData["FlashError"] = message;

    protected void FlashWarnings(IReadOnlyList<string> warnings)
    {
        if (warnings.Count > 0)
        {
            TempData["FlashWarnings"] = warnings.ToArray();
        }
    }

    /// <summary>
    /// Copies service errors into ModelState. <paramref name="modelPrefix"/> is the name of the
    /// bound property ("Input."), so field errors land on the right input and show inline.
    /// </summary>
    protected void CopyErrors(OperationResult result, string modelPrefix = "Input.")
    {
        foreach (var (key, message) in result.Errors)
        {
            ModelState.AddModelError(key.Length == 0 ? string.Empty : modelPrefix + key, message);
        }
    }
}
