using FireAndSteel.Core.Data.Models;

namespace FireAndSteel.Core.Data.Validation.Rules;

public static class DropRules
{
    public static void Validate(DropsConfig drops, IReadOnlyDictionary<string, ItemDef> itemsById, IReadOnlyDictionary<string, MobDef> mobsById, ValidationResult outResult)
    {
        var tableSeen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var t in drops.Tables)
        {
            var tp = $"drops.tables[{t.Id}]";

            if (!CommonRules.NonEmpty(t.Id))
                outResult.Add("DROP_TABLE_ID_EMPTY", tp, "DropTable.id vazio.");

            if (!tableSeen.Add(t.Id))
                outResult.Add("DROP_TABLE_ID_DUP", tp, $"DropTable.id duplicado: '{t.Id}'.");

            for (var i = 0; i < t.Entries.Count; i++)
            {
                var e = t.Entries[i];
                var ep = $"{tp}.entries[{i}]";

                if (!CommonRules.NonEmpty(e.ItemId))
                    outResult.Add("DROP_ITEMID_EMPTY", ep, "itemId vazio.");

                if (CommonRules.NonEmpty(e.ItemId) && !itemsById.ContainsKey(e.ItemId))
                    outResult.Add("DROP_ITEMID_REF", ep, $"itemId não existe: '{e.ItemId}'.");

                if (e.Min < 0 || e.Max < 0 || e.Min > e.Max)
                    outResult.Add("DROP_MINMAX_RANGE", ep, $"min/max inválidos (min={e.Min}, max={e.Max}).");

                if (!CommonRules.In01(e.Chance))
                    outResult.Add("DROP_CHANCE_RANGE", ep, $"chance deve estar em 0..1 (chance={e.Chance}).");
            }
        }

        foreach (var link in drops.MobToTable)
        {
            var lp = $"drops.mobToTable[{link.MobId}->{link.TableId}]";

            if (!CommonRules.NonEmpty(link.MobId) || !CommonRules.NonEmpty(link.TableId))
                outResult.Add("DROP_LINK_EMPTY", lp, "mobId/tableId vazio.");

            if (CommonRules.NonEmpty(link.MobId) && !mobsById.ContainsKey(link.MobId))
                outResult.Add("DROP_LINK_MOB_REF", lp, $"mobId não existe: '{link.MobId}'.");

            if (CommonRules.NonEmpty(link.TableId) && !drops.Tables.Any(t => t.Id == link.TableId))
                outResult.Add("DROP_LINK_TABLE_REF", lp, $"tableId não existe: '{link.TableId}'.");
        }
    }
}
