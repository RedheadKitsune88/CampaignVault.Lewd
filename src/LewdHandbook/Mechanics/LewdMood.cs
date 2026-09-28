using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Short, engine-owned mood buffs after wanted intimacy. The engine fixes the tiers and the guards; nothing here is authored by the
/// model, so it cannot be talked into a bigger bonus. Guards, all deterministic: the <c>lewdMoodBuffs</c> setting, one mood at a
/// time (a stronger one replaces a weaker, never stacks), no repeat of the same trigger key on the same day, and a per-day cap.
/// The effect has no <c>EffectTier</c>, so it never takes one of the host's event-buff slots. Magnitudes stay inside the host's
/// light/moderate tiers by hand (±1, at most 8 hours).
/// </summary>
internal static class LewdMood
{
    public const string AppliedBy = "lewd_mood";
    public const string Key = "lewd_mood";
    public const int DailyCap = 3;
    private const string LedgerKey = LewdKeys.ModeTraitPrefix + "mood.ledger";

    public sealed record Mood(string Trigger, int Rank, string Name, double Hours, Dictionary<string, float> Modifiers, string Hint);

    public static readonly Mood WarmGlow = new("advance", 1, "Warm glow", 1, new() { ["Persuasion"] = 1 }, "A welcome touch; it fades within the hour.");
    public static readonly Mood Afterglow = new("scene", 2, "Afterglow", 8, new() { ["Wisdom"] = 1, ["Persuasion"] = 1 }, "A good time; it fades within the day.");
    public static readonly Mood Relief = new("relief", 2, "Relief", 8, new() { ["Wisdom"] = 1, ["Intelligence"] = 1 }, "A weight eased; it fades within the day.");

    /// <summary>
    /// Grants <paramref name="mood"/> to <paramref name="character"/> unless a guard refuses. <paramref name="scope"/> names what the
    /// buff is for (a partner id, a scene's participants, an imprint track); the same trigger and scope earns one buff per day.
    /// Returns the DM message, or null when nothing was granted.
    /// </summary>
    public static string? TryGrant(Character character, Mood mood, string scope, int day, double nowDays, LewdSettings settings)
    {
        if (!settings.MoodBuffs)
            return null;

        var (ledgerDay, count, keys) = Read(character);
        if (ledgerDay != day)
            (count, keys) = (0, []);
        var key = $"{mood.Trigger}:{scope}".ToLowerInvariant();
        if (count >= DailyCap || keys.Contains(key))
            return null;

        var effects = character.SystemStats.StatusEffects;
        var live = effects.FirstOrDefault(e => string.Equals(e.EffectKey, Key, StringComparison.OrdinalIgnoreCase) &&
                                               (e.ExpiresAtDay is null || e.ExpiresAtDay > nowDays));
        if (live is not null && RankOf(live) >= mood.Rank)
            return null;

        effects.RemoveAll(e => string.Equals(e.EffectKey, Key, StringComparison.OrdinalIgnoreCase));
        effects.Add(new StatusEffect
        {
            Name = mood.Name,
            Category = "Buff",
            AppliedBy = AppliedBy,
            EffectKey = Key,
            StatModifiers = new Dictionary<string, float>(mood.Modifiers),
            RecoveryHint = mood.Hint,
            ExpiresAtDay = (float)(nowDays + mood.Hours / 24.0),
        });
        keys.Add(key);
        PregnancyState.Set(character, LedgerKey, $"{day}|{count + 1}|{string.Join(",", keys)}");
        return $"{character.Name}: {mood.Name} ({string.Join(", ", mood.Modifiers.Select(kv => $"{kv.Key} {kv.Value:+0.##;-0.##}"))}) for {mood.Hours:0.#}h.";
    }

    private static int RankOf(StatusEffect e) => new[] { WarmGlow, Afterglow, Relief }.FirstOrDefault(m => m.Name == e.Name)?.Rank ?? int.MaxValue;

    private static (int Day, int Count, List<string> Keys) Read(Character character)
    {
        var parts = (PregnancyState.Text(character, LedgerKey) ?? "").Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var count))
            return (-1, 0, []);
        return (day, count, parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
    }
}
