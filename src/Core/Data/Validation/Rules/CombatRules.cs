using FireAndSteel.Core.Data.Models;

namespace FireAndSteel.Core.Data.Validation.Rules;

public static class CombatRules
{
    public static void Validate(CombatConfig cfg, ValidationResult outResult)
    {
        if (cfg.Player.BaseHp < 1)
            outResult.Add("COMBAT_PLAYER_HP", "combat.player.baseHp", "baseHp deve ser >= 1.");

        if (cfg.Player.BaseAttack < 0)
            outResult.Add("COMBAT_PLAYER_ATK", "combat.player.baseAttack", "baseAttack deve ser >= 0.");

        if (!CommonRules.In01(cfg.Rules.CritChance))
            outResult.Add("COMBAT_CRIT_CHANCE", "combat.rules.critChance", "critChance deve estar em 0..1.");

        if (cfg.Rules.CritMultiplier < 1.0)
            outResult.Add("COMBAT_CRIT_MULT", "combat.rules.critMultiplier", "critMultiplier deve ser >= 1.0.");
    }
}
