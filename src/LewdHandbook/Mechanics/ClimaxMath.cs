namespace LewdHandbook.Mechanics;

internal enum ClimaxOutcomeKind
{
    Pending,
    HeldEdge,
    Climaxed,
    InstantClimax,
}

internal readonly record struct ClimaxSaveResult(
    ClimaxOutcomeKind Kind,
    int Successes,
    int Failures,
    int ArousalAfter,
    bool EdgingAfter,
    string Summary);

internal static class ClimaxMath
{
    public const int SaveDc = 15;

    public static ClimaxSaveResult ResolveSave(
        int d20,
        int inhibitionBonus,
        int successes,
        int failures,
        int arousalCurrent,
        int arousalMax)
    {
        d20 = Math.Clamp(d20, 1, 20);
        successes = Math.Max(0, successes);
        failures = Math.Max(0, failures);
        arousalMax = Math.Max(0, arousalMax);

        if (d20 == 20)
        {
            var held = Math.Max(0, arousalMax - 1);
            return new ClimaxSaveResult(
                ClimaxOutcomeKind.HeldEdge,
                0,
                0,
                held,
                EdgingAfter: false,
                Summary: $"Natural 20 on climax save — arousal set to {held}, edging cleared.");
        }

        if (d20 == 1)
        {
            failures += 2;
            return Finalize(successes, failures, arousalCurrent, arousalMax, $"Natural 1 — two climax failures (now {failures}).");
        }

        var total = d20 + inhibitionBonus;
        if (total >= SaveDc)
        {
            successes++;
            return Finalize(successes, failures, arousalCurrent, arousalMax,
                $"Climax save {d20}+{inhibitionBonus}={total} ≥ {SaveDc} — success #{successes}.");
        }

        failures++;
        return Finalize(successes, failures, arousalCurrent, arousalMax,
            $"Climax save {d20}+{inhibitionBonus}={total} < {SaveDc} — failure #{failures}.");
    }

    public static ClimaxSaveResult ApplyAutoFailures(
        int failuresToAdd,
        int successes,
        int failures,
        int arousalCurrent,
        int arousalMax,
        bool instantClimax)
    {
        if (instantClimax)
        {
            return new ClimaxSaveResult(
                ClimaxOutcomeKind.InstantClimax,
                0,
                0,
                arousalCurrent,
                EdgingAfter: false,
                Summary: "Stimulation ≥ arousal maximum — instant climax.");
        }

        failures = Math.Max(0, failures) + Math.Max(0, failuresToAdd);
        return Finalize(successes, failures, arousalCurrent, arousalMax,
            $"Automatic climax failure x{failuresToAdd} while at max arousal (failures={failures}).");
    }

    private static ClimaxSaveResult Finalize(
        int successes,
        int failures,
        int arousalCurrent,
        int arousalMax,
        string prefix)
    {
        if (failures >= 3)
        {
            return new ClimaxSaveResult(
                ClimaxOutcomeKind.Climaxed,
                0,
                0,
                arousalCurrent,
                EdgingAfter: false,
                Summary: prefix + " Third failure — climax.");
        }

        if (successes >= 3)
        {
            var held = Math.Max(0, arousalMax - 1);
            return new ClimaxSaveResult(
                ClimaxOutcomeKind.HeldEdge,
                0,
                0,
                held,
                EdgingAfter: false,
                Summary: prefix + $" Third success — arousal set to {held}, edging cleared.");
        }

        return new ClimaxSaveResult(
            ClimaxOutcomeKind.Pending,
            successes,
            failures,
            arousalCurrent,
            EdgingAfter: true,
            Summary: prefix);
    }
}
