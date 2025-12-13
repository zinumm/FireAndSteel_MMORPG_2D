using FireAndSteel.Core.Data.Models;

namespace FireAndSteel.Core.Data;

public sealed class DataStore
{
    public required Dictionary<string, ItemDef> ItemsById { get; init; }
    public required Dictionary<string, MobDef> MobsById { get; init; }

    public required CombatConfig Combat { get; init; }
    public required DropsConfig Drops { get; init; }
}
