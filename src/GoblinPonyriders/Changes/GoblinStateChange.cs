using CampaignVault.Models;
using CampaignVault.Plugins;

namespace GoblinPonyriders.Changes;

/// <summary>
/// Unified Clans / Goblin Ponyriders state verb. Works anywhere (no ModeId).
/// Actions: seed_faction | capture | release | defiance | mark | role | train | name | status
/// </summary>
[PluginWorldChange("goblin_state")]
public sealed class GoblinStateChange : WorldChange
{
    public string CharacterId { get; set; } = null!;

    /// <summary>
    /// seed_faction | capture | release | defiance | mark | role | train | name | status
    /// </summary>
    public string Action { get; set; } = "status";

    /// <summary>For defiance: signed delta. For mark: levels to raise (default 1) or absolute via Absolute.</summary>
    public int Delta { get; set; }

    /// <summary>When true with mark/defiance, Delta is an absolute level instead of a relative change.</summary>
    public bool Absolute { get; set; }

    /// <summary>Role id from the catalog (riding_girl, domestic, entertainer, ...).</summary>
    public string? RoleId { get; set; }

    /// <summary>Training phase override: raid_camp | village | assigned.</summary>
    public string? Phase { get; set; }

    /// <summary>Humiliating assigned name / tattoo label.</summary>
    public string? AssignedName { get; set; }

    /// <summary>release: when true, stamp terror-release + rumor guidance.</summary>
    public bool TerrorRelease { get; set; }

    public string? Reason { get; set; }

    /// <summary>Optional; seed_faction refreshes this id when already loaded (default factions/unified_clans).</summary>
    public string? FactionId { get; set; }
}
