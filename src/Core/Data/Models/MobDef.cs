namespace FireAndSteel.Core.Data.Models;

public sealed class MobDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Hp { get; init; } = 1;
    public int Attack { get; init; } = 0;
}

public sealed class MobsDoc
{
    public List<MobDef> Mobs { get; init; } = new();
}
