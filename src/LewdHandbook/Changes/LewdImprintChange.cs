using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_imprint")]
public sealed class LewdImprintChange : WorldChange
{
    public string TargetId { get; set; } = null!;

    /// <summary>wanton | training | breeding | ordeal | cruelty</summary>
    public string? Category { get; set; }

    /// <summary>training | wanton | bad_end | cruelty | exposure</summary>
    public string? Source { get; set; }

    public bool Willing { get; set; }

    /// <summary>Consent-gated convert of an existing track's origin to willing. Does not itself add points.</summary>
    public bool Accept { get; set; }

    public int? Delta { get; set; }

    /// <summary>wis | int. Used when willing is false.</summary>
    public string Ability { get; set; } = "wis";

    public int D20 { get; set; }

    /// <summary>Omit to read the ability from Dnd5eExtension. Explicit 0 stays 0.</summary>
    public int? AbilityMod { get; set; }

    /// <summary>Added to DC 12 + level. AbilityMod is on the roll, not the DC.</summary>
    public int DcMod { get; set; }

    public List<string>? Tags { get; set; }

    /// <summary>
    /// Backstory seeding: set the track straight to level 1–3 (origin from <see cref="Willing"/>) with no save —
    /// e.g. a rescued captive who arrives already conditioned. Not for in-scene pressure; that ticks.
    /// </summary>
    public int? SetLevel { get; set; }

    /// <summary>Who the conditioning is tied to (a captor, a trusted partner). It then weighs only against them.</summary>
    public string? AnchorId { get; set; }
}
