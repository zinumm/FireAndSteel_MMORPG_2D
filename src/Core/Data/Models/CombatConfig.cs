namespace FireAndSteel.Core.Data.Models;

public sealed class CombatConfig
{
    public PlayerCombat Player { get; init; } = new();
    public CombatRules Rules { get; init; } = new();

    public sealed class PlayerCombat
    {
        public int BaseHp { get; init; } = 1;
        public int BaseAttack { get; init; } = 1;
    }

    public sealed class CombatRules
    {
        public double CritChance { get; init; } = 0.0;     // 0..1
        public double CritMultiplier { get; init; } = 1.0; // >= 1
    }
}
