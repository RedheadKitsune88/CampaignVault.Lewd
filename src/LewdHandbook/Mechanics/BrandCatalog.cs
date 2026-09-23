namespace LewdHandbook.Mechanics;

internal readonly record struct BrandDef(string Id, string Title, int Tier, string Hint);

internal static class BrandCatalog
{
    public const string Abundance = "abundance";
    public const string Addiction = "addiction";
    public const string Altruism = "altruism";
    public const string Bestial = "bestial";
    public const string Betrayal = "betrayal";
    public const string Denial = "denial";
    public const string Echoes = "echoes";
    public const string Emptiness = "emptiness";
    public const string FalseDominance = "false_dominance";
    public const string Fertility = "fertility";
    public const string Infatuation = "infatuation";
    public const string Oaths = "oaths";
    public const string Ruin = "ruin";
    public const string Obedience = "obedience";
    public const string Forsaken = "forsaken";
    public const string HungryGaze = "hungry_gaze";
    public const string Transformation = "transformation";

    public const int MaxTier = 5;
    public const string AppliedBy = "lewd_apply_brand";

    private static readonly BrandDef[] Brands =
    [
        new(Abundance, "Abundance", 1, "Rest without climax raises endowment by 1 (−1 AC and Dex saves). Climax produces 1 liter and reduces that penalty by 1."),
        new(Addiction, "Addiction", 1, "Sexual-fluids vice DC 18, locked while this brand remains. Long-rest benefits need 8 oz of sexual fluids. Craving/saves/withdrawal use lewd_vice."),
        new(Altruism, "Altruism", 1, "Healing another reduces arousal max by the same amount until a short rest. Con save or hyperaroused. Stabilizing raises tier, max 5."),
        new(Bestial, "Bestial Instinct", 4, "Short or long rest stamps nymphomanic until an unprotected climax."),
        new(Betrayal, "Betrayal", 5, "Nymphomanic within 10 ft of the stored foe type. Their climax infatuates the bearer for 1d6 hours. Distance is not known to the engine."),
        new(Denial, "Denial", 5, "Denied until a stored release condition is met. Release lasts until the next rest, heal, advance, or climax commit."),
        new(Echoes, "Echoes", 2, "Stim inflicted on another echoes as psychic stim. Cannot self-climax. A climax within 5 ft forces one — distance is not known to the engine."),
        new(Emptiness, "Emptiness", 1, "No penalty for larger penetration. Disadvantage on saves, attacks, and checks while not penetrated by a larger implement."),
        new(FalseDominance, "False Dominance", 1, "Narrate Demitri's Demanding Desire 1/long rest. Auto-fail charm, infatuated, and hyperaroused from a submissive source. The engine does not grant the spell."),
        new(Fertility, "Fertility", 2, "Hyperfertile. Climax only through unprotected sex. Hyperaroused while pregnant. Extra pregnancies −1 Str and Dex each."),
        new(Infatuation, "Infatuation", 3, "Infatuated by the stored source. Cannot be removed while this brand remains."),
        new(Oaths, "Oaths", 2, "An explicit vow binds. Breaking it compels public sexual humiliation, or nymphomanic and −1 arousal max per day. The engine stores the vow; it does not judge the breach."),
        new(Ruin, "Ruin", 1, "A would-be climax instead spends one recovery die and reduces arousal by the die plus tier, as psychic damage. No dice: 1d12 psychic and +1 overstimulation. HP 0 becomes 1 and raises tier."),
        new(Obedience, "Obedience", 5, "Infatuated by the source and cannot willingly move more than 1 mile from them. While overstimulated, DC 20 Wis or dominated."),
        new(Forsaken, "The Forsaken", 3, "Touching True Love poisons the bearer and deals 1d6 radiant at the start of each turn. The engine does not detect that touch."),
        new(HungryGaze, "The Hungry Gaze", 3, "Disadvantage on Stealth and hiding. Viewers make DC 15 Wis or become sexually aggressive until climax. Charm-immune creatures succeed."),
        new(Transformation, "Transformation", 5, "Stored trigger. DC 18 Con or a hybrid form for 1 hour: nymphomanic, advantage on sexual advances, disadvantage vs charm or domination."),
    ];

    public static IReadOnlyList<BrandDef> All => Brands;

    public static bool TryGet(string? id, out BrandDef def)
    {
        var key = id?.Trim().ToLowerInvariant();
        foreach (var brand in Brands)
        {
            if (string.Equals(brand.Id, key, StringComparison.Ordinal))
            {
                def = brand;
                return true;
            }
        }

        def = default;
        return false;
    }

    public static string ConditionName(string id) => "lustbrand:" + id;

    public static string EffectName(BrandDef def) => "Lustbrand: " + def.Title;
}
