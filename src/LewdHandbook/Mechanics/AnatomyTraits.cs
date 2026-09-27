using System.Globalization;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>What a body part does in a scene: it stimulates (implement), it is stimulated or filled (receptive), or both.</summary>
internal enum AnatomyRole
{
    Implement,
    Receptive,
    Both,
}

/// <param name="IsDefault">Filled in by the plugin rather than declared by the DM (<c>source=default</c>, or synthesised at read time).</param>
internal sealed record AnatomyImplement(
    string Key,
    string DieExpression,
    IReadOnlyList<string> Tags,
    string? Size,
    bool IsFinesse,
    AnatomyRole Role = AnatomyRole.Implement,
    bool IsDefault = false)
{
    public string Slot => Key.StartsWith(LewdKeys.AnatomyPrefix, StringComparison.OrdinalIgnoreCase)
        ? Key[LewdKeys.AnatomyPrefix.Length..]
        : Key;

    /// <summary>Can this part supply stimulation dice?</summary>
    public bool CanStimulate => Role != AnatomyRole.Receptive;
}

/// <summary>One entry of the canonical slot vocabulary.</summary>
/// <param name="Universal">Every humanoid has it, so it is derived instead of declared; the DM only declares the variable slots.</param>
/// <param name="Default">The Trait value used when the slot is derived or filled in.</param>
internal sealed record AnatomySlot(string Name, AnatomyRole Role, bool Universal, string Default, params string[] Aliases);

/// <summary>
/// Canonical anatomy vocabulary. <c>Traits["lewd_encounter.anatomy.&lt;slot&gt;"]</c> holds
/// <c>die=1d8;tags=phallic,natural;size=medium;finesse=false;role=implement|receptive|both</c>; every part is optional
/// and <c>role</c> defaults to the slot's role (unknown slots are implements). The value <c>none</c> declares the part
/// absent. <c>anatomy.plan=custom</c> switches off the derived universal slots (slimes, constructs, beasts).
/// </summary>
internal static class AnatomySlots
{
    public const string SourceDefault = "source=default";
    public const string Absent = "none";
    public const string PlanSuffix = "plan";
    public const string PlanCustom = "custom";

    /// <summary>Catalogue order is the tie-break when the engine has to guess an implement.</summary>
    public static readonly IReadOnlyList<AnatomySlot> All =
    [
        new("hands", AnatomyRole.Implement, true, "die=1d4;tags=natural,manual;finesse=true;" + SourceDefault, "hand"),
        new("mouth", AnatomyRole.Both, true, "die=1d4;tags=natural,oral;role=both;" + SourceDefault, "lips"),
        new("ass", AnatomyRole.Receptive, true, "tags=natural;role=receptive;" + SourceDefault, "anus", "butt", "rear"),
        new("cock", AnatomyRole.Implement, false, "die=1d8;tags=phallic,natural", "dick", "penis", "phallus"),
        new("pussy", AnatomyRole.Receptive, false, "tags=natural;role=receptive", "vagina", "cunt"),
        new("breasts", AnatomyRole.Receptive, false, "tags=natural;role=receptive", "tits", "chest", "breast"),
        new("tail", AnatomyRole.Implement, false, "die=1d4;tags=natural,prehensile"),
    ];

    /// <summary>Slots the DM must answer for (declare, or mark <c>none</c>) because bodies differ.</summary>
    public static IEnumerable<AnatomySlot> Variable => All.Where(s => !s.Universal);

    /// <summary>The slots whose absence the completeness prompt asks about: the ones no body plan implies.</summary>
    public static readonly IReadOnlyList<string> Asked = ["cock", "pussy", "breasts"];

    public static AnatomySlot? Lookup(string nameOrAlias)
    {
        var want = nameOrAlias.Trim();
        return All.FirstOrDefault(s =>
            string.Equals(s.Name, want, StringComparison.OrdinalIgnoreCase) ||
            s.Aliases.Any(a => string.Equals(a, want, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The canonical slot name for an alias ("penis" → "cock"); unknown names pass through.</summary>
    public static string Canonical(string nameOrAlias) => Lookup(nameOrAlias)?.Name ?? nameOrAlias.Trim();

    public static int Order(string slot)
    {
        var i = All.ToList().FindIndex(s => string.Equals(s.Name, slot, StringComparison.OrdinalIgnoreCase));
        return i < 0 ? All.Count : i;
    }

    public static bool IsAbsent(string? value) =>
        string.Equals(value?.Trim(), Absent, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Natural body parts live on <see cref="SystemExtension.Traits"/> (string bag), not float Attributes.
/// Example: Traits["lewd_encounter.anatomy.cock"] = "die=1d8;tags=phallic,natural;size=medium;finesse=false"
/// </summary>
internal static class AnatomyTraits
{
    /// <summary>Parts the document declares (including plugin-filled defaults); excludes <c>none</c> and the plan marker.</summary>
    public static IReadOnlyList<AnatomyImplement> ListAnatomy(Character? character)
    {
        if (character?.SystemStats.Traits is not { Count: > 0 } traits)
            return [];

        var byKey = new Dictionary<string, AnatomyImplement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in traits)
        {
            if (!IsAnatomyKey(key) || string.IsNullOrWhiteSpace(value) || AnatomySlots.IsAbsent(value))
                continue;
            var normalized = NormalizeAnatomyKey(key);
            if (IsPlanKey(normalized))
                continue;
            // A canonical key wins over a legacy/alias spelling of the same slot.
            if (byKey.ContainsKey(normalized) && !string.Equals(key, normalized, StringComparison.OrdinalIgnoreCase))
                continue;
            byKey[normalized] = Parse(normalized, value);
        }

        return byKey.Values.ToList();
    }

    /// <summary>
    /// Declared parts plus the universal slots (mouth, hands, ass) nobody declared. A slot declared <c>none</c>, or a
    /// <c>plan=custom</c> body, gets no derived slots. Stable order: catalogue slots first, then the rest by name.
    /// </summary>
    public static IReadOnlyList<AnatomyImplement> Effective(Character? character)
    {
        var declared = ListAnatomy(character).ToList();
        if (character is not null && !IsCustomPlan(character))
        {
            foreach (var slot in AnatomySlots.All.Where(s => s.Universal))
            {
                var key = LewdKeys.AnatomyPrefix + slot.Name;
                if (IsDeclared(character, slot.Name))
                    continue;
                declared.Add(Parse(key, slot.Default) with { IsDefault = true });
            }
        }

        return declared
            .OrderBy(a => AnatomySlots.Order(a.Slot))
            .ThenBy(a => a.Slot, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Has the DM (or a plugin fill-in) said anything about this slot, including "none"?</summary>
    public static bool IsDeclared(Character? character, string slot)
    {
        var want = NormalizeAnatomyKey(AnatomySlots.Canonical(slot));
        return TryGetRaw(character, want, out _) || TryGetRaw(character, ToLegacyAnatomyKey(want), out _) ||
               AnatomySlots.Lookup(slot) is { } known &&
               known.Aliases.Any(a => TryGetRaw(character, LewdKeys.AnatomyPrefix + a, out _) ||
                                      TryGetRaw(character, LewdKeys.LegacyAnatomyPrefix + a, out _));
    }

    public static bool IsCustomPlan(Character? character) =>
        string.Equals(GetTrait(character, LewdKeys.AnatomyPrefix + AnatomySlots.PlanSuffix,
                LewdKeys.LegacyAnatomyPrefix + AnatomySlots.PlanSuffix),
            AnatomySlots.PlanCustom, StringComparison.OrdinalIgnoreCase);

    /// <summary>The part for a slot name or alias: declared, else a derived universal slot. Null for absent or unknown.</summary>
    public static AnatomyImplement? Find(Character? character, string? anatomyKeyOrSuffix)
    {
        if (character is null || string.IsNullOrWhiteSpace(anatomyKeyOrSuffix))
            return null;

        var want = NormalizeAnatomyKey(anatomyKeyOrSuffix.Trim());
        var slotName = want[LewdKeys.AnatomyPrefix.Length..];
        var slot = AnatomySlots.Lookup(slotName);

        string? raw = null;
        var found = TryGetRaw(character, want, out raw) || TryGetRaw(character, ToLegacyAnatomyKey(want), out raw);
        if (!found && slot is not null)
        {
            foreach (var alias in slot.Aliases)
            {
                if (TryGetRaw(character, LewdKeys.AnatomyPrefix + alias, out raw) ||
                    TryGetRaw(character, LewdKeys.LegacyAnatomyPrefix + alias, out raw))
                {
                    found = true;
                    break;
                }
            }
        }

        if (found)
            return AnatomySlots.IsAbsent(raw) ? null : Parse(want, raw!);

        return slot is { Universal: true } && !IsCustomPlan(character)
            ? Parse(want, slot.Default) with { IsDefault = true }
            : null;
    }

    public static string? GetSexualHistory(Character? character) =>
        GetTrait(character, LewdKeys.TraitSexualHistory, LewdKeys.LegacyTraitSexualHistory);

    public static string? GetRecoveryDie(Character? character) =>
        GetTrait(character, LewdKeys.TraitRecoveryDie, LewdKeys.LegacyTraitRecoveryDie);

    public static string? GetTrait(Character? character, string key, string? legacyKey = null)
    {
        if (TryGetRaw(character, key, out var v))
            return v!.Trim();
        if (legacyKey is not null && TryGetRaw(character, legacyKey, out v))
            return v!.Trim();
        return null;
    }

    public static bool IsAnatomyKey(string key) =>
        key.StartsWith(LewdKeys.AnatomyPrefix, StringComparison.OrdinalIgnoreCase) ||
        key.StartsWith(LewdKeys.LegacyAnatomyPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsPlanKey(string normalizedKey) =>
        string.Equals(normalizedKey, LewdKeys.AnatomyPrefix + AnatomySlots.PlanSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Canonical <c>lewd_encounter.anatomy.&lt;slot&gt;</c> for a bare slot, legacy key or alias ("penis" → cock).</summary>
    public static string NormalizeAnatomyKey(string keyOrSuffix)
    {
        var want = keyOrSuffix.Trim();
        if (want.StartsWith(LewdKeys.AnatomyPrefix, StringComparison.OrdinalIgnoreCase))
            want = want[LewdKeys.AnatomyPrefix.Length..];
        else if (want.StartsWith(LewdKeys.LegacyAnatomyPrefix, StringComparison.OrdinalIgnoreCase))
            want = want[LewdKeys.LegacyAnatomyPrefix.Length..];
        return LewdKeys.AnatomyPrefix + AnatomySlots.Canonical(want);
    }

    public static string ToLegacyAnatomyKey(string normalized) =>
        LewdKeys.LegacyAnatomyPrefix + normalized[LewdKeys.AnatomyPrefix.Length..];

    private static bool TryGetRaw(Character? character, string key, out string? value)
    {
        value = null;
        if (character?.SystemStats.Traits is not { Count: > 0 } traits)
            return false;
        if (traits.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
        {
            value = v;
            return true;
        }
        foreach (var (k, val) in traits)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(val))
            {
                value = val;
                return true;
            }
        }
        return false;
    }

    public static AnatomyImplement Parse(string key, string value)
    {
        var die = "1d4";
        var tags = new List<string> { "natural" };
        string? size = null;
        var finesse = false;
        var slot = key.StartsWith(LewdKeys.AnatomyPrefix, StringComparison.OrdinalIgnoreCase)
            ? key[LewdKeys.AnatomyPrefix.Length..]
            : key;
        var role = AnatomySlots.Lookup(slot)?.Role ?? AnatomyRole.Implement;
        var isDefault = false;

        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                // bare token → tag
                tags.Add(part);
                continue;
            }

            var name = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();
            if (name.Equals("die", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("dice", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("damage", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("damageDice", StringComparison.OrdinalIgnoreCase))
            {
                die = val;
            }
            else if (name.Equals("tags", StringComparison.OrdinalIgnoreCase))
            {
                tags = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
            else if (name.Equals("size", StringComparison.OrdinalIgnoreCase))
            {
                size = val;
            }
            else if (name.Equals("role", StringComparison.OrdinalIgnoreCase))
            {
                role = val.ToLowerInvariant() switch
                {
                    "receptive" or "receive" or "receiving" => AnatomyRole.Receptive,
                    "both" => AnatomyRole.Both,
                    "implement" or "active" => AnatomyRole.Implement,
                    _ => role,
                };
            }
            else if (name.Equals("source", StringComparison.OrdinalIgnoreCase))
            {
                isDefault = val.Equals("default", StringComparison.OrdinalIgnoreCase);
            }
            else if (name.Equals("finesse", StringComparison.OrdinalIgnoreCase))
            {
                finesse = val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                          val.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                          val.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
        }

        return new AnatomyImplement(key, die, tags, size, finesse, role, isDefault);
    }

    /// <summary>Parse leading NdM from expressions like 1d8+2 or 2d6.</summary>
    public static bool TryParseDice(string expression, out int count, out int faces)
    {
        count = 1;
        faces = 4;
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var expr = expression.Trim().ToLowerInvariant();
        var plus = expr.IndexOf('+');
        var minus = expr.IndexOf('-', 1);
        var cut = expr.Length;
        if (plus > 0) cut = Math.Min(cut, plus);
        if (minus > 0) cut = Math.Min(cut, minus);
        var core = expr[..cut];
        var d = core.IndexOf('d');
        if (d < 0)
            return int.TryParse(core, NumberStyles.Integer, CultureInfo.InvariantCulture, out faces);

        var left = d == 0 ? "1" : core[..d];
        var right = core[(d + 1)..];
        if (!int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
            count = 1;
        if (!int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out faces))
            return false;
        return count > 0 && faces > 0;
    }
}
