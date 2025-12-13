using FireAndSteel.Core.Data.Models;
using FireAndSteel.Core.Data.Validation.Rules;

namespace FireAndSteel.Core.Data.Validation;

public static class Validator
{
    public static ValidationResult ValidateAll(DataStore store)
    {
        var r = new ValidationResult();

        ItemRules.Validate(store.ItemsById.Values, r);
        MobRules.Validate(store.MobsById.Values, r);
        CombatRules.Validate(store.Combat, r);

        DropRules.Validate(store.Drops, store.ItemsById, store.MobsById, r);

        return r;
    }
}
