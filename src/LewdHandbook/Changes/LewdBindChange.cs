using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

/// <summary>
/// Puts a binding on a character, in a lewd scene or anywhere else (a captive, prisoners on a road gang). An unwilling
/// target must be subdued first (grappled, restrained, incapacitated…). Erotic binds follow the lewd consent rules;
/// plain restraint only the player's hard limits.
/// </summary>
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

    /// <summary>Ties the target to someone (chars/…) or something (items/…, a fixture): they can't travel unless it comes along.</summary>
    public string? AnchorId { get; set; }

    /// <summary>A lock: lewd_unbind needs the key when one is set; otherwise pick it or break it (lewd_escape).</summary>
    public bool? Locked { get; set; }

    public int? LockDc { get; set; }

    public string? KeyItemId { get; set; }

    /// <summary>The target submits to it (no subdue needed). For erotic binds their stance decides instead.</summary>
    public bool Willing { get; set; }

    /// <summary>Part of a sex scene. Omitted: true inside a lewd_encounter, or for erotic gear (suits, spreaders); else plain restraint.</summary>
    public bool? Erotic { get; set; }

    /// <summary>Optional posture label (standing, kneeling, prone, all_fours_crawl…), kept on the character.</summary>
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

    /// <summary>The key, for a locked binding that names one.</summary>
    public string? KeyItemId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// An attempt to get out of (or cut someone out of) a binding. slip: Dex (or Str) vs escape DC; break: Str vs break
/// DC; pick: Dex + tools vs lock DC; unlock: with its key; cut / damage: <c>amount</c> off its hit points.
/// </summary>
[PluginWorldChange("lewd_escape")]
public sealed class LewdEscapeChange : WorldChange
{
    /// <summary>The bound character.</summary>
    public string CharacterId { get; set; } = null!;

    /// <summary>Someone else working on it (picking, cutting, breaking); omit when the bound character tries.</summary>
    public string? ActorId { get; set; }

    /// <summary>Which binding; omitted = the most recent.</summary>
    public string? BindingId { get; set; }

    /// <summary>slip | break | pick | unlock | cut</summary>
    public string Method { get; set; } = "slip";

    /// <summary>0 = the host rolls.</summary>
    public int D20 { get; set; }

    /// <summary>Proficiency or tool bonus on top of the ability modifier (Athletics, Acrobatics, thieves' tools).</summary>
    public int Bonus { get; set; }

    /// <summary>For slip: dex (default) or str.</summary>
    public string? Ability { get; set; }

    /// <summary>For cut: damage dealt to the binding.</summary>
    public int Amount { get; set; }

    public string? KeyItemId { get; set; }
}
