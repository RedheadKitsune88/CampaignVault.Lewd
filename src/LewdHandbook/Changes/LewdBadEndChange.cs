using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_bad_end")]
public sealed class LewdBadEndChange : WorldChange
{
    public string TargetId { get; set; } = null!;

    /// <summary>defeat | explicit. Engine-owned reasons are rejected.</summary>
    public string Reason { get; set; } = "explicit";

    public bool NoEscape { get; set; }

    public string? Consequence { get; set; }

    public string? ImprintTrack { get; set; }

    public int? ImprintJump { get; set; }

    public bool ImprintWilling { get; set; }

    public string? ViceId { get; set; }
}
