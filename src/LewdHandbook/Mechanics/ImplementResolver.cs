using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal sealed record ResolvedImplement(
    string Source,
    string DiceExpression,
    string? DamageType,
    IReadOnlyList<string> Tags,
    bool IsFinesse);

/// <summary>
/// Resolution order for stimulation dice. Anything the commit names explicitly wins:
/// implementId → anatomyKey → stimulationDice. Only when the commit names nothing (and needs dice) does the engine
/// guess: a held item that is marked as an implement, then the actor's first anatomy Trait.
/// </summary>
internal static class ImplementResolver
{
    public static ResolvedImplement? Resolve(
        Character? actor,
        IReadOnlyDictionary<string, Item> items,
        string? implementId,
        string? anatomyKey,
        string? explicitDice,
        string? explicitType,
        bool allowGuess = true)
    {
        if (!string.IsNullOrWhiteSpace(implementId) && TryFromItemId(items, implementId, out var fromId))
            return fromId;

        if (!string.IsNullOrWhiteSpace(anatomyKey) && AnatomyTraits.Find(actor, anatomyKey) is { } named)
            return FromAnatomy(named);

        if (!string.IsNullOrWhiteSpace(explicitDice))
        {
            return new ResolvedImplement(
                "explicit",
                explicitDice.Trim(),
                explicitType,
                explicitType is null ? [] : [explicitType],
                IsFinesse: false);
        }

        if (!allowGuess || actor is null)
            return null;

        foreach (var item in items.Values)
        {
            if (IsHeldBy(item, actor.Id) && IsImplement(item) && TryFromItem(item, out var held))
                return held;
        }

        // The engine never guesses a receptive part: the commit names it when it does the work. Declared implements beat
        // plugin defaults, pure implements beat "both" (mouth); ties break by catalogue order.
        return AnatomyTraits.Effective(actor)
            .Where(a => a.CanStimulate)
            .OrderBy(a => a.IsDefault)
            .ThenBy(a => a.Role == AnatomyRole.Both)
            .FirstOrDefault() is { } first ? FromAnatomy(first) : null;
    }

    private static ResolvedImplement FromAnatomy(AnatomyImplement anatomy) =>
        new(anatomy.Key, anatomy.DieExpression, GuessType(anatomy.Tags), anatomy.Tags, anatomy.IsFinesse);

    /// <summary>A held weapon is not a toy: guessing only considers items marked as implements.</summary>
    private static bool IsImplement(Item item) =>
        item.Properties.Keys.Any(k =>
            k.Equals("implementTags", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("stimulationDice", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("lewdCategory", StringComparison.OrdinalIgnoreCase));

    /// <summary>Every die at its highest face, plus the expression's flat modifiers ("2d6+1" → 13).</summary>
    public static int MaximizeDice(string expression)
    {
        if (!AnatomyTraits.TryParseDice(expression, out var count, out var faces))
            return 0;
        return Math.Max(0, count * faces + FlatModifier(expression));
    }

    /// <summary>Sum of the +N / −N terms after the first dice term ("1d8+2-1" → 1). Dice terms after the first are ignored.</summary>
    public static int FlatModifier(string expression)
    {
        var total = 0;
        var expr = (expression ?? "").Replace(" ", "").ToLowerInvariant();
        var i = expr.IndexOfAny(['+', '-'], 1);
        while (i > 0 && i < expr.Length)
        {
            var sign = expr[i] == '-' ? -1 : 1;
            var end = expr.IndexOfAny(['+', '-'], i + 1);
            var term = end < 0 ? expr[(i + 1)..] : expr[(i + 1)..end];
            if (!term.Contains('d') && int.TryParse(term, out var n))
                total += sign * n;
            i = end;
        }

        return total;
    }

    private static bool TryFromItemId(IReadOnlyDictionary<string, Item> items, string implementId, out ResolvedImplement resolved)
    {
        foreach (var item in items.Values)
        {
            if (string.Equals(item.Id, implementId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Name, implementId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.DefinitionName, implementId, StringComparison.OrdinalIgnoreCase) ||
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
