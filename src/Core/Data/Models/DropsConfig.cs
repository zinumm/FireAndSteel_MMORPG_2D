namespace FireAndSteel.Core.Data.Models;

public sealed class DropsConfig
{
    public List<DropTable> Tables { get; init; } = new();
    public List<MobDropLink> MobToTable { get; init; } = new();

    public sealed class DropTable
    {
        public string Id { get; init; } = "";
        public List<DropEntry> Entries { get; init; } = new();
    }

    public sealed class DropEntry
    {
        public string ItemId { get; init; } = "";
        public int Min { get; init; } = 0;
        public int Max { get; init; } = 0;
        public double Chance { get; init; } = 0.0; // 0..1
    }

    public sealed class MobDropLink
    {
        public string MobId { get; init; } = "";
        public string TableId { get; init; } = "";
    }
}
