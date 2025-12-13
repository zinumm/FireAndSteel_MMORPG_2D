using FireAndSteel.Core.Data.Models;

namespace FireAndSteel.Core.Data.Validation.Rules;

public static class MobRules
{
    public static void Validate(IEnumerable<MobDef> mobs, ValidationResult outResult)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var m in mobs)
        {
            var p = $"mobs[{m.Id}]";

            if (!CommonRules.NonEmpty(m.Id))
                outResult.Add("MOB_ID_EMPTY", p, "Mob.id vazio.");

            if (!seen.Add(m.Id))
                outResult.Add("MOB_ID_DUP", p, $"Mob.id duplicado: '{m.Id}'.");

            if (!CommonRules.NonEmpty(m.Name))
                outResult.Add("MOB_NAME_EMPTY", p, $"Mob.name vazio: '{m.Id}'.");

            if (m.Hp < 1)
                outResult.Add("MOB_HP_RANGE", p, $"Mob.hp deve ser >= 1: '{m.Id}'.");

            if (m.Attack < 0)
                outResult.Add("MOB_ATK_RANGE", p, $"Mob.attack deve ser >= 0: '{m.Id}'.");
        }
    }
}
