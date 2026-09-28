using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

/// <summary>
/// Seats an implement, plug, beads, wand or partner anatomy in a receptive orifice. Works in or out of a scene.
/// Not a binding: use <c>lewd_bind</c> for restraints.
/// </summary>
[PluginWorldChange("lewd_insert")]
[ActorAction]
public sealed class LewdInsertChange : WorldChange
{
    public string ActorId { get; set; } = null!;
    public string TargetId { get; set; } = null!;

    /// <summary>pussy | ass | mouth (required).</summary>
    public string Orifice { get; set; } = null!;

    /// <summary>phallic | plug | beads | wand | partner. Omitted: guessed from itemId.</summary>
    public string? Kind { get; set; }

    /// <summary>open | plugged | beaded. Omitted: derived from kind.</summary>
    public string? Seal { get; set; }

    public string? ItemId { get; set; }

    /// <summary>Partner character id when kind is partner.</summary>
    public string? SourceId { get; set; }

    /// <summary>How many beads on the string (beads only).</summary>
    public int BeadStages { get; set; }

    /// <summary>How many are seated now (beads only). Omitted: all stages.</summary>
    public int? BeadStage { get; set; }

    public string? Label { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Removes a seated occupancy (and may trigger a leak if an internal deposit was sealed).</summary>
[PluginWorldChange("lewd_remove")]
public sealed class LewdRemoveChange : WorldChange
{
    public string ActorId { get; set; } = null!;
    public string TargetId { get; set; } = null!;

    /// <summary>Remove this occupancy id. When null, remove by orifice or itemId.</summary>
    public string? OccupancyId { get; set; }

    public string? Orifice { get; set; }
    public string? ItemId { get; set; }

    /// <summary>Beads only: pull this many stages (omit = remove the whole string).</summary>
    public int? BeadPull { get; set; }

    public string? Notes { get; set; }
}
