namespace LewdHandbook.Mechanics;

internal readonly record struct OverstimClimaxTick(
    int ClimaxStreak,
    int OverstimulationAfter,
    bool OverstimIncreased,
    string? IncapacitationCondition);

internal static class OverstimMath
{
    public const int MaxLevel = 6;
    public const int BeatsPerEdgingHour = 10;

    public static OverstimClimaxTick OnClimax(int priorStreak, bool wasIncapacitated, int overstimBefore)
    {
        var streak = wasIncapacitated ? Math.Max(1, priorStreak) + 1 : 1;
        var os = Math.Clamp(overstimBefore, 0, MaxLevel);
        var increased = false;
        if (wasIncapacitated && streak > 3 && os < MaxLevel)
        {
            os++;
            increased = true;
        }

        var extra = streak >= 3 ? LewdKeys.ConditionParalyzed
            : streak >= 2 ? LewdKeys.ConditionStunned
            : null;
        return new OverstimClimaxTick(streak, os, increased, extra);
    }

    public static int? ExtendedEdgingOverstim(int edgingBeatsAfterIncrement, int inhibitionBonus, int overstimBefore)
    {
        if (edgingBeatsAfterIncrement <= 0)
            return null;

        var hours = edgingBeatsAfterIncrement / BeatsPerEdgingHour;
        var prevHours = (edgingBeatsAfterIncrement - 1) / BeatsPerEdgingHour;
        if (hours <= prevHours)
            return null;

        var allowed = Math.Max(0, inhibitionBonus);
        if (hours <= allowed)
            return null;

        return Math.Min(MaxLevel, Math.Max(0, overstimBefore) + 1);
    }
}
