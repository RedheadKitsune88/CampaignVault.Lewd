using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

public sealed class LewdBadEndObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context)
    {
        // A rescue that began outside a scene (a capture) still needs finishing.
        if (CharacterId(committed) is { } touched && context.Characters.TryGetValue(touched, out var pending) && BadEndRescue.IsPending(pending))
            return true;
        var mode = LewdModeAccess.TryGetActive(context);
        if (mode is null)
            return false;

        return committed is HpChange or CharacterUpdate or StatusRemove
            or LewdAdvanceChange or LewdClimaxCheckChange or LewdPregnancyChange;
    }

    public async Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        var id = CharacterId(committed);
        if (string.IsNullOrWhiteSpace(id) || !context.Characters.TryGetValue(id, out var character))
            return;

        var participant = LewdModeAccess.TryGetActive(context)?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));
        if (BadEndRescue.IsPending(character))
        {
            await BadEndRescue.FinishAsync(context, character, participant, ct).ConfigureAwait(false);
            return;
        }

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        var flagged = BadEndState.IsMarked(participant, character);
        if (flagged && (participant is null || ConsentGate.GetBool(participant, LewdKeys.BadEnded) || PregnancyState.Flag(character, LewdKeys.BadEnded)))
        {
            if (participant is not null && ConsentGate.GetBool(participant, LewdKeys.BadEnded) &&
                !PregnancyState.Flag(character, LewdKeys.BadEnded))
            {
                var reason = ConsentGate.GetString(participant, LewdKeys.BadEndReason) ?? BadEndMath.Overstim;
                BadEndState.Apply(participant, character, reason, context: context, settings: settings);
            }
            else if (!character.SystemStats.StatusEffects.Any(BadEndState.IsBadEndEffect))
            {
                BadEndState.Stamp(character, "lewd_bad_end");
            }
        }

        if (character.SystemStats.ResourcePools.TryGetValue(LewdKeys.PoolArousal, out var arousal) &&
            arousal is not null && arousal.Max <= 0)
        {
            BadEndState.Apply(participant, character, BadEndMath.ArousalMax, context: context, settings: settings);
        }

        if (committed is HpChange && character.CurrentHp <= 0 && !BadEndState.IsMarked(participant, character))
        {
            context.RecordMessage(
                $"{id} is at 0 HP in a lewd encounter. Defeat is not automatic. Emit lewd_bad_end with reason=defeat and noEscape=true if there is no rescue.");
        }

        if (BadEndRescue.IsPending(character))
            await BadEndRescue.FinishAsync(context, character, participant, ct).ConfigureAwait(false);
    }

    private static string? CharacterId(WorldChange change) => change switch
    {
        LewdAdvanceChange advance => advance.TargetId,
        LewdClimaxCheckChange check => check.TargetId,
        LewdPregnancyChange pregnancy => pregnancy.TargetId,
        HpChange hp => hp.CharacterId,
        CharacterUpdate update => update.CharacterId,
        StatusRemove remove => remove.CharacterId,
        _ => null,
    };
}
