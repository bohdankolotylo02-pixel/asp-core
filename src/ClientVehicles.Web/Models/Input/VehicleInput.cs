using System.ComponentModel.DataAnnotations;

namespace ClientVehicles.Web.Models.Input;

/// <summary>What a form is allowed to post for a vehicle.</summary>
public class VehicleInput
{
    [Required(ErrorMessage = "License plate is required.")]
    [StringLength(20, ErrorMessage = "License plate cannot be longer than 20 characters.")]
    [Display(Name = "License plate")]
    public string LicensePlate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Brand is required.")]
    [StringLength(60, ErrorMessage = "Brand cannot be longer than 60 characters.")]
    [Display(Name = "Brand")]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Model is required.")]
    [StringLength(60, ErrorMessage = "Model cannot be longer than 60 characters.")]
    [Display(Name = "Model")]
    public string Model { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "VIN cannot be longer than 30 characters.")]
    [Display(Name = "VIN")]
    public string? Vin { get; set; }

    [Range(0, 5_000_000, ErrorMessage = "Mileage must be between 0 and 5,000,000.")]
    [Display(Name = "Current mileage (km)")]
    public int? Mileage { get; set; }

    public static VehicleInput FromEntity(Vehicle vehicle) => new()
    {
        LicensePlate = vehicle.LicensePlate,
        Brand = vehicle.Brand,
        Model = vehicle.Model,
        Vin = vehicle.Vin,
        Mileage = vehicle.Mileage
    };

    /// <summary>True when the user did not fill in any part of the optional first-vehicle block.</summary>
    public bool IsEmpty() =>
        string.IsNullOrWhiteSpace(LicensePlate)
        && string.IsNullOrWhiteSpace(Brand)
        && string.IsNullOrWhiteSpace(Model)
        && string.IsNullOrWhiteSpace(Vin)
        && Mileage is null;
}
