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
}
