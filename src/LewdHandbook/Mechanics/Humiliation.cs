using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Engine-owned shame bite for <c>lewd_humiliate</c>: willpower drain + timed untiered status (always), optional
/// arousal only when ordeal (masochism) imprint ≥1. Inhibition is never an input. Mirrors <see cref="LewdMood"/>
/// guards: setting, daily cap, one live effect (stronger replaces weaker), same source+tags hash once per day.
/// </summary>
internal static class Humiliation
{
    public const string AppliedBy = "lewd_humiliate";
    public const string EffectKey = "lewd_humiliate";
    public const int DailyCap = 3;
    private const string LedgerKey = LewdKeys.ModeTraitPrefix + "humiliate.ledger";

    public sealed record Tier(int Severity, string Name, float Drain, double Hours, Dictionary<string, float> Modifiers, string Hint);

    public static readonly Tier[] Tiers =
    [
        new(1, "Humiliated", 2f, 4, new() { ["Charisma"] = -1 }, "The flush fades within a few hours."),
        new(2, "Humiliated", 4f, 8, new() { ["Charisma"] = -1, ["Wisdom"] = -1 }, "Rest or a quiet day; it fades within ~8 hours."),
        new(3, "Deeply humiliated", 6f, 24, new() { ["Charisma"] = -2, ["Wisdom"] = -1 }, "A long rest, or a calm day away from the crowd."),
    ];

    public static readonly string[] HardLimitTags = ["humiliation", "shame"];

    public static Tier For(int severity) =>
        Tiers[Math.Clamp(severity, 1, 3) - 1];

    /// <summary>Hash of source + sorted tags for the once-per-day duplicate guard.</summary>
    public static string ScopeKey(string? sourceId, IEnumerable<string>? tags)
    {
        var tagPart = tags is null
            ? ""
            : string.Join(",", tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()).OrderBy(t => t));
        // Use '#' so the ledger line (day|count|key,key) can split on '|' safely.
        return $"{(sourceId ?? "").Trim().ToLowerInvariant()}#{tagPart}";
    }

    /// <summary>
    /// Stamps shame status + willpower when guards allow. Returns null when refused (caller should still treat as
    /// a soft skip with a message, not a hard failure — except setting-off which the handler fails).
    /// </summary>
    public static string? TryApplyShame(
        Character character,
        int severity,
        string scope,
        int day,
        double nowDays,
        LewdSettings settings,
        out float drained)
    {
        drained = 0;
        if (!settings.Humiliation)
            return null;

        var tier = For(severity);
        var (ledgerDay, count, keys) = ReadLedger(character);
        if (ledgerDay != day)
            (count, keys) = (0, []);
        if (count >= DailyCap || keys.Contains(scope, StringComparer.OrdinalIgnoreCase))
            return null;

        var effects = character.SystemStats.StatusEffects;
        var live = effects.FirstOrDefault(e =>
            string.Equals(e.EffectKey, EffectKey, StringComparison.OrdinalIgnoreCase) &&
            (e.ExpiresAtDay is null || e.ExpiresAtDay > nowDays));
        var liveRank = live is null ? 0 : RankOf(live);
        if (live is not null && liveRank > tier.Severity)
            return null;

        effects.RemoveAll(e => string.Equals(e.EffectKey, EffectKey, StringComparison.OrdinalIgnoreCase));
        effects.Add(new StatusEffect
        {
            Name = tier.Name,
            Category = "Condition",
            AppliedBy = AppliedBy,
            EffectKey = EffectKey,
            StatModifiers = new Dictionary<string, float>(tier.Modifiers),
            RecoveryHint = tier.Hint,
            ExpiresAtDay = (float)(nowDays + tier.Hours / 24.0),
        });

        drained = Willpower.Drain(character, tier.Drain);
        keys.Add(scope);
        PregnancyState.Set(character, LedgerKey, $"{day}|{count + 1}|{string.Join(",", keys)}");

        return $"{character.Name}: {tier.Name} " +
               $"({string.Join(", ", tier.Modifiers.Select(kv => $"{kv.Key} {kv.Value:+0.##;-0.##}"))} for {tier.Hours:0.#}h)" +
               (drained > 0 ? $"; willpower −{drained:0.#} ({character.SystemStats.Willpower:0.#} left)." : ".");
    }

    /// <summary>
    /// Ordeal-gated arousal from shame. Severity 1 never arouses. Inhibition is not consulted.
    /// </summary>
    public static int ArousalDelta(int severity, int ordealLevel, int d4Face)
    {
        if (severity <= 1 || ordealLevel < 1)
            return 0;
        var face = Math.Clamp(d4Face, 1, 4);
        return severity switch
        {
            2 => face,
            3 when ordealLevel == 1 => face,
            3 => face + ordealLevel,
            _ => 0,
        };
    }

    /// <summary>Marks recent shame for the slow ordeal-climb assist (TTL ~1 campaign day).</summary>
    public static void MarkRecent(Character character, int day) =>
        PregnancyState.Set(character, LewdKeys.TraitRecentHumiliate, day.ToString());

    public static bool HasRecent(Character character, int day)
    {
        var stamped = PregnancyState.Int(character, LewdKeys.TraitRecentHumiliate);
        return stamped > 0 && day - stamped <= 1;
    }

    public static void ClearRecent(Character character)
    {
        character.SystemStats.Traits.Remove(LewdKeys.TraitRecentHumiliate);
        character.SystemStats.Traits.Remove(LewdKeys.TraitRecentPain);
        character.SystemStats.Traits.Remove(LewdKeys.TraitPendingOrdealFromPain);
        character.SystemStats.Traits.Remove(LewdKeys.TraitOrdealNudgeDay);
    }

    private static int RankOf(StatusEffect e)
    {
        if (e.Name.StartsWith("Deeply", StringComparison.OrdinalIgnoreCase))
            return 3;
        if (e.StatModifiers.TryGetValue("Wisdom", out var w) && w < 0)
            return 2;
        return 1;
    }

    private static (int Day, int Count, List<string> Keys) ReadLedger(Character character)
    {
        var parts = (PregnancyState.Text(character, LedgerKey) ?? "").Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var count))
            return (-1, 0, []);
        return (day, count, parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
    }
}

/// <summary>Recent pain / ordeal-climb assist flags (LLM commits imprint; engine only nudges).</summary>
internal static class OrdealClimb
{
    public static void MarkPain(Character character, int day) =>
        PregnancyState.Set(character, LewdKeys.TraitRecentPain, day.ToString());

    public static bool HasRecentPain(Character character, int day)
    {
        var stamped = PregnancyState.Int(character, LewdKeys.TraitRecentPain);
        return stamped > 0 && day - stamped <= 1;
    }

    /// <summary>
    /// After a climax close to recent pain/shame: one RecordMessage nudge per character per day toward
    /// <c>lewd_imprint</c> ordeal. Never auto-levels.
    /// </summary>
    public static string? MaybeNudge(Character character, int day, IChangeContext? context)
    {
        if (!HasRecentPain(character, day) && !Humiliation.HasRecent(character, day))
            return null;
        var nudged = PregnancyState.Int(character, LewdKeys.TraitOrdealNudgeDay);
        if (nudged == day)
            return null;

        PregnancyState.Set(character, LewdKeys.TraitPendingOrdealFromPain, "1");
        PregnancyState.Set(character, LewdKeys.TraitOrdealNudgeDay, day.ToString());
        var msg =
            $"{character.Id}: pain/shame and climax landed close — consider lewd_imprint category=ordeal with a small points tick (no auto-level).";
        context?.RecordMessage(msg);
        return msg;
    }

    /// <summary>Receiver-side pain stim when ordeal ≥1: +ordeal, cap +3.</summary>
    public static int ReceiverPainBonus(Character? target, IEnumerable<string>? tags)
    {
        if (target is null || !ImprintMath.IsSuffering(tags, targetOverstim: 0))
            return 0;
        var ordeal = ImprintState.Level(target, "ordeal");
        return ordeal < 1 ? 0 : Math.Min(3, ordeal);
    }
}
