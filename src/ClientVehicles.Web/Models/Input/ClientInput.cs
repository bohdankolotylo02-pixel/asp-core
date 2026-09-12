using System.ComponentModel.DataAnnotations;

namespace ClientVehicles.Web.Models.Input;

/// <summary>
/// What a form is allowed to post for a client. Kept separate from the entity so that
/// computed columns (keys, timestamps, archive state) can never be set from the browser.
/// </summary>
public class ClientInput
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, ErrorMessage = "Name cannot be longer than 120 characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [StringLength(40, ErrorMessage = "Phone cannot be longer than 40 characters.")]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Address cannot be longer than 200 characters.")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(30, ErrorMessage = "NIF / tax number cannot be longer than 30 characters.")]
    [Display(Name = "NIF / Tax number")]
    public string? TaxNumber { get; set; }

    public static ClientInput FromEntity(Client client) => new()
    {
        Name = client.Name,
        Phone = client.Phone,
        Address = client.Address,
        TaxNumber = client.TaxNumber
    };
}
