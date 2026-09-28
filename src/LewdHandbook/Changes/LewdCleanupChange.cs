using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

/// <summary>
/// Aftercare / wash-up: clear internal deposits and engine <c>lewd.*</c> dirt on a character (and optionally remove
/// seated toys). Works in or out of a scene.
/// </summary>
[PluginWorldChange("lewd_cleanup")]
public sealed class LewdCleanupChange : WorldChange
{
    public string ActorId { get; set; } = null!;
    public string TargetId { get; set; } = null!;

    /// <summary>Also remove occupancy (plugs/beads/toys). Default false — wash without unseating.</summary>
    public bool RemoveToys { get; set; }

    public string? Notes { get; set; }
}
