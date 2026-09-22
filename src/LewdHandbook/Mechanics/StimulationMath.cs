namespace LewdHandbook.Mechanics;

internal readonly record struct StimulationResult(
    int NumbingBefore,
    int NumbingAfter,
    int ArousalBefore,
    int ArousalAfter,
    int AbsorbedByNumbing,
    int AppliedToArousal,
    bool WasAlreadyAtMax,
    bool InstantClimax,
    int AutoClimaxFailures);

internal static class StimulationMath
{
    /// <summary>
    /// Apply stimulation: numbing absorbs first; leftover raises arousal (clamped to max).
    /// Stimulation while already at max yields automatic climax failures (handbook).
    /// Leftover stim magnitude ≥ arousal max → instant climax.
    /// </summary>
    public static StimulationResult Apply(
        int arousalCurrent,
        int arousalMax,
        int numbingCurrent,
        int stimulation,
        bool isCritical = false)
    {
        if (arousalMax < 0)
            arousalMax = 0;
        arousalCurrent = Math.Max(0, arousalCurrent);
        numbingCurrent = Math.Max(0, numbingCurrent);
        stimulation = Math.Max(0, stimulation);

        var numbingBefore = numbingCurrent;
        var arousalBefore = arousalCurrent;
        var absorbed = Math.Min(numbingCurrent, stimulation);
        numbingCurrent -= absorbed;
        var leftover = stimulation - absorbed;

        var alreadyAtMax = arousalMax > 0 && arousalBefore >= arousalMax;
        var autoFails = 0;
        var instant = false;
        var applied = 0;

        if (leftover <= 0)
        {
            return new StimulationResult(
                numbingBefore, numbingCurrent, arousalBefore, arousalCurrent,
                absorbed, 0, alreadyAtMax, false, 0);
        }

        if (arousalMax > 0 && leftover >= arousalMax)
            instant = true;

        if (alreadyAtMax)
        {
            autoFails = isCritical ? 2 : 1;
            return new StimulationResult(
                numbingBefore, numbingCurrent, arousalBefore, arousalCurrent,
                absorbed, 0, true, instant, autoFails);
        }

        var next = arousalBefore + leftover;
        if (arousalMax > 0)
            next = Math.Min(next, arousalMax);
        applied = next - arousalBefore;
        arousalCurrent = next;

        return new StimulationResult(
            numbingBefore, numbingCurrent, arousalBefore, arousalCurrent,
            absorbed, applied, false, instant, 0);
    }

    /// <summary>Numbing does not stack; keep existing or replace with incoming.</summary>
    public static int ChooseNumbing(int existing, int incoming, bool preferIncoming) =>
        preferIncoming ? Math.Max(0, incoming) : Math.Max(0, existing);

    public static int LongRestArousalReduction(int arousalCurrent, int arousalMax)
    {
        var reducedBy = arousalMax / 2;
        return Math.Max(0, arousalCurrent - reducedBy);
    }
}
