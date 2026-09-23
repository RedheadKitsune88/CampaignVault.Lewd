namespace LewdHandbook.Mechanics;

internal static class PregnancyMath
{
    public const int ProgressMax = 100;
    public const int RestPoisonDc = 15;
    public const int TraditionalLongRestDelta = 1;
    public const int NontraditionalLongRestDelta = 25;
    public const int ForcedHalfway = 50;

    public static int TraditionalDc(int targetConMod, int targetProficiency, bool condom) =>
        condom ? 25 : 10 + targetConMod + targetProficiency;

    public static int PickDie(int d20, int? second, bool advantage)
    {
        if (second is null)
            return d20;
        return advantage ? Math.Max(d20, second.Value) : Math.Min(d20, second.Value);
    }

    public static bool CheckSucceeds(int die, int modifier, int dc) => die + modifier >= dc;

    public static int ClampProgress(int value) => Math.Clamp(value, 0, ProgressMax);

    public static int DefaultRestDelta(bool nontraditional) =>
        nontraditional ? NontraditionalLongRestDelta : TraditionalLongRestDelta;
}
