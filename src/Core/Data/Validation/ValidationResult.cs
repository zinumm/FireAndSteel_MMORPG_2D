namespace FireAndSteel.Core.Data.Validation;

public sealed class ValidationResult
{
    private readonly List<ValidationError> _errors = new();
    public IReadOnlyList<ValidationError> Errors => _errors;
    public bool Ok => _errors.Count == 0;

    public void Add(string code, string path, string message)
        => _errors.Add(new ValidationError(code, path, message));
}
