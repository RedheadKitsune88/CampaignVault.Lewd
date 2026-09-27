namespace LewdHandbook.Events;

/// <summary>
/// Domain-event topics this plugin publishes. Other plugins subscribe by string topic
/// (no assembly reference). Prefix must match <c>plugin.json</c> id.
/// </summary>
public static class LewdEvents
{
    public const string SourcePrefix = "com.campaignvault.lewd-handbook.";

    public const string Climax = SourcePrefix + "climax.v1";
    public const string BadEnd = SourcePrefix + "bad_end.v1";
    public const string BrandChanged = SourcePrefix + "brand_changed.v1";
    public const string ViceState = SourcePrefix + "vice_state.v1";
    public const string ImprintChanged = SourcePrefix + "imprint_changed.v1";
    public const string Pregnancy = SourcePrefix + "pregnancy.v1";
    public const string BindingChanged = SourcePrefix + "binding_changed.v1";

    public static readonly string[] All =
    [
        Climax,
        BadEnd,
        BrandChanged,
        ViceState,
        ImprintChanged,
        Pregnancy,
        BindingChanged,
    ];

    public static class Fields
    {
        public const string CharacterId = "characterId";
        public const string Outcome = "outcome";
        public const string Forced = "forced";
        public const string InEncounter = "inEncounter";
        public const string Reason = "reason";
        public const string Consequence = "consequence";
        public const string ViceId = "viceId";
        public const string ImprintTrack = "imprintTrack";
        public const string ImprintJump = "imprintJump";
        public const string BrandId = "brandId";
        public const string Action = "action";
        public const string Tier = "tier";
        public const string State = "state";
        public const string Dc = "dc";
        public const string Category = "category";
        public const string Level = "level";
    }
}
