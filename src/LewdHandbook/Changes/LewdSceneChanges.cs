using CampaignVault.Models;
using CampaignVault.Plugins;

namespace LewdHandbook.Changes;

/// <summary>
/// Start-of-turn rules for one participant (edging, extended-edging overstimulation, climax incapacitation expiry,
/// timed hardening). Emitted by the plugin when <c>mode_transition action=turn</c> starts that participant's turn;
/// the model does not need to send it.
/// </summary>
[EngineOnly]
[PluginWorldChange("lewd_turn_start", ModeId = LewdEncounterMode.ModeIdValue)]
public sealed class LewdTurnStartChange : WorldChange
{
    public string CharacterId { get; set; } = null!;
}

/// <summary>
/// Scene wrap-up after <c>lewd_encounter</c> exits: resolves the imprint ledger, ends climax incapacitation and
/// edging, resyncs overstimulation conditions. Bindings stay. Emitted by the plugin on core.mode_exited.v1.
/// </summary>
[EngineOnly]
[PluginWorldChange("lewd_scene_end", ModeId = LewdEncounterMode.ModeIdValue)]
public sealed class LewdSceneEndChange : WorldChange
{
    public List<string> ParticipantIds { get; set; } = [];
}

/// <summary>
/// In-fiction disposition of one character: stance toward advances, allowed partners, personal limits, kinks and
/// Inhibition. <c>scope=scene</c> overrides for the current encounter only; <c>scope=default</c> writes the
/// character's lasting defaults. The player's content limits are campaign options, not this.
/// </summary>
[PluginWorldChange("lewd_stance")]
public sealed class LewdStanceChange : WorldChange
{
    public string CharacterId { get; set; } = null!;

    /// <summary>willing | selective | unwilling | revoked. revoked is a hard stop: no lewd verb touches them.</summary>
    public string? Stance { get; set; }

    /// <summary>For selective: who is welcome. Replaces the list at this scope.</summary>
    public List<string>? AllowedPartners { get; set; }

    /// <summary>Acts/tags this character never accepts. Replaces the list at this scope.</summary>
    public List<string>? HardLimits { get; set; }

    /// <summary>Tags that halve stimulation. Replaces the list at this scope.</summary>
    public List<string>? SoftLimits { get; set; }

    /// <summary>Tags that add half again to stimulation. Replaces the list at this scope.</summary>
    public List<string>? Kinks { get; set; }

    /// <summary>Inhibition bonus (Int, Wis or Cha modifier chosen at creation).</summary>
    public int? Inhibition { get; set; }

    /// <summary>scene | default. Omitted: scene inside a lewd_encounter, default outside.</summary>
    public string? Scope { get; set; }

    /// <summary>
    /// The player's own words. Required to loosen the player's character (a more willing stance, more allowed
    /// partners, fewer hard limits); tightening never needs it.
    /// </summary>
    public string? PlayerRequest { get; set; }
}

/// <summary>
/// Everything a completed rest does to the lewd tracks, in one ordered step: vice withdrawal saves, brand hooks,
/// imprint decay, pregnancy (progress and the rest poison save), arousal (long rest: −½ max, numbing cleared) and the
/// recovery-dice window after a short rest. Emitted by the plugin on core.rested.v1 (interrupted rests publish
/// nothing); the model does not send it.
/// </summary>
[EngineOnly]
[PluginWorldChange("lewd_rest", ModeId = LewdEncounterMode.ModeIdValue)]
public sealed class LewdRestChange : WorldChange
{
    public string CharacterId { get; set; } = null!;

    /// <summary>short | long</summary>
    public string RestType { get; set; } = "long";
}

/// <summary>
/// Spend Recovery Dice right after a climax or a short rest: up to the proficiency bonus, each die + Con lowers
/// arousal. After a climax, incapacitation lasts one round per die spent.
/// </summary>
[PluginWorldChange("lewd_recover")]
public sealed class LewdRecoverChange : WorldChange
{
    public string CharacterId { get; set; } = null!;

    /// <summary>How many Recovery Dice to spend (1 .. proficiency bonus, and no more than remain).</summary>
    public int Dice { get; set; } = 1;

    /// <summary>Optional faces already rolled; otherwise the host rolls.</summary>
    public List<int>? Faces { get; set; }

    /// <summary>
    /// Instead of spending dice: a free action while incapacitated by a climax, a Constitution save (DC 12 + climaxes
    /// in the last hour) to end it. <see cref="D20"/> is the rolled face, or 0 to let the host roll.
    /// </summary>
    public bool Save { get; set; }

    public int D20 { get; set; }
}

/// <summary>
/// Brand of Echoes check after a climax: which of <see cref="CandidateIds"/> bear Echoes and are within 5 ft. Emitted by
/// the plugin on its own climax.v1 (so the host loads the candidates first); the model does not send it.
/// </summary>
[EngineOnly]
[PluginWorldChange("lewd_echo_check", ModeId = LewdEncounterMode.ModeIdValue)]
public sealed class LewdEchoCheckChange : WorldChange
{
    public string ClimaxedId { get; set; } = null!;
    public List<string> CandidateIds { get; set; } = [];
}
