using System.ComponentModel;
using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

/// <summary>
/// Public theater / naming / piercing display / forced begging — shame bite. Works outside a scene.
/// Arousal only when ordeal (masochism) imprint ≥1; inhibition is never consulted.
/// </summary>
[PluginWorldChange("lewd_humiliate")]
public sealed class LewdHumiliateChange : WorldChange
{
    [Description("Character being shamed.")]
    public string CharacterId { get; set; } = null!;

    /// <summary>1 light / 2 sharp / 3 crushing.</summary>
    [Description("Shame severity 1–3 (light / sharp / crushing).")]
    public int Severity { get; set; } = 1;

    [Description("Open tags: public, display, begging, piercing, naming, use, clothing, pain, …")]
    public List<string>? Tags { get; set; }

    [Description("Optional actor, crowd, or ritual id.")]
    public string? SourceId { get; set; }

    [Description("Short audit string.")]
    public string? Reason { get; set; }

    /// <summary>Optional 1d4 face for ordeal-gated arousal (tests / explicit rolls). 0 = engine rolls or averages.</summary>
    [Description("Optional 1d4 face for ordeal-gated arousal; 0 lets the engine roll.")]
    public int ArousalD4 { get; set; }
}
