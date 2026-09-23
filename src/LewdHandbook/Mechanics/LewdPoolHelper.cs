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

        if (pool.Max <= 0 && defaultMax > 0)
            pool.Max = defaultMax;
        return pool;
    }

    public static void MirrorArousal(ModeParticipantState participant, ResourcePool arousal)
    {
        participant.State[LewdKeys.ArousalCurrentMirror] = arousal.Current;
        participant.State[LewdKeys.ArousalMaxMirror] = arousal.Max;
    }

    public static void SetEdging(ModeParticipantState participant, Character? character, bool edging)
    {
        participant.State[LewdKeys.Edging] = edging;
        if (!edging)
            participant.State[LewdKeys.EdgingBeats] = 0;
        if (character is null)
            return;

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

    public static void WriteClimaxCounters(ModeParticipantState participant, int successes, int failures)
    {
        participant.State[LewdKeys.ClimaxSuccesses] = successes;
        participant.State[LewdKeys.ClimaxFailures] = failures;
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

        if (character is null)
            return;

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

    public static void EnsureNamedCondition(Character character, string name, string? conditionName, string recoveryHint)
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
            AppliedBy = "lewd_overstim",
        });
    }

    private static bool IsOverstimEffect(StatusEffect e) =>
        string.Equals(e.ConditionName, LewdKeys.ConditionOverstimulation, StringComparison.OrdinalIgnoreCase) ||
        e.Name.StartsWith("Overstimulation ", StringComparison.OrdinalIgnoreCase);
}
