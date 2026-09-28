namespace GoblinPonyriders.Mechanics;

internal static class GoblinKeys
{
    public const string PluginId = "com.campaignvault.goblins";
    public const string TraitPrefix = PluginId + ".";

    public const string ClansOption = "goblinClans";
    public const string HardLimitsOption = "goblinHardLimits";
    public const string OptionOn = "on";
    public const string OptionOff = "off";

    public const string FactionId = "factions/unified_clans";
    public const string FactionName = "Unified Clans";

    public const string Defiance = TraitPrefix + "defiance";
    public const string ClanMarkLevel = TraitPrefix + "clan_mark";
    public const string Capture = TraitPrefix + "capture";
    public const string Role = TraitPrefix + "role";
    public const string TrainingPhase = TraitPrefix + "training_phase";
    public const string TrainingDay = TraitPrefix + "training_day";
    public const string AssignedName = TraitPrefix + "assigned_name";
    public const string Loyalty = TraitPrefix + "loyalty";
    public const string TerrorReleased = TraitPrefix + "terror_released";

    public const string AppliedBy = "goblin_ponyriders";

    public const string ConditionCapture = "capture_state";
    public const string ConditionClanMark = "clan_mark";

    public static readonly string[] HardLimitProbe =
        ["capture", "pony", "ponyrider", "breeding", "clan_mark", "goblin", "terror_release", "training"];
}
