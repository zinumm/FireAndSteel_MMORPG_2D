using FireAndSteel.Core.Data.Models;

namespace FireAndSteel.Core.Data.Validation.Rules;

public static class ItemRules
{
    public static void Validate(IEnumerable<ItemDef> items, ValidationResult outResult)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var it in items)
        {
            var p = $"items[{it.Id}]";

            if (!CommonRules.NonEmpty(it.Id))
                outResult.Add("ITEM_ID_EMPTY", p, "Item.id vazio.");

            if (!seen.Add(it.Id))
                outResult.Add("ITEM_ID_DUP", p, $"Item.id duplicado: '{it.Id}'.");

            if (!CommonRules.NonEmpty(it.Name))
                outResult.Add("ITEM_NAME_EMPTY", p, $"Item.name vazio: '{it.Id}'.");

            if (it.MaxStack < 1)
                outResult.Add("ITEM_MAXSTACK_RANGE", p, $"Item.maxStack deve ser >= 1: '{it.Id}'.");
        }
    }
}
