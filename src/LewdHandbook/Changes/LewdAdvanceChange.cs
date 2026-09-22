using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_advance")]
public sealed class LewdAdvanceChange : WorldChange
{
    public string ActorId { get; set; } = null!;
    public string TargetId { get; set; } = null!;

    /// <summary>martial | indirect | skilled</summary>
    public string Kind { get; set; } = "martial";

    /// <summary>
    /// Pre-resolved stimulation after dice. When 0/omitted and Rolls is available, engine may roll
    /// <see cref="StimulationDice"/> / implement / anatomy Traits.
    /// </summary>
    public int StimulationAmount { get; set; }

    /// <summary>Explicit stim dice expression (e.g. 1d8). Used when Rolls is present or for maximize.</summary>
    public string? StimulationDice { get; set; }

    public string? StimulationType { get; set; }

    /// <summary>Optional kink/limit tags (in addition to StimulationType).</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Item id / template name for artificial implement.</summary>
    public string? ImplementId { get; set; }

    /// <summary>Natural anatomy Traits key or suffix (e.g. cock or anatomy.cock).</summary>
    public string? AnatomyKey { get; set; }

    /// <summary>Ability bonus added to rolled stim (Str, or Dex if finesse).</summary>
    public int AbilityBonus { get; set; }

    /// <summary>
    /// Martial hit result. Null + unwilling martial → attempt roll via Rolls when attackBonus set,
    /// else fail asking for a roll. Willing martial may omit (treated as hit).
    /// </summary>
    public bool? Hit { get; set; }

    /// <summary>Attack bonus for martial roll when Hit is null and Rolls is available.</summary>
    public int? AttackBonus { get; set; }

    /// <summary>Target AC including Inhibition when unwanted. Used with AttackBonus + Rolls.</summary>
    public int? TargetAc { get; set; }

    public bool IsCritical { get; set; }

    /// <summary>When true, deal maximum stimulation die result (edging target rule / crit flavor).</summary>
    public bool MaximizeStimulation { get; set; }

    /// <summary>Sites the actor must have free (structured binding check).</summary>
    public List<string>? RequiresFreeSites { get; set; }

    public string? Notes { get; set; }
}
