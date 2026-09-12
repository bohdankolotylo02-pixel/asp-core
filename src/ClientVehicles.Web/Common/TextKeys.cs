using System.Globalization;
using System.Text;

namespace ClientVehicles.Web.Common;

/// <summary>
/// Normalisation helpers. Values are stored twice: once as the user typed them (for display)
/// and once as a "key" (for uniqueness checks and search), so that "12-ab-34", "12 AB 34"
/// and "12AB34" are treated as the same plate.
/// </summary>
public static class TextKeys
{
    /// <summary>Trims, collapses repeated whitespace, returns null when nothing is left.</summary>
    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts);
    }

    /// <summary>Same as <see cref="Clean"/> but never returns null.</summary>
    public static string CleanRequired(string? value) => Clean(value) ?? string.Empty;

    /// <summary>Upper-cased display form for plates and VINs: "12-ab-34" => "12-AB-34".</summary>
    public static string UpperDisplay(string? value) => CleanRequired(value).ToUpperInvariant();

    /// <summary>Letters and digits only, upper-cased: "12-AB-34" => "12AB34". Used for plate/VIN keys.</summary>
    public static string AlphaNumericKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>Digits only: "+351 912 345 678" => "351912345678". Used for phone search.</summary>
    public static string DigitsKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Lower-cased, accent-stripped form used for name search, so that "Joao" also finds "Joao Ferreira"
    /// written with accents. SQLite's LIKE is only case-insensitive for plain ASCII, so the comparison
    /// is done against this pre-computed column instead.
    /// </summary>
    public static string SearchKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
