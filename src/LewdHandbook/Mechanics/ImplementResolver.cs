using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal sealed record ResolvedImplement(
    string Source,
    string DiceExpression,
    string? DamageType,
    IReadOnlyList<string> Tags,
    bool IsFinesse);

/// <summary>
/// Resolution order for martial stimulation dice:
/// held Item Properties → implementId Item (by id/name match) → Traits anatomy.* → explicit stimulationDice.
/// </summary>
internal static class ImplementResolver
{
    public static ResolvedImplement? Resolve(
        Character? actor,
        IReadOnlyDictionary<string, Item> items,
        string? implementId,
        string? anatomyKey,
        string? explicitDice,
        string? explicitType)
    {
        // 1) Explicit implementId → item in context
        if (!string.IsNullOrWhiteSpace(implementId))
        {
            if (TryFromItemId(items, implementId, out var fromId))
                return fromId;
        }

        // 2) Held/equipped items on actor with implement-like properties
        if (actor is not null)
        {
            foreach (var item in items.Values)
            {
                if (!IsHeldBy(item, actor.Id))
                    continue;
                if (TryFromItem(item, out var held))
                    return held;
            }
        }

        // 3) Anatomy trait
        var anatomy = AnatomyTraits.Find(actor, anatomyKey) ??
                      AnatomyTraits.ListAnatomy(actor).FirstOrDefault();
        if (anatomy is not null)
        {
            return new ResolvedImplement(
                anatomy.Key,
                anatomy.DieExpression,
                GuessType(anatomy.Tags),
                anatomy.Tags,
                anatomy.IsFinesse);
        }

        // 4) Explicit dice on the commit
        if (!string.IsNullOrWhiteSpace(explicitDice))
        {
            return new ResolvedImplement(
                "explicit",
                explicitDice.Trim(),
                explicitType,
                explicitType is null ? [] : [explicitType],
                IsFinesse: false);
        }

        return null;
    }

    public static int MaximizeDice(string expression)
    {
        if (!AnatomyTraits.TryParseDice(expression, out var count, out var faces))
            return 0;
        return count * faces;
    }

    private static bool TryFromItemId(IReadOnlyDictionary<string, Item> items, string implementId, out ResolvedImplement resolved)
    {
        foreach (var item in items.Values)
        {
            if (string.Equals(item.Id, implementId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Name, implementId, StringComparison.OrdinalIgnoreCase) ||
                item.Id.EndsWith("/" + implementId, StringComparison.OrdinalIgnoreCase))
            {
                if (TryFromItem(item, out resolved))
                    return true;
            }
        }

        // Treat implementId as a template name with default dice when no live Item exists yet.
        resolved = new ResolvedImplement(
            implementId,
            "1d6",
            "piercing",
            ["artificial", implementId],
            implementId.Contains("finesse", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    private static bool TryFromItem(Item item, out ResolvedImplement resolved)
    {
        resolved = null!;
        var props = item.Properties;
        if (props is null || props.Count == 0)
            return false;

        var dice = GetStringProp(props, "damageDice") ??
                   GetStringProp(props, "damage") ??
                   GetStringProp(props, "stimulationDice") ??
                   GetStringProp(props, "die");
        if (string.IsNullOrWhiteSpace(dice))
            return false;

        var type = GetStringProp(props, "damageType") ?? GetStringProp(props, "stimulationType");
        var tags = GetStringListProp(props, "implementTags");
        if (tags.Count == 0)
            tags = GetStringListProp(props, "tags");
        var finesse = GetBoolProp(props, "finesse") ||
                      tags.Any(t => t.Equals("finesse", StringComparison.OrdinalIgnoreCase));

        resolved = new ResolvedImplement(item.Id, dice!, type, tags, finesse);
        return true;
    }

    private static bool IsHeldBy(Item item, string characterId)
    {
        if (string.IsNullOrEmpty(item.HolderId) ||
            !string.Equals(item.HolderId, characterId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Prefer equipped implements; otherwise any held item with stim dice Properties counts.
        if (item.IsEquipped)
            return true;
        if (item.Properties.TryGetValue("held", out var held) && held is bool h)
            return h;
        return true;
    }

    private static string? GuessType(IReadOnlyList<string> tags)
    {
        if (tags.Any(t => t.Contains("phallic", StringComparison.OrdinalIgnoreCase) ||
                          t.Contains("penetrat", StringComparison.OrdinalIgnoreCase)))
            return "piercing";
        if (tags.Any(t => t.Contains("vibrat", StringComparison.OrdinalIgnoreCase)))
            return "thunder";
        return null;
    }

    private static string? GetStringProp(Dictionary<string, object> props, string key)
    {
        foreach (var (k, v) in props)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                return v?.ToString();
        }

        return null;
    }

    private static bool GetBoolProp(Dictionary<string, object> props, string key)
    {
        foreach (var (k, v) in props)
        {
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase) || v is null)
                continue;
            if (v is bool b) return b;
            if (bool.TryParse(v.ToString(), out var parsed)) return parsed;
        }

        return false;
    }

    private static List<string> GetStringListProp(Dictionary<string, object> props, string key)
    {
        foreach (var (k, v) in props)
        {
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase) || v is null)
                continue;
            if (v is IEnumerable<object> objs)
                return objs.Select(o => o?.ToString() ?? "").Where(s => s.Length > 0).ToList();
            if (v is IEnumerable<string> strs)
                return strs.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var s = v.ToString();
            if (string.IsNullOrWhiteSpace(s)) return [];
            return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return [];
    }
}
