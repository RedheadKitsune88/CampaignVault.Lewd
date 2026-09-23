using CampaignVault.Data;
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

        var mode = context.ActiveMode;
        if (mode is null || !mode.IsActive ||
            !string.Equals(mode.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase))
        {
            return ChangeHandlerResult.Failure(
                "lewd_climax_check requires an active lewd_encounter mode.");
        }

        var target = mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, check.TargetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return ChangeHandlerResult.Failure($"Target '{check.TargetId}' is not in the lewd encounter.");

        context.Characters.TryGetValue(check.TargetId, out var targetChar);
        var arousal = targetChar is not null
            ? LewdPoolHelper.EnsurePool(targetChar, LewdKeys.PoolArousal, defaultMax: 10, RecoveryType.Never)
            : new ResourcePool
            {
                Current = ConsentGate.GetInt(target, LewdKeys.ArousalCurrentMirror),
                Max = Math.Max(1, ConsentGate.GetInt(target, LewdKeys.ArousalMaxMirror)),
                Recovery = RecoveryType.Never,
            };
        if (arousal.Max <= 0)
            arousal.Max = 10;

        if (check.ForceClimax)
        {
            var forced = new ClimaxSaveResult(
                ClimaxOutcomeKind.InstantClimax,
                0,
                0,
                arousal.Current,
                EdgingAfter: false,
                Summary: "Forced climax.");
            var note = LewdAdvanceHandler.ApplyClimaxResult(target, targetChar, arousal, forced, sourceId: null, context);
            context.RecordMessage($"Lewd climax check {check.TargetId}: forced climax.{note}");
            return ChangeHandlerResult.Ok;
        }

        var d20 = check.D20;
        if (d20 is < 1 or > 20)
        {
            if (context.Rolls is not null && d20 == 0)
            {
                var roll = await context.Rolls.RollAsync(
                    new RollRequest { Tag = "lewd_climax_save", Expression = "1d20" },
                    ct).ConfigureAwait(false);
                d20 = roll.IndividualDice.FirstOrDefault() is > 0 and <= 20
                    ? roll.IndividualDice[0]
                    : Math.Clamp(roll.Result, 1, 20);
                // Prefer natural die face when bonus was 0
                if (roll.IndividualDice.Count > 0)
                    d20 = roll.IndividualDice[0];
                context.RecordMessage($"Lewd climax d20 roll: {roll.Summary}.");
            }
            else
            {
                return ChangeHandlerResult.Failure("d20 must be between 1 and 20 (or 0 with Rolls to auto-roll).");
            }
        }

        if (arousal.Current >= arousal.Max)
            LewdPoolHelper.SetEdging(target, targetChar, true);

        if (targetChar is not null)
            BrandState.Mirror(target, targetChar);
        var inhib = check.InhibitionBonus
            ?? ConsentGate.GetInt(target, LewdKeys.Inhibition) - ConsentGate.GetInt(target, LewdKeys.LustbrandInhib);
        var wasIncap = ConsentGate.GetBool(target, LewdKeys.ClimaxIncapacitated);
        var successes = ConsentGate.GetInt(target, LewdKeys.ClimaxSuccesses);
        var failures = ConsentGate.GetInt(target, LewdKeys.ClimaxFailures);

        var result = ClimaxMath.ResolveSave(
            d20,
            inhib,
            successes,
            failures,
            arousal.Current,
            arousal.Max);

        var aftermath = LewdAdvanceHandler.ApplyClimaxResult(target, targetChar, arousal, result, context: context);
        context.RecordMessage($"Lewd climax check {check.TargetId}: {result.Summary}{aftermath}");
        if (targetChar is not null &&
            result.Kind is ClimaxOutcomeKind.Climaxed or ClimaxOutcomeKind.InstantClimax &&
            (wasIncap || check.ForceClimax))
        {
            await ImprintState.AutoAsync(
                context,
                target,
                targetChar,
                ["ordeal", "incapacitated"],
                ConsentGate.IsAdvanceWanted(target, check.TargetId),
                bitchsuit: false,
                ct).ConfigureAwait(false);
        }

        return ChangeHandlerResult.Ok;
    }
}
