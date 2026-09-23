namespace LewdHandbook.Mechanics;

internal readonly record struct ViceDef(
    string Id,
    string Kind,
    int BaseDc,
    string DefaultAbility,
    int WithdrawalHours,
    string AddictedHint,
    string WithdrawalFailHint);

internal static class ViceCatalog
{
    public const string Sex = "sex";
    public const string SexualFluids = "sexual_fluids";
    public const string Alcohol = "alcohol";
    public const string SuccubusVenom = "succubus_venom";

    public const string AppliedBy = "lewd_vice";
    public const int WeekHours = 24 * 7;

    private static readonly ViceDef[] Vices =
    [
        new(Sex, "psychological", 8, "cha", 24,
            "Always hyperaroused; nymphomanic while arousal is above half maximum. Disadvantage on Cha checks/saves while affected or in presence; disadvantage vs spells/effects/persuasion involving the vice.",
            "Withdrawal failure adds overstimulation (nat 1 = +2), not exhaustion."),
        new(SexualFluids, "psychological", 18, "cha", 24,
            "Sexual-fluids hunger (Brand of Addiction line). Disadvantage on Cha checks/saves while affected or in presence; disadvantage vs spells/effects/persuasion involving the vice.",
            "Withdrawal failure adds overstimulation (nat 1 = +2), not exhaustion."),
        new(Alcohol, "complex", 10, "con", 4,
            "Must partake every 4 hours or become intoxicated. Disadvantage on the chosen addiction-save ability while affected or in presence.",
            "Withdrawal failure adds exhaustion (+2 on nat 1)."),
        new(SuccubusVenom, "chemical", 14, "con", 24,
            "Disadvantage vs charmed and infatuated. Disadvantage on Con checks/saves while affected or in presence.",
            "Withdrawal failure stamps Denied."),
    ];

    public static IReadOnlyList<ViceDef> All => Vices;

    public static bool TryGet(string? id, out ViceDef def)
    {
        var key = Normalize(id);
        foreach (var vice in Vices)
        {
            if (vice.Id == key)
            {
                def = vice;
                return true;
            }
        }

        def = default;
        return false;
    }

    public static string Normalize(string? id) => (id ?? "").Trim().ToLowerInvariant();

    public static string ConditionName(string id) => "vice_" + Normalize(id);

    public static string EffectName(string id) => "Vice: " + Normalize(id);

    public static string AbilityFor(ViceDef def, string? requested)
    {
        var a = Normalize(requested);
        if (def.Kind == "complex")
            return a is "con" or "wis" or "cha" ? a : def.DefaultAbility;
        return def.Kind switch
        {
            "chemical" => "con",
            "magical" => "wis",
            "psychological" => "cha",
            _ => a is "con" or "wis" or "cha" ? a : def.DefaultAbility,
        };
    }
}
