using CampaignVault.Models;

namespace GoblinPonyriders.Mechanics;

/// <summary>
/// Namespaced piercing kinds for Unified Clans kits. Applied via core <c>piercing</c> follow-ups —
/// never a second jewelry list.
/// </summary>
internal static class GoblinPiercingKinds
{
    public const string HeavyNoseRing = "goblins.heavy_nose_ring";
    public const string EarNotchIron = "goblins.ear_notch_iron";
    public const string SeptumLead = "goblins.septum_lead";
    public const string JingleIron = "goblins.jingle_iron";
}

internal static class GoblinPiercingKits
{
    public const string AppliedBy = "com.campaignvault.goblins";

    /// <summary>Terror-release / naming: locked heavy iron through the septum.</summary>
    public static PiercingChange TerrorMark(string characterId) => new()
    {
        CharacterId = characterId,
        Action = "add",
        Site = PiercingSites.NoseSeptum,
        Kind = GoblinPiercingKinds.HeavyNoseRing,
        Material = "iron",
        Load = PiercingLoads.Heavy,
        Tags = [PiercingTags.Locked, PiercingTags.Fresh],
        Note = "Unified Clans terror / naming iron",
        AppliedBy = AppliedBy,
        Replace = true, // singular septum piece
    };

    /// <summary>Riding-Girl: septum lead ring (leashable).</summary>
    public static PiercingChange RidingSeptum(string characterId) => new()
    {
        CharacterId = characterId,
        Action = "add",
        Site = PiercingSites.NoseSeptum,
        Kind = GoblinPiercingKinds.SeptumLead,
        Material = "iron",
        Load = PiercingLoads.Light,
        Tags = [PiercingTags.LeashRing, PiercingTags.Locked],
        Note = "Riding-Girl septum lead",
        AppliedBy = AppliedBy,
        Replace = true,
    };

    /// <summary>Entertainer: bells on both nipples (intimate sites; Lewd hard limits still apply via skill).</summary>
    public static IReadOnlyList<PiercingChange> EntertainerBells(string characterId) =>
    [
        new()
        {
            CharacterId = characterId,
            Action = "add",
            Site = "nipple.left",
            Kind = GoblinPiercingKinds.JingleIron,
            Material = "iron",
            Load = PiercingLoads.Heavy,
            Tags = [PiercingTags.Bell, PiercingTags.Locked],
            Note = "Entertainer jingle — left",
            AppliedBy = AppliedBy,
            Replace = true,
        },
        new()
        {
            CharacterId = characterId,
            Action = "add",
            Site = "nipple.right",
            Kind = GoblinPiercingKinds.JingleIron,
            Material = "iron",
            Load = PiercingLoads.Heavy,
            Tags = [PiercingTags.Bell, PiercingTags.Locked],
            Note = "Entertainer jingle — right",
            AppliedBy = AppliedBy,
            Replace = true,
        },
    ];

    /// <summary>Naming ritual: locked iron ear notch (visible clan mark jewelry).</summary>
    public static PiercingChange NamingEar(string characterId) => new()
    {
        CharacterId = characterId,
        Action = "add",
        Site = PiercingSites.EarLobeLeft,
        Kind = GoblinPiercingKinds.EarNotchIron,
        Material = "iron",
        Load = PiercingLoads.None,
        Tags = [PiercingTags.Locked, PiercingTags.Fresh],
        Note = "Assigned-name iron notch",
        AppliedBy = AppliedBy,
        Replace = true,
    };
}
