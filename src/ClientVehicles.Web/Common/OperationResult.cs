namespace ClientVehicles.Web.Common;

/// <summary>
/// Result of a service operation. Errors are keyed by the input property they belong to
/// ("" for form-level errors) so a page can copy them straight into ModelState.
/// Keeping services free of ModelState makes them testable without the web stack.
/// </summary>
public sealed class OperationResult
{
    private readonly List<(string Key, string Message)> _errors = new();
    private readonly List<string> _warnings = new();

    public bool Succeeded => _errors.Count == 0;

    public IReadOnlyList<(string Key, string Message)> Errors => _errors;

    /// <summary>Non-blocking information the user still needs to see (e.g. a vehicle that could not be reactivated).</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Id of the record created or affected, when relevant.</summary>
    public int EntityId { get; private set; }

    public static OperationResult Success(int entityId = 0) => new() { EntityId = entityId };

    public static OperationResult Failure(string key, string message)
    {
        var result = new OperationResult();
        result.AddError(key, message);
        return result;
    }

    public OperationResult AddError(string key, string message)
    {
        _errors.Add((key, message));
        return this;
    }

    public OperationResult AddWarning(string message)
    {
        _warnings.Add(message);
        return this;
    }

    public OperationResult WithEntityId(int entityId)
    {
        EntityId = entityId;
        return this;
    }
}
