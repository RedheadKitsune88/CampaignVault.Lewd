using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// The engine-owned shock of having an imprint forced deeper: every time an <em>unwilling</em> track climbs a level the captive
/// is left rattled for a while (worse Wisdom and Intelligence saves, so the next tick is harder to resist) and loses some
/// willpower. Willing imprints cost nothing. One effect per character, no <c>EffectTier</c> (so the host's event-debuff cap does
/// not see it): a new climb only ever raises it to the stronger stage or refreshes its clock, so several tracks cannot pile up.
/// Magnitudes stay inside the host's moderate/serious tiers.
/// </summary>
internal static class ImprintAftermath
{
    public const string AppliedBy = "imprint_aftermath";
    public const string Key = "imprint_aftermath";

    public sealed record Stage(int Level, string Name, double Hours, Dictionary<string, float> Modifiers, string Hint);

    public static readonly Stage[] Stages =
    [
        new(1, "Shaken", 8, new() { ["Wisdom"] = -1 }, "Rest; the shock fades within the day."),
        new(2, "Rattled", 24, new() { ["Wisdom"] = -2, ["Intelligence"] = -1 }, "Rest and a calm, safe day, or a long rest."),
        new(3, "Broken down", 24, new() { ["Wisdom"] = -3, ["Intelligence"] = -2 }, "A long rest, or calming care (Medicine or Insight)."),
    ];

    /// <summary>Willpower lost when a track reaches this level: 4, 8, 12.</summary>
    public static float DrainFor(int level) => 4f * level;

    /// <summary>Stamps or raises the shock for an unwilling climb from <paramref name="before"/> to <paramref name="after"/>. Returns the DM message or null.</summary>
    public static string? OnClimb(Character character, string track, string origin, int before, int after, double nowDays)
    {
        if (after <= before || string.Equals(origin, "willing", StringComparison.OrdinalIgnoreCase))
            return null;
        var stage = Stages[Math.Clamp(after, 1, Stages.Length) - 1];
        var effects = character.SystemStats.StatusEffects;
        var existing = effects.FirstOrDefault(e => string.Equals(e.EffectKey, Key, StringComparison.OrdinalIgnoreCase));
        var previous = existing is null ? 0 : Array.FindIndex(Stages, s => existing.Name.StartsWith(s.Name, StringComparison.Ordinal)) + 1;
        if (existing is null)
        {
            existing = new StatusEffect { Category = "Condition", AppliedBy = AppliedBy, EffectKey = Key };
            effects.Add(existing);
        }

        var expires = (float)(nowDays + stage.Hours / 24.0);
        if (stage.Level >= previous)
        {
            existing.Name = $"{stage.Name} ({track})";
            existing.StatModifiers = new Dictionary<string, float>(stage.Modifiers);
            existing.RecoveryHint = stage.Hint;
            existing.ExpiresAtDay = expires;
        }
        else
        {
            existing.ExpiresAtDay = Math.Max(existing.ExpiresAtDay ?? 0f, expires);
        }

        var lost = Willpower.Drain(character, DrainFor(after));
        return $"{character.Name}: forced {track} imprint reached level {after}: {stage.Name} " +
               $"({string.Join(", ", stage.Modifiers.Select(kv => $"{kv.Key} saves {kv.Value:+0.##;-0.##}"))})" +
               (lost > 0 ? $"; willpower −{lost:0.#} ({character.SystemStats.Willpower:0.#} left)." : ".");
    }
}
