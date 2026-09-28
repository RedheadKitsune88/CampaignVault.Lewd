using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdClimaxCheckHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdClimaxCheckChange;

    public Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default) =>
        ApplyAsync((LewdClimaxCheckChange)change, context, enforceConsent: true, ct);

    /// <param name="enforceConsent">
    /// False only for a brand's own compulsion (Echoes): the brand itself was authorized when it was applied.
    /// </param>
    internal async Task<ChangeHandlerResult> ApplyAsync(
        LewdClimaxCheckChange check,
        IChangeContext context,
        bool enforceConsent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(check.TargetId))
            return ChangeHandlerResult.Failure("targetId is required.");
        if (!AgeGate.TryPassAll(context, out var ageError, check.TargetId))
            return ChangeHandlerResult.Failure(ageError!);
        var targetChar = context.Characters[check.TargetId];

        // Outside an encounter (spells/items) the counters live on an ephemeral scratch participant.
        var target = LewdModeAccess.TryGetParticipant(context, check.TargetId)
                     ?? new ModeParticipantState { CharacterId = check.TargetId };
        if (LewdProfile.IsRevoked(target, targetChar))
            return ChangeHandlerResult.Failure($"Target '{check.TargetId}' has revoked consent.");

        var arousal = LewdPoolHelper.Arousal(targetChar);
        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        if (check.ForceClimax && enforceConsent &&
            !ConsentGate.AuthorizeEffect(target, targetChar, check.TargetId, null, ["forced_climax"], settings, out _, out var gateError))
            return ChangeHandlerResult.Failure($"Forced climax refused: {gateError}");
        BrandState.Mirror(target, targetChar);
        var wasIncap = ConsentGate.GetBool(target, LewdKeys.ClimaxIncapacitated);

        string summary;
        ClimaxSaveResult result;
        if (check.ForceClimax)
        {
            result = new ClimaxSaveResult(
                ClimaxOutcomeKind.InstantClimax, 0, 0, arousal.Current, EdgingAfter: false, Summary: "Forced climax.");
            summary = "forced climax.";
        }
        else
        {
            var atEdge = ConsentGate.GetBool(target, LewdKeys.Edging) ||
                         (arousal.Max > 0 && arousal.Current >= arousal.Max);
            if (!atEdge)
            {
                return ChangeHandlerResult.Failure(
                    $"{check.TargetId} is not edging (arousal {arousal.Current}/{arousal.Max}). Climax saves happen at maximum arousal; use forceClimax for effects that force one.");
            }

            LewdPoolHelper.SetEdging(target, targetChar, true);
            // Not tagged "mental": nymphomanic and flustered already weigh in through inhibition. Numbers on status effects
            // (a rattled will, dead arms do not apply) still count, so they join the inhibition bonus.
            var die = await SaveDice.RollAsync(
                context, "lewd_climax_save", check.D20, abilityMod: 0, disadvantage: false, ct,
                who: targetChar, subject: "wis", tags: ["climax"]).ConfigureAwait(false);
            if (die.Error is not null)
                return ChangeHandlerResult.Failure(die.Error);
            var d20 = die.Face;
            var inhib = (check.InhibitionBonus ?? ConsentGate.ClimaxInhibition(target, targetChar)) + (die.Total - die.Face);
            var (tallySuccesses, tallyFailures) = LewdPoolHelper.ClimaxCounters(target, targetChar);
            result = ClimaxMath.ResolveSave(
                d20,
                inhib,
                tallySuccesses,
                tallyFailures,
                arousal.Current,
                arousal.Max);
            summary = result.Summary;
        }

        var ruin = await BrandState.PreRollRuinAsync(context, targetChar, ct).ConfigureAwait(false);
        var aftermath = LewdAdvanceHandler.ApplyClimaxResult(
            target, targetChar, arousal, result, sourceId: null, context, forced: check.ForceClimax, ruin: ruin,
            nowDays: await LewdClock.NowDaysAsync(context).ConfigureAwait(false));
        context.RecordMessage($"Lewd climax check {check.TargetId}: {summary}{aftermath}");
        settings.Narrate(context);

        var climaxed = result.Kind is ClimaxOutcomeKind.Climaxed or ClimaxOutcomeKind.InstantClimax;
        if (climaxed && (wasIncap || check.ForceClimax))
        {
            await ImprintState.AutoAsync(
                context,
                target,
                targetChar,
                ["ordeal", "incapacitated"],
                LewdProfile.Wants(target, targetChar, null),
                bitchsuit: false,
                ct).ConfigureAwait(false);
        }

        return ChangeHandlerResult.Ok;
    }
}
