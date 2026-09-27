using CampaignVault.Data.Guidance;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Guidance;

/// <summary>
/// Edge hint when <c>lewd_encounter</c> starts. Examples live in skills — keep this text short.
/// </summary>
public sealed class LewdModeGuidanceContributor : IPluginGuidanceContributor
{
    public Task<IEnumerable<PluginGuidanceHint>> EvaluateAsync(IGuidanceContext ctx, CancellationToken ct = default)
    {
        var entered = ctx.AppliedChanges.OfType<ModeTransitionChange>().Any(m =>
            string.Equals(m.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.Action, "enter", StringComparison.OrdinalIgnoreCase));

        if (!entered)
            return Task.FromResult<IEnumerable<PluginGuidanceHint>>([]);

        var settings = LewdSettings.From(ctx.Config?.SystemOptions);
        var nonConsent = settings.NonConsent switch
        {
            LewdNonConsent.On => "unwanted acts may resolve",
            LewdNonConsent.NotAgainstPc => "unwanted acts may resolve, never against the player's character",
            _ => "keep it consensual: unwanted acts fail",
        };
        var narration = settings.NarrationDirective ?? "lewdNarration=explicit.";
        return Task.FromResult<IEnumerable<PluginGuidanceHint>>(
        [
            new PluginGuidanceHint(
                "mode.enter",
                $"lewd_encounter active. {narration} lewdNonConsent: {nonConsent}. Player settings change only when the player asks. " +
                "Set NPC stance with lewd_stance. One action per participant on their own turn (lewd_advance, lewd_bind); " +
                "mode_transition action=turn passes it on. After a climax: lewd_recover. Skills: lewd-encounter, lewd-bindings, lewd-tracks, lewd-catalog.",
                Priority: 8),
        ]);
    }
}
