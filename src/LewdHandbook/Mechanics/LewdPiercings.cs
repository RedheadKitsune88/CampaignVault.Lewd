namespace LewdHandbook.Mechanics;

/// <summary>
/// Intimate piercing sites and kinds documented for Lewd only. Core <c>piercing</c> accepts any string;
/// these constants stay out of PluginSdk so SFW tables never list them.
/// </summary>
internal static class LewdPiercingSites
{
    public const string NippleLeft = "nipple.left";
    public const string NippleRight = "nipple.right";
    public const string Clitoris = "clitoris";
    public const string LabiaLeft = "labia.left";
    public const string LabiaRight = "labia.right";
    public const string Foreskin = "foreskin";
    public const string Guiche = "guiche";
    public const string PrinceAlbert = "prince_albert";
}

internal static class LewdPiercingKinds
{
    public const string NippleRing = "lewd.nipple_ring";
    public const string ClitRing = "lewd.clit_ring";
    public const string LabiaRing = "lewd.labia_ring";
    public const string WeightedBell = "lewd.weighted_bell";
    public const string SlaveRing = "lewd.slave_ring";
}

/// <summary>Hard-limit tags that refuse intimate piercing theater (skill + optional helpers).</summary>
internal static class LewdPiercingLimits
{
    public static readonly string[] Tags =
    [
        "piercing", "needles", "genital_jewelry", "nipple_piercing", "clit_piercing",
    ];
}
