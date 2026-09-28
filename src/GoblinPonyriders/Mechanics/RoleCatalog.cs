namespace GoblinPonyriders.Mechanics;

internal sealed record GoblinRole(
    string Id,
    string DisplayName,
    string Summary,
    /// <summary>Suggested LewdHandbook bind/posture/occupancy preset — never a parallel bondage engine.</summary>
    string LewdBindHint,
    bool FieldMount);

internal static class RoleCatalog
{
    public static readonly GoblinRole RidingGirl = new(
        "riding_girl",
        "Riding-Girl",
        "High-trust upright/bent-seat mount for scouts and raiders. Throws nets, yanks reins, living transport.",
        "lewd_bind: collar+reins, bit/ring gag, wrist-to-collar short chains, hoof-boot posture upright/bent-seat; " +
        "optional lewd_insert carved shaft. Core piercing: goblins.septum_lead (leash_ring). Never quadrupedal in the field.",
        FieldMount: true);

    public static readonly GoblinRole Domestic = new(
        "domestic",
        "Domestic Slave",
        "Camp/village chores and service. Light restraints once proven.",
        "lewd_bind: heavy collar+leash, ankle hobble; gag only if talking back.",
        FieldMount: false);

    public static readonly GoblinRole Entertainer = new(
        "entertainer",
        "Entertainer",
        "Public use, races, non-maiming pain theater. Heavy jingle harness.",
        "lewd_bind: full quadrupedal harness (bitchsuit/jingle), bells, muzzle; lewd_insert oversized shaft/beads. " +
        "Core piercing: goblins.jingle_iron on both nipples (load heavy, tag bell) — public notice → lewd_humiliate.",
        FieldMount: false);

    public static readonly GoblinRole HeavyLabor = new(
        "heavy_labor",
        "Heavy Labor",
        "Mill / bellows / miner-cart punishment labor under whip.",
        "lewd_bind: yoke or wrist-to-beam, ankle chain, hood optional; lewd_insert active during work.",
        FieldMount: false);

    public static readonly GoblinRole SlaveWarrior = new(
        "slave_warrior",
        "Slave Warrior",
        "Martial promotion under owner escort. Semi-free hands for a crude weapon.",
        "lewd_bind: collar, light hobble, wrists to belt (not mitted); no bitchsuit.",
        FieldMount: false);

    public static readonly GoblinRole PetCaster = new(
        "pet_caster",
        "Pet Caster",
        "Caster captive; hands freed only under guard for casting, otherwise entertainer-grade restraint.",
        "Off-duty: entertainer harness. Casting: free wrists, short collar chains, muzzle loosened; disadv concentration.",
        FieldMount: false);

    public static readonly GoblinRole Breeder = new(
        "breeder",
        "Breeder / Broodmare",
        "Immobilized breeding stock; retention plugs; pregnancy via LewdHandbook rules.",
        "lewd_bind: quadrupedal stocks/stake; lewd_insert double retention plugs; breeding muzzle.",
        FieldMount: false);

    public static readonly GoblinRole CockSleeve = new(
        "cock_sleeve",
        "Cock-Sleeve Transport",
        "Punishment crawl / living transport sleeve during marches.",
        "lewd_bind: quadrupedal harness, anal hook chains, muzzle hood, wrist-to-collar; no upright posture.",
        FieldMount: false);

    public static readonly IReadOnlyList<GoblinRole> All =
    [
        RidingGirl, Domestic, Entertainer, HeavyLabor, SlaveWarrior, PetCaster, Breeder, CockSleeve,
    ];

    public static bool TryGet(string? id, out GoblinRole role)
    {
        role = All.FirstOrDefault(r =>
            string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r.DisplayName, id, StringComparison.OrdinalIgnoreCase))!;
        return role is not null;
    }

    public static string ListIds() => string.Join(", ", All.Select(r => r.Id));
}
