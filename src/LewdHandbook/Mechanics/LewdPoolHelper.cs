using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class LewdPoolHelper
{
    public static ResourcePool EnsurePool(Character character, string poolName, int defaultMax, RecoveryType recovery)
    {
        var pools = character.SystemStats.ResourcePools;
        if (!pools.TryGetValue(poolName, out var pool) || pool is null)
        {
            pool = new ResourcePool
            {
                Current = poolName == LewdKeys.PoolArousal ? 0 : defaultMax,
                Max = Math.Max(0, defaultMax),
                Recovery = recovery,
            };
            pools[poolName] = pool;
            return pool;
        }

        // An existing pool keeps its Max even at 0: arousal max ≤ 0 is a bad-end condition, not a missing value.
        return pool;
    }

    /// <summary>The arousal pool, created if missing, with its maximum re-derived from the sheet.</summary>
    public static ResourcePool Arousal(Character character)
    {
        var pool = EnsurePool(character, LewdKeys.PoolArousal, ArousalMath.FallbackMax, RecoveryType.Never);
        SyncArousalMax(character, pool);
        return pool;
    }

    public static ResourcePool Numbing(Character character) =>
        EnsurePool(character, LewdKeys.PoolNumbing, 0, RecoveryType.Never);

    /// <summary>
    /// Sets the arousal maximum from the recovery die, Con and level (<see cref="ArousalMath"/>), less any Brand of
    /// Altruism suppression (floor 1). Without a recovery die or sexual history the stored maximum is kept, so a
    /// hand-set value survives. Current never exceeds the new maximum.
    /// </summary>
    public static void SyncArousalMax(Character character, ResourcePool? pool = null)
    {
        pool ??= character.SystemStats.ResourcePools.GetValueOrDefault(LewdKeys.PoolArousal);
        if (pool is null)
            return;

        var derived = ArousalMath.DerivedMax(character);
        var suppressed = PregnancyState.Int(character, BrandState.AltruismSuppressedKey);
        if (suppressed > 0)
        {
            var baseMax = derived ?? PregnancyState.Int(character, BrandState.AltruismBaseMaxKey);
            if (baseMax > 0)
                pool.Max = Math.Max(1, baseMax - suppressed);
        }
        else if (derived is not null)
        {
            pool.Max = derived.Value;
        }

        if (pool.Current > pool.Max && pool.Max > 0)
            pool.Current = pool.Max;
    }

    public static void MirrorArousal(ModeParticipantState participant, ResourcePool arousal)
    {
        participant.State[LewdKeys.ArousalCurrentMirror] = arousal.Current;
        participant.State[LewdKeys.ArousalMaxMirror] = arousal.Max;
    }

    public static void SetEdging(ModeParticipantState? participant, Character? character, bool edging)
    {
        if (participant is not null)
        {
            participant.State[LewdKeys.Edging] = edging;
            if (!edging)
            {
                participant.State[LewdKeys.EdgingBeats] = 0;
                participant.State[LewdKeys.ClimaxSuccesses] = 0;
                participant.State[LewdKeys.ClimaxFailures] = 0;
            }
        }

        if (character is null)
            return;

        // Handbook: the tally resets when arousal drops below maximum, i.e. when edging ends.
        if (!edging)
        {
            character.SystemStats.Attributes.Remove(SheetSuccessesKey);
            character.SystemStats.Attributes.Remove(SheetFailuresKey);
        }

        var effects = character.SystemStats.StatusEffects;
        var existing = effects.FirstOrDefault(e =>
            string.Equals(e.Name, LewdKeys.ConditionEdging, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.ConditionName, LewdKeys.ConditionEdging, StringComparison.OrdinalIgnoreCase));

        if (edging)
        {
            if (existing is null)
            {
                effects.Add(new StatusEffect
                {
                    Name = LewdKeys.ConditionEdging,
                    Category = "Condition",
                    ConditionName = LewdKeys.ConditionEdging,
                    RecoveryHint = "Cleared by holding the edge (3 climax successes / nat 20) or by climaxing.",
                });
            }
        }
        else if (existing is not null)
        {
            effects.Remove(existing);
        }
    }

    private const string SheetSuccessesKey = "lewd.climax_successes";
    private const string SheetFailuresKey = "lewd.climax_failures";

    /// <summary>
    /// The climax-save tally. Handbook: successes and failures are kept until arousal drops below maximum, however long
    /// that takes, so the sheet (not the scene) is the record. The participant copy is the fallback for sheetless callers.
    /// </summary>
    public static (int Successes, int Failures) ClimaxCounters(ModeParticipantState participant, Character? character)
    {
        if (character is not null)
        {
            var attrs = character.SystemStats.Attributes;
            if (attrs.TryGetValue(SheetSuccessesKey, out var s) | attrs.TryGetValue(SheetFailuresKey, out var f))
                return ((int)s, (int)f);
        }

        return (ConsentGate.GetInt(participant, LewdKeys.ClimaxSuccesses), ConsentGate.GetInt(participant, LewdKeys.ClimaxFailures));
    }

    public static void WriteClimaxCounters(ModeParticipantState participant, int successes, int failures, Character? character = null)
    {
        participant.State[LewdKeys.ClimaxSuccesses] = successes;
        participant.State[LewdKeys.ClimaxFailures] = failures;
        if (character is null)
            return;

        var attrs = character.SystemStats.Attributes;
        if (successes == 0 && failures == 0)
        {
            attrs.Remove(SheetSuccessesKey);
            attrs.Remove(SheetFailuresKey);
            return;
        }

        attrs[SheetSuccessesKey] = successes;
        attrs[SheetFailuresKey] = failures;
    }

    public static void SetOverstimulation(
        ModeParticipantState participant,
        Character? character,
        int level,
        string? sourceId,
        CampaignVault.Data.ChangeHandlers.IChangeContext? context = null)
    {
        level = Math.Clamp(level, 0, OverstimMath.MaxLevel);
        participant.State[LewdKeys.Overstimulation] = level;
        if (!string.IsNullOrWhiteSpace(sourceId))
            participant.State[LewdKeys.OverstimSource] = sourceId;

        if (level >= 3)
            participant.State[LewdKeys.Inhibition] = 0;
        if (level >= OverstimMath.MaxLevel)
            BadEndState.Apply(participant, character, BadEndMath.Overstim, sourceId, context: context);

        // A rescue reset the level to 0; do not write the stale one back.
        if (character is null || BadEndRescue.IsPending(character))
            return;

        SyncOverstimCascade(character, level, sourceId);
        var effects = character.SystemStats.StatusEffects;
        var existing = effects.FirstOrDefault(IsOverstimEffect);
        if (level <= 0)
        {
            if (existing is not null)
                effects.Remove(existing);
            return;
        }

        var name = $"Overstimulation {level}";
        if (existing is null)
        {
            effects.Add(new StatusEffect
            {
                Name = name,
                Category = "Condition",
                ConditionName = LewdKeys.ConditionOverstimulation,
                RecoveryHint = "Long rest reduces 1 level if no sexual stimulation while resting. Level 6 is Bad-Ended.",
                AppliedBy = sourceId ?? "lewd_overstim",
            });
            return;
        }

        existing.Name = name;
        existing.ConditionName = LewdKeys.ConditionOverstimulation;
        if (!string.IsNullOrWhiteSpace(sourceId))
            existing.AppliedBy = sourceId;
    }

    /// <summary>Longest a climax incapacitation lasts once the clock moves on: the campaign clock has hour resolution.</summary>
    internal const float ClimaxIncapDays = 1f / 24f;

    /// <summary>The host's "cannot act" StatModifiers key (SDK 0.9.0 <c>ActionBlock.Tag</c>).</summary>
    private const string BlocksAllActionsTag = ActionBlock.Tag;

    /// <summary>
    /// Stamps the "cannot act" status for a climax (incapacitated; stunned/paralyzed on a repeat climax replace it in
    /// the rules but all three block). It carries the host's block tag and an expiry, so it has an exit even when no
    /// scene turn ever ticks it: out of a scene the clock moving on an hour lifts it. Scene turns and scene end clear it
    /// sooner (<see cref="EndClimaxIncapacitation"/>).
    /// </summary>
    public static void StampClimaxIncapacitation(Character character, string name, double? nowDays)
    {
        EnsureNamedCondition(character, name, name,
            name == LewdKeys.ConditionIncapacitated
                ? "Climax: incapacitated for one round per recovery die spent, or until the end of their next turn. Con save (lewd_recover save=true) may end it."
                : "Repeated climax while still incapacitated. Ends with the climax incapacitation.",
            nowDays is { } now ? (float)(now + ClimaxIncapDays) : null);
    }

    public static void EnsureNamedCondition(
        Character character, string name, string? conditionName, string recoveryHint, float? expiresAtDay = null)
    {
        var effects = character.SystemStats.StatusEffects;
        if (effects.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        effects.Add(new StatusEffect
        {
            Name = name,
            Category = "Condition",
            ConditionName = conditionName,
            RecoveryHint = recoveryHint,
            AppliedBy = OverstimAppliedBy,
            ExpiresAtDay = expiresAtDay,
            StatModifiers = expiresAtDay is null ? [] : new Dictionary<string, float> { [BlocksAllActionsTag] = 1 },
        });
    }

    internal const string OverstimAppliedBy = "lewd_overstim";

    /// <summary>
    /// Cascade conditions for an overstimulation level: 1+ intoxicated, 2+ hyperaroused, 4+ infatuated (by the source).
    /// Adds what the level implies and removes the ones this cascade stamped that it no longer implies.
    /// </summary>
    public static void SyncOverstimCascade(Character character, int level, string? sourceId = null)
    {
        var cascade = new (int Min, string Name, string Hint)[]
        {
            (1, LewdKeys.ConditionIntoxicated, "Overstimulation 1+: Intoxicated."),
            (2, LewdKeys.ConditionHyperaroused, "Overstimulation 2+: Hyperaroused."),
            (4, LewdKeys.ConditionInfatuated, "Overstimulation 4+: Infatuated by the source of overstimulation."),
        };
        var effects = character.SystemStats.StatusEffects;
        foreach (var (min, name, hint) in cascade)
        {
            if (level >= min)
            {
                EnsureNamedCondition(character, name, name, hint);
                continue;
            }

            effects.RemoveAll(e =>
                string.Equals(e.AppliedBy, OverstimAppliedBy, StringComparison.Ordinal) &&
                string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        if (level >= 4 && !string.IsNullOrWhiteSpace(sourceId))
        {
            var infatuated = effects.FirstOrDefault(e =>
                string.Equals(e.AppliedBy, OverstimAppliedBy, StringComparison.Ordinal) &&
                string.Equals(e.Name, LewdKeys.ConditionInfatuated, StringComparison.OrdinalIgnoreCase));
            if (infatuated is not null)
                infatuated.RecoveryHint = $"Overstimulation 4+: Infatuated by {sourceId}.";
        }
    }

    /// <summary>Level from the sheet's <c>Overstimulation N</c> status (host long rest decrements it).</summary>
    public static int SheetOverstimLevel(Character character)
    {
        var existing = character.SystemStats.StatusEffects.FirstOrDefault(IsOverstimEffect);
        if (existing?.Name is null)
            return 0;
        var parts = existing.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[^1], out var n) ? n : 0;
    }

    /// <summary>Ends climax incapacitation: resets the streak and drops the stunned/paralyzed it stamped.</summary>
    public static void EndClimaxIncapacitation(ModeParticipantState? participant, Character? character)
    {
        if (participant is not null)
        {
            participant.State[LewdKeys.ClimaxIncapacitated] = false;
            participant.State[LewdKeys.ClimaxIncapTurns] = 0;
            participant.State[LewdKeys.ClimaxStreak] = 0;
        }

        character?.SystemStats.StatusEffects.RemoveAll(e =>
            string.Equals(e.AppliedBy, OverstimAppliedBy, StringComparison.Ordinal) &&
            (string.Equals(e.Name, LewdKeys.ConditionStunned, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.Name, LewdKeys.ConditionParalyzed, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.Name, LewdKeys.ConditionIncapacitated, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Whether a climax-stamped incapacitated / stunned / paralyzed is on the sheet.</summary>
    public static bool HasClimaxIncapacitation(Character character) =>
        character.SystemStats.StatusEffects.Any(e =>
            string.Equals(e.AppliedBy, OverstimAppliedBy, StringComparison.Ordinal) &&
            (string.Equals(e.Name, LewdKeys.ConditionIncapacitated, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.Name, LewdKeys.ConditionStunned, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.Name, LewdKeys.ConditionParalyzed, StringComparison.OrdinalIgnoreCase)));

    private static bool IsOverstimEffect(StatusEffect e) =>
        string.Equals(e.ConditionName, LewdKeys.ConditionOverstimulation, StringComparison.OrdinalIgnoreCase) ||
        e.Name.StartsWith("Overstimulation ", StringComparison.OrdinalIgnoreCase);
}
