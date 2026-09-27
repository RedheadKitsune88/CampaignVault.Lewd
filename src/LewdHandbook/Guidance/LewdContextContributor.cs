using CampaignVault.Data.Context;
using CampaignVault.Models;
using LewdHandbook.Changes;

namespace LewdHandbook.Guidance;

/// <summary>
/// Lean one-line encounter facts on turns that touched Lewd verbs or entered the mode.
/// Put changing values in the key so the host re-delivers updates.
/// </summary>
public sealed class LewdContextContributor : IPluginContextContributor
{
    public Task<IEnumerable<PluginContextItem>> ContributeAsync(IContextTurn turn, CancellationToken ct = default)
    {
        var relevant = turn.AppliedChanges.Any(IsLewdRelevant);
        if (!relevant)
            return Task.FromResult<IEnumerable<PluginContextItem>>([]);

        var items = new List<PluginContextItem>();

        if (turn.AppliedChanges.OfType<ModeTransitionChange>().Any(m =>
                string.Equals(m.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(m.Action, "enter", StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(new PluginContextItem(
                "lewd:mode:enter",
                "lewd_encounter started — read participant arousal/bindings from the mode state / sheet pools.",
                Priority: 6));
        }

        var verb = turn.AppliedChanges
            .Select(Discriminator)
            .FirstOrDefault(d => d is not null && d.StartsWith("lewd_", StringComparison.OrdinalIgnoreCase));
        if (verb is not null)
        {
            items.Add(new PluginContextItem(
                $"lewd:verb:{verb}",
                $"{verb} committed — refresh arousal, overstim, bindings and tracks from engine state before narrating.",
                Priority: 5));
        }

        return Task.FromResult<IEnumerable<PluginContextItem>>(items);
    }

    private static bool IsLewdRelevant(WorldChange change) =>
        change is ModeTransitionChange mt &&
        string.Equals(mt.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase)
        || change is LewdAdvanceChange
            or LewdBindChange
            or LewdUnbindChange
            or LewdClimaxCheckChange
            or LewdBadEndChange
            or LewdViceChange
            or LewdApplyBrandChange
            or LewdImprintChange
            or LewdDeconditionChange
            or LewdPregnancyChange
            or LewdStanceChange
            or LewdTurnStartChange
            or LewdSceneEndChange
            or LewdEscapeChange
            or LewdRecoverChange
            or LewdRestChange;

    private static string? Discriminator(WorldChange change) => change switch
    {
        LewdAdvanceChange => "lewd_advance",
        LewdBindChange => "lewd_bind",
        LewdUnbindChange => "lewd_unbind",
        LewdClimaxCheckChange => "lewd_climax_check",
        LewdBadEndChange => "lewd_bad_end",
        LewdViceChange => "lewd_vice",
        LewdApplyBrandChange => "lewd_apply_brand",
        LewdImprintChange => "lewd_imprint",
        LewdDeconditionChange => "lewd_decondition",
        LewdPregnancyChange => "lewd_pregnancy",
        LewdStanceChange => "lewd_stance",
        LewdTurnStartChange => "lewd_turn_start",
        LewdSceneEndChange => "lewd_scene_end",
        LewdEscapeChange => "lewd_escape",
        LewdRecoverChange => "lewd_recover",
        LewdRestChange => "lewd_rest",
        _ => null,
    };
}
