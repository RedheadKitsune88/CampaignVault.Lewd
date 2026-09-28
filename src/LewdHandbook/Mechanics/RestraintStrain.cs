using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// The engine-owned aftermath of staying tied up: the longer a limb-binding stays on, the worse the debuff, separately for
/// the arms and the legs, so a fully trussed captive carries both and they stack. These are consequences of a physical
/// state, not random events: they are stamped without an <c>EffectTier</c>, so they do not count toward the host's cap of
/// two event debuffs and never crowd out <c>apply_effect</c>. Magnitudes stay inside the host's moderate/serious tiers.
/// The effect is refreshed every clock step while the tie stays on and simply runs out afterwards; a captive's willpower
/// is worn down by each stage reached (see <see cref="Willpower"/>).
/// </summary>
internal static class RestraintStrain
{
    public const string AppliedBy = "restraint_aftermath";
    public const string ArmsKey = "restraint_aftermath.arms";
    public const string LegsKey = "restraint_aftermath.legs";

    public sealed record Stage(int Level, string Name, double Hours, Dictionary<string, float> Modifiers, string Hint);

    public sealed record Area(string Key, string[] Slots, Stage[] Stages);

    public static readonly Area Arms = new(ArmsKey, [BondageSlots.Wrists, BondageSlots.Arms, BondageSlots.Elbows],
    [
        new(1, "Cramped arms", 8, new() { ["AttackRoll"] = -1 }, "Free the arms; it wears off."),
        new(2, "Numb arms", 24, new() { ["AttackRoll"] = -2, ["SleightOfHand"] = -2 }, "Get out of the restraints and rest; the numbness fades within the day."),
        new(3, "Dead arms", 24, new() { ["AttackRoll"] = -3, ["SleightOfHand"] = -3 }, "Free the arms, then treatment (Medicine) or a long rest."),
    ]);

    public static readonly Area Legs = new(LegsKey, [BondageSlots.Thighs, BondageSlots.Ankles],
    [
        new(1, "Stiff legs", 8, new() { ["Speed"] = -5 }, "Free the legs; it wears off."),
        new(2, "Numb legs", 24, new() { ["Speed"] = -10, ["Athletics"] = -2 }, "Get out of the restraints and rest; the numbness fades within the day."),
        new(3, "Failing legs", 24, new() { ["Speed"] = -20, ["Athletics"] = -3 }, "Free the legs, then treatment (Medicine) or a long rest."),
    ]);

    public static readonly Area[] Areas = [Arms, Legs];

    /// <summary>Willpower lost on reaching a stage: 5, 10, 15.</summary>
    public static float DrainFor(int level) => 5f * level;

    public static bool IsLimbBinding(BindingEntry b) => Areas.Any(a => Touches(b, a));

    private static bool Touches(BindingEntry b, Area area) => b.Sites.SelectMany(BondageSlots.SlotsOf).Any(area.Slots.Contains);

    /// <summary>0 = nothing yet. Thresholds: 4h, 12h, 24h.</summary>
    public static int LevelFor(double hours) => hours switch { >= 24 => 3, >= 12 => 2, >= 4 => 1, _ => 0 };

    /// <summary>
    /// Consensual scene gear stops at the first stage: hours of play should tire someone, not injure them. Restraint that is
    /// not erotic (a captive), or any binding when the campaign runs non-consent, climbs the whole ladder.
    /// </summary>
    public static int Cap(IReadOnlyList<BindingEntry> bindings, LewdNonConsent nonConsent) =>
        bindings.All(b => b.Erotic) && nonConsent == LewdNonConsent.Off ? 1 : 3;

    /// <summary>
    /// Adds <paramref name="hours"/> to every binding, then stamps or refreshes each area's effect from its longest-worn
    /// binding. Returns what to tell the DM (stage changes and willpower loss), empty when nothing changed. Persists the list.
    /// </summary>
    public static List<string> Advance(Character character, List<BindingEntry> bindings, double hours, double nowDays, LewdNonConsent nonConsent)
    {
        var messages = new List<string>();
        foreach (var b in bindings)
            b.HoursBound += hours;
        BindingGraph.SetBindings(null, character, bindings);

        var effects = character.SystemStats.StatusEffects;
        foreach (var area in Areas)
        {
            var worn = bindings.Where(b => Touches(b, area)).ToList();
            if (worn.Count == 0)
                continue;
            var level = Math.Min(LevelFor(worn.Max(b => b.HoursBound)), Cap(worn, nonConsent));
            if (level == 0)
                continue;

            var stage = area.Stages[level - 1];
            var existing = effects.FirstOrDefault(e => string.Equals(e.EffectKey, area.Key, StringComparison.OrdinalIgnoreCase));
            var previous = existing is null ? 0 : Array.FindIndex(area.Stages, s => s.Name == existing.Name) + 1;
            if (existing is null)
            {
                existing = new StatusEffect { Category = "Injury", AppliedBy = AppliedBy, EffectKey = area.Key };
                effects.Add(existing);
            }

            existing.Name = stage.Name;
            existing.StatModifiers = new Dictionary<string, float>(stage.Modifiers);
            existing.RecoveryHint = stage.Hint;
            existing.ExpiresAtDay = (float)(nowDays + stage.Hours / 24.0);
            if (level <= previous)
                continue;

            var lost = Willpower.Drain(character, DrainFor(level));
            messages.Add(
                $"{character.Name} has been bound for {worn.Max(b => b.HoursBound):0.#}h: {stage.Name} " +
                $"({string.Join(", ", stage.Modifiers.Select(kv => $"{kv.Key} {kv.Value:+0.##;-0.##}"))})" +
                (lost > 0 ? $"; willpower −{lost:0.#} ({character.SystemStats.Willpower:0.#} left)." : "."));
        }

        return messages;
    }
}
