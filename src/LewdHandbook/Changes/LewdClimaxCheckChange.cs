using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_climax_check")]
public sealed class LewdClimaxCheckChange : WorldChange
{
    public string TargetId { get; set; } = null!;

    /// <summary>
    /// d20 result for the climax saving throw (1–20). When 0 and Rolls is available, engine rolls 1d20.
    /// </summary>
    public int D20 { get; set; }

    /// <summary>
    /// Optional override; climax saves always use raw inhibition (handbook).
    /// </summary>
    public int? InhibitionBonus { get; set; }

    /// <summary>Force climax without rolling (e.g. Power Word Cum).</summary>
    public bool ForceClimax { get; set; }

    /// <summary>Where this climax lands: inside | outside | none. Required for engine soil when lewdFluids is on.</summary>
    public string? Finish { get; set; }

    /// <summary>Site on the deposit target (face, hair, chest, pussy, ass...).</summary>
    public string? TargetAnatomy { get; set; }

    /// <summary>Who receives the deposit when different from the climaxing character (partner id).</summary>
    public string? DepositOnId { get; set; }

    public string? Notes { get; set; }
}
