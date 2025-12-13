namespace FireAndSteel.Core.Data;

public sealed class DataManifest
{
    public string Items { get; init; } = "items.json";
    public string Mobs { get; init; } = "mobs.json";
    public string Combat { get; init; } = "combat.json";
    public string Drops { get; init; } = "drops.json";
}
