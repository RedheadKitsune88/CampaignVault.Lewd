namespace GoblinPonyriders.Events;

/// <summary>
/// Domain-event topics this plugin publishes. Subscribe by string topic (no assembly reference).
/// Prefix must match <c>plugin.json</c> id.
/// </summary>
public static class GoblinEvents
{
    public const string SourcePrefix = "com.campaignvault.goblins.";

    public const string Capture = SourcePrefix + "capture.v1";
    public const string Defiance = SourcePrefix + "defiance.v1";
    public const string ClanMark = SourcePrefix + "clan_mark.v1";
    public const string Role = SourcePrefix + "role.v1";
    public const string Training = SourcePrefix + "training.v1";
    public const string Release = SourcePrefix + "release.v1";
    public const string Name = SourcePrefix + "name.v1";

    /// <summary>LewdHandbook topics this plugin listens to (string-only; Lewd need not be referenced).</summary>
    public static class LewdTopics
    {
        public const string BindingChanged = "com.campaignvault.lewd-handbook.binding_changed.v1";
        public const string ImprintChanged = "com.campaignvault.lewd-handbook.imprint_changed.v1";
        public const string Climax = "com.campaignvault.lewd-handbook.climax.v1";
        public const string BadEnd = "com.campaignvault.lewd-handbook.bad_end.v1";
        public const string Humiliated = "com.campaignvault.lewd-handbook.humiliated.v1";
    }

    public static class Fields
    {
        public const string CharacterId = "characterId";
        public const string Action = "action";
        public const string Delta = "delta";
        public const string Level = "level";
        public const string Role = "role";
        public const string Phase = "phase";
        public const string Reason = "reason";
        public const string AssignedName = "assignedName";
        public const string FactionId = "factionId";
    }
}
