using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

public sealed class LewdClimaxCheckHandler : IWorldChangeHandler
{
    public bool ShouldHandle(WorldChange change) => change is LewdClimaxCheckChange;

    public async Task<ChangeHandlerResult> ApplyAsync(
        WorldChange change,
        IChangeContext context,
        CancellationToken ct = default)
    {
        var check = (LewdClimaxCheckChange)change;
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
            var d20 = check.D20;
            if (d20 is < 1 or > 20)
            {
                var die = await SaveDice.RollAsync(context, "lewd_climax_save", d20, abilityMod: 0, disadvantage: false, ct)
                    .ConfigureAwait(false);
                if (die.Error is not null)
                    return ChangeHandlerResult.Failure(die.Error);
                d20 = die.Face;
            }

            var inhib = check.InhibitionBonus ?? ConsentGate.ClimaxInhibition(target, targetChar);
            result = ClimaxMath.ResolveSave(
                d20,
                inhib,
                ConsentGate.GetInt(target, LewdKeys.ClimaxSuccesses),
                ConsentGate.GetInt(target, LewdKeys.ClimaxFailures),
                arousal.Current,
                arousal.Max);
            summary = result.Summary;
        }

        var ruin = await BrandState.PreRollRuinAsync(context, targetChar, ct).ConfigureAwait(false);
        var aftermath = LewdAdvanceHandler.ApplyClimaxResult(
            target, targetChar, arousal, result, sourceId: null, context, forced: check.ForceClimax, ruin: ruin);
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
