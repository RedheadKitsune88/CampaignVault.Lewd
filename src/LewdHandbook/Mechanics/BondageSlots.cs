namespace LewdHandbook.Mechanics;

/// <summary>
/// The body slots a binding can occupy, and everything derived from which of them are taken. A binding's <c>sites</c> are
/// mapped onto slots; the effects (hands unusable, hobbled, gagged, blind, hog-tied…) come from the occupied set plus
/// orientation and posture, so a gag, a hood and a bitchsuit all mean the same thing where they overlap. Ties are presets:
/// they only fill in sites, orientation and DCs. Pure functions, no state.
/// </summary>
internal static class BondageSlots
{
    public const string Wrists = "wrists", Arms = "arms", Elbows = "elbows", Thighs = "thighs", Ankles = "ankles",
        Torso = "torso", Neck = "neck", Mouth = "mouth", Eyes = "eyes";

    public static readonly string[] All = [Wrists, Arms, Elbows, Thighs, Ankles, Torso, Neck, Mouth, Eyes];

    /// <summary>Site names the model may use → the slots they occupy. Unknown names occupy nothing (e.g. "head" alone).</summary>
    public static IReadOnlyList<string> SlotsOf(string? site)
    {
        var s = (site ?? "").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return s switch
        {
            "wrists" or "wrist" or "hands" or "hand" or "fingers" => [Wrists],
            "arms" or "arm" or "forearms" or "forearm" or "shoulders" or "shoulder" => [Arms],
            "elbows" or "elbow" => [Elbows],
            "thighs" or "thigh" or "knees" or "knee" or "calves" or "calf" => [Thighs],
            "ankles" or "ankle" or "feet" or "foot" => [Ankles],
            "legs" or "leg" => [Thighs, Ankles],
            "torso" or "chest" or "breasts" or "waist" or "body" => [Torso],
            "neck" or "throat" => [Neck],
            "mouth" or "jaw" => [Mouth],
            "eyes" or "eye" => [Eyes],
            "head" => [Eyes, Mouth],
            _ => [],
        };
    }

    /// <summary>The occupied slots across all bindings, each with the orientation that last touched it (null when none was given).</summary>
    public static Dictionary<string, string?> Occupied(IEnumerable<BindingEntry> bindings)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in bindings)
        {
            var orientation = string.IsNullOrWhiteSpace(b.Orientation) ? null : BindingGraph.NormalizeOrientation(b.Orientation);
            foreach (var slot in b.Sites.SelectMany(SlotsOf))
                map[slot] = orientation is null or "free" ? map.GetValueOrDefault(slot) : orientation;
        }

        return map;
    }

    /// <summary>A slot is loose only when every binding on it is loose; one tight tie makes it tight.</summary>
    public static HashSet<string> LooseSlots(IEnumerable<BindingEntry> bindings)
    {
        var tight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in bindings)
        {
            foreach (var slot in b.Sites.SelectMany(SlotsOf))
            {
                seen.Add(slot);
                if (b.Slack != "loose")
                    tight.Add(slot);
            }
        }

        seen.ExceptWith(tight);
        return seen;
    }

    /// <summary>Movement cap in feet the lower-body ties impose: 5 tight, 15 loose, null when nothing hobbles.</summary>
    public static int? SpeedCap(IReadOnlyList<BindingEntry> bindings)
    {
        var slots = Occupied(bindings);
        var lower = new[] { Thighs, Ankles }.Where(slots.ContainsKey).ToList();
        if (lower.Count == 0)
            return null;
        var loose = LooseSlots(bindings);
        return lower.All(loose.Contains) ? 15 : 5;
    }

    /// <summary>
    /// Condition/effect tokens the occupied slots imply, in the same vocabulary as a binding's <c>implies</c>/<c>effects</c>.
    /// <paramref name="posture"/> is the character's posture label (hogtie, kneeling…).
    /// </summary>
    public static HashSet<string> Derive(IReadOnlyList<BindingEntry> bindings, string? posture = null)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slots = Occupied(bindings);
        bool Has(string slot) => slots.ContainsKey(slot);
        string? Orient(string slot) => slots.GetValueOrDefault(slot);

        // A loose tie leaves play: it does not pin. Only slots held by a tight tie count towards pinning the hands.
        var loose = LooseSlots(bindings);
        bool Tight(string slot) => Has(slot) && !loose.Contains(slot);
        var pinnedByAttachment = Tight(Wrists) && Orient(Wrists) is "behind" or "above" or "belt" or "collar";
        var pinnedByLimbs = (Tight(Wrists) && (Tight(Arms) || Tight(Elbows))) || (Tight(Arms) && Tight(Elbows));
        var handsPinned = pinnedByAttachment || pinnedByLimbs;
        var upperBound = Has(Wrists) || Has(Arms) || Has(Elbows);
        if (upperBound)
            tokens.Add("cuffed");
        if (handsPinned)
        {
            tokens.Add("no_hand_use");
            tokens.Add("no_somatic_spellcasting");
        }
        else if (new[] { Wrists, Arms, Elbows }.Any(slot => Has(slot) && loose.Contains(slot)))
        {
            // Slack, not a pin: the hands work, badly. Tight cuffs in front stay as they were (cuffed, no more).
            tokens.Add("awkward_hands");
        }

        // Upper-arm or elbow bonds on top of the wrists take the whole limb out, not just the hands.
        if (pinnedByLimbs)
            tokens.Add("limb_bound");

        if (Has(Ankles) || Has(Thighs))
            tokens.Add("hobbled");

        var hogtie = string.Equals(posture, "hogtie", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(posture, "hogtied", StringComparison.OrdinalIgnoreCase) ||
                     bindings.Any(b => string.Equals(BindingGraph.NormalizeOrientation(b.Orientation), "hogtie", StringComparison.Ordinal));
        if (hogtie && Has(Wrists) && Has(Ankles))
            tokens.Add("full_tied");

        if (Has(Mouth))
        {
            tokens.Add("gagged");
            tokens.Add("no_verbal_spellcasting");
        }

        if (Has(Eyes))
            tokens.Add("blinded");

        return tokens;
    }

    /// <summary>The one-line description the DM reads: which slots are taken and what that means right now.</summary>
    public static string Summary(IReadOnlyList<BindingEntry> bindings, string? posture, bool anchored)
    {
        if (bindings.Count == 0)
            return "";
        var slots = Occupied(bindings);
        var looseSlots = LooseSlots(bindings);
        var slotText = string.Join(", ", All.Where(slots.ContainsKey).Select(s =>
            (slots[s] is { } o ? $"{s} {o}" : s) + (looseSlots.Contains(s) ? " (loose)" : "")));

        var set = BindingGraph.CollectImplied(bindings);
        set.UnionWith(BindingGraph.CollectEffects(bindings));
        set.UnionWith(Derive(bindings, posture));
        var effects = new List<string>();
        if (set.Contains("full_tied"))
            effects.Add("hog-tied: cannot stand");
        if (set.Contains("encased"))
            effects.Add("encased: incapacitated");
        if (Restraint.BlocksSomaticComponents(bindings))
            effects.Add("hands unusable, no somatic components");
        else if (set.Contains("awkward_hands"))
            effects.Add("hands awkwardly usable: disadvantage on attacks and Dex");
        else if (set.Contains("cuffed"))
            effects.Add("hands bound, disadvantage on Dex");
        if (set.Contains("encased"))
            effects.Add("speed 5 ft");
        else if (SpeedCap(bindings) is { } cap)
            effects.Add($"speed {cap} ft{(cap == 15 ? " (loose)" : "")}");
        if (Restraint.BlocksVerbalComponents(bindings))
            effects.Add("no verbal components");
        if (set.Contains("blinded") || set.Contains("no_sight"))
            effects.Add("blind");
        if (anchored)
            effects.Add("tethered");
        if (!string.IsNullOrWhiteSpace(posture))
            effects.Add($"posture {posture}");

        var fits = bindings.Where(b => !string.IsNullOrWhiteSpace(b.Fit)).Select(b => $"{b.Kind}: {b.Fit!.Trim()}").ToList();
        var fitText = fits.Count == 0 ? "" : $" Fit: {string.Join("; ", fits)}.";
        return $"Slots [{slotText}] → {(effects.Count == 0 ? "no mechanical effect" : string.Join("; ", effects))}.{fitText}";
    }

    // Material → (escape DC, break DC). Escape is about knots and buckles, break about toughness.
    private static readonly Dictionary<string, (int Escape, int Break)> MaterialDcs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cloth"] = (12, 10), ["bandage"] = (12, 10), ["silk"] = (13, 11), ["tar"] = (15, 14), ["twine"] = (12, 11),
        ["rope"] = (15, 13), ["cord"] = (14, 12), ["hemp"] = (15, 13), ["leather"] = (16, 15), ["hardwood"] = (16, 16),
        ["oak"] = (16, 18), ["latex"] = (17, 15), ["chain"] = (18, 20), ["iron"] = (20, 22), ["steel"] = (22, 24),
        ["mithral"] = (23, 25), ["adamantine"] = (26, 28), ["adamant"] = (26, 28), ["magic"] = (25, 25), ["enchanted"] = (25, 25),
    };

    private static readonly Dictionary<string, int> QualityBonus = new(StringComparer.OrdinalIgnoreCase)
    {
        ["crude"] = -4, ["poor"] = -2, ["standard"] = 0, ["fine"] = 2, ["masterwork"] = 4, ["enchanted"] = 6,
    };

    public static bool IsQuality(string? quality) => quality is not null && QualityBonus.ContainsKey(quality.Trim());

    public static string QualityList => string.Join(", ", QualityBonus.Keys);

    /// <summary>The DCs for the toughest known material, or null when none of the materials is in the table.</summary>
    public static (int Escape, int Break)? MaterialBase(IEnumerable<string> materials)
    {
        (int Escape, int Break)? best = null;
        foreach (var m in materials)
        {
            if (m is null || !MaterialDcs.TryGetValue(m.Trim(), out var dcs))
                continue;
            if (best is null || dcs.Break > best.Value.Break)
                best = dcs;
        }

        return best;
    }

    public static int QualityDelta(string? quality) =>
        quality is not null && QualityBonus.TryGetValue(quality.Trim(), out var d) ? d : 0;
}
