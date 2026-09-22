using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

[PluginWorldChange("lewd_bind")]
public sealed class LewdBindChange : WorldChange
{
    public string ActorId { get; set; } = null!;
    public string TargetId { get; set; } = null!;

    /// <summary>Optional live Item / template that seeds default graph fragments.</summary>
    public string? ItemId { get; set; }

    public string? Kind { get; set; }
    public List<string>? Sites { get; set; }
    public string? Orientation { get; set; }
    public List<string>? Links { get; set; }
    public List<string>? Implies { get; set; }
    public List<string>? Materials { get; set; }
    public List<string>? Effects { get; set; }
    public bool Hardened { get; set; }
    public int? HardenAtRound { get; set; }
    public int? EscapeDc { get; set; }
    public int? BreakDc { get; set; }
    public int? Hp { get; set; }

    /// <summary>Optional posture label written onto target State.</summary>
    public string? Posture { get; set; }

    public string? Notes { get; set; }
}

[PluginWorldChange("lewd_unbind")]
public sealed class LewdUnbindChange : WorldChange
{
    public string ActorId { get; set; } = null!;
    public string TargetId { get; set; } = null!;

    /// <summary>Remove a specific binding id. When null, remove by itemId or all.</summary>
    public string? BindingId { get; set; }

    public string? ItemId { get; set; }

    /// <summary>When true, clear all bindings on the target.</summary>
    public bool RemoveAll { get; set; }

    public string? Notes { get; set; }
}
