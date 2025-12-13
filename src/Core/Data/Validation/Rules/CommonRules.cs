namespace FireAndSteel.Core.Data.Validation.Rules;

public static class CommonRules
{
    public static bool NonEmpty(string? value) => !string.IsNullOrWhiteSpace(value);

    public static bool In01(double value) => value >= 0.0 && value <= 1.0;
    public static bool In01(float value)  => value >= 0.0f && value <= 1.0f;
    public static bool In01(decimal value)=> value >= 0m && value <= 1m;
}
