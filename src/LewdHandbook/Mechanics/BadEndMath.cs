namespace LewdHandbook.Mechanics;

internal readonly record struct BadEndProbe(
    bool AlreadyMarked,
    int Overstimulation,
    bool Climaxed,
    bool WasEdging,
    bool HasRecoveryPool,
    int RecoveryCurrent,
    bool HasArousalPool,
    int ArousalMax,
    int? CurrentHp);

internal readonly record struct BadEndDecision(bool Mark, string? Reason, bool PromptDefeat, bool MissingRecoveryPool);

internal static class BadEndMath
{
    public const string Overstim = "overstim";
    public const string EmptyRecovery = "empty_recovery";
    public const string ArousalMax = "arousal_max";
    public const string CaptureImpreg = "capture_impreg";
    public const string Defeat = "defeat";
    public const string Explicit = "explicit";

    public static bool IsVerbReason(string? reason) =>
        string.Equals(reason, Defeat, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(reason, Explicit, StringComparison.OrdinalIgnoreCase);

    public static BadEndDecision Evaluate(BadEndProbe probe)
    {
        var prompt = probe.CurrentHp is <= 0 && !probe.AlreadyMarked;
        if (probe.AlreadyMarked)
            return new BadEndDecision(false, null, false, false);

        if (probe.Overstimulation >= OverstimMath.MaxLevel)
            return new BadEndDecision(true, Overstim, false, false);

        if (probe.Climaxed && probe.WasEdging)
        {
            if (!probe.HasRecoveryPool)
                return new BadEndDecision(false, null, prompt, true);
            if (probe.RecoveryCurrent <= 0)
                return new BadEndDecision(true, EmptyRecovery, false, false);
        }

        if (probe.HasArousalPool && probe.ArousalMax <= 0)
            return new BadEndDecision(true, ArousalMax, false, false);

        return new BadEndDecision(false, null, prompt, false);
    }
}
