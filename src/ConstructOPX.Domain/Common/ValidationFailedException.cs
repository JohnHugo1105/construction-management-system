namespace ConstructOPX.Domain.Common;

/// <summary>One or more input fields are invalid. Errors are keyed by the camelCase request field name.</summary>
public sealed class ValidationFailedException : Exception
{
    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }

    public ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
        : base("Some details need your attention.") => Errors = errors;

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>Collects field errors so a request reports every problem at once instead of one at a time.</summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool Any => _errors.Count > 0;

    public ValidationErrors Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list)) _errors[field] = list = [];
        list.Add(message);
        return this;
    }

    public void ThrowIfAny()
    {
        if (Any) throw new ValidationFailedException(_errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
    }
}
