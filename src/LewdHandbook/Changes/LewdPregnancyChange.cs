using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_pregnancy")]
public sealed class LewdPregnancyChange : WorldChange
{
    public string TargetId { get; set; } = null!;

    public string? ActorId { get; set; }

    /// <summary>impregnate | advance | rest | terminate | termination_save</summary>
    public string Action { get; set; } = "impregnate";

    /// <summary>traditional | nontraditional</summary>
    public string Kind { get; set; } = "traditional";

    public int D20 { get; set; }

    public int? SecondD20 { get; set; }

    /// <summary>Omit to read Constitution from Dnd5eExtension.</summary>
    public int? ActorConModifier { get; set; }

    /// <summary>Omit to read Constitution from Dnd5eExtension.</summary>
    public int? TargetConModifier { get; set; }

    public int TargetProficiency { get; set; }

    public int? Dc { get; set; }

    public int InhibitionBonus { get; set; }

    public bool Unwilling { get; set; }

    public bool Hyperfertile { get; set; }

    public bool Hypervirile { get; set; }

    public bool Infertile { get; set; }

    public bool AutoSucceedSave { get; set; }

    /// <summary>none | condom | oil | beads</summary>
    public string? Contraceptive { get; set; }

    public bool Force { get; set; }

    public bool NoEscape { get; set; }

    public int? ProgressDelta { get; set; }

    public int? ProgressOnSuccess { get; set; }

    public string? Notes { get; set; }
}
