using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_apply_brand")]
public sealed class LewdApplyBrandChange : WorldChange
{
    public string TargetId { get; set; } = null!;

    /// <summary>Catalog id: abundance, addiction, altruism, bestial, betrayal, denial, echoes, emptiness, false_dominance, fertility, infatuation, oaths, ruin, obedience, forsaken, hungry_gaze, transformation.</summary>
    public string? BrandId { get; set; }

    /// <summary>apply | remove | vow | trigger | release | stabilize</summary>
    public string Action { get; set; } = "apply";

    public int? Tier { get; set; }

    public string? SourceId { get; set; }

    /// <summary>wish | feature. Required for remove. remove curse is rejected.</summary>
    public string? Method { get; set; }

    /// <summary>Foe type, release conditions, infatuation source, vow text, or transformation trigger.</summary>
    public string? Payload { get; set; }

    public bool Willing { get; set; }

    /// <summary>Concubi whose last brand is removed rebrand at tier+1 on the next climax.</summary>
    public bool Concubi { get; set; }

    public int D20 { get; set; }

    public int ConModifier { get; set; }
}
