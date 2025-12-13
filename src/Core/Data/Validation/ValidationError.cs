namespace FireAndSteel.Core.Data.Validation;

public sealed record ValidationError(string Code, string Path, string Message);
