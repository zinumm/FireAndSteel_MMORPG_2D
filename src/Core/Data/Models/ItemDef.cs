namespace FireAndSteel.Core.Data.Models;

public sealed class ItemDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int MaxStack { get; init; } = 1;
}

public sealed class ItemsDoc
{
    public List<ItemDef> Items { get; init; } = new();
}
