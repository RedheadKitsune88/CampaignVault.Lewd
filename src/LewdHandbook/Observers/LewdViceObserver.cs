using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

public sealed class LewdViceObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context) =>
        CharacterIdOf(committed) is not null &&
        (committed is RestChange or StatusRemove or TravelChange ||
         committed.MinutesElapsed is > 0);

    public async Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        var id = CharacterIdOf(committed);
        if (string.IsNullOrWhiteSpace(id) || !context.Characters.TryGetValue(id, out var character))
            return;

        ViceState.ApplyPendingBadEnd(character, context);

        var participant = context.ActiveMode?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));
        var now = await ViceState.HoursNowAsync(context, ct).ConfigureAwait(false);

        switch (committed)
        {
            case StatusRemove remove:
                ViceState.Restamp(character, remove.Status);
                return;
            case RestChange rest:
                await HandleRestAsync(id, character, participant, rest, now, context, ct).ConfigureAwait(false);
                return;
            default:
                // Travel / MinutesElapsed: sync the withdrawal clock only.
                foreach (var def in ViceCatalog.All)
                {
                    if (!ViceState.IsAddicted(character, def.Id))
                        continue;
                    ViceState.SyncWithdrawal(character, def, now, context);
                }

                break;
        }
    }

    private static async Task HandleRestAsync(
        string id,
        Character character,
        ModeParticipantState? participant,
        RestChange rest,
        float now,
        IChangeContext context,
        CancellationToken ct)
    {
        foreach (var def in ViceCatalog.All)
        {
            if (!ViceState.IsAddicted(character, def.Id) &&
                string.IsNullOrWhiteSpace(PregnancyState.Text(character, LewdKeys.BadEndViceId)))
                continue;

            ViceState.SyncWithdrawal(character, def, now, context);
            if (!IsLong(rest) || !ViceState.IsWithdrawal(character, def.Id))
                continue;

            if (context.Rolls is null)
            {
                context.RecordMessage(
                    $"{id} vice {def.Id} long rest withdrawal pending. Emit lewd_vice action=rest with d20 (Rolls unavailable).");
                continue;
            }

            var ability = PregnancyState.Text(character, ViceState.AbilityKey(def.Id));
            if (string.IsNullOrWhiteSpace(ability))
                ability = def.DefaultAbility;
            var mod = AbilityScores.Mod(character, ability);
            var roll = await SaveDice.RollAsync(
                context, "lewd_vice_rest", faceOrZero: 0, mod, disadvantage: true, ct).ConfigureAwait(false);
            if (roll.Error is not null)
            {
                context.RecordMessage($"{id} vice {def.Id}: {roll.Error}");
                continue;
            }

            var dc = ViceState.CurrentDc(character, def);
            if (roll.Total < dc)
                ViceState.FailWithdrawal(character, participant, def, roll.Face, context);
            else
            {
                context.RecordMessage(
                    $"{id} vice {def.Id} withdrawal save {roll.Summary} ≥ DC {dc} (disadv, {ability} {mod:+#;-#;+0}).");
                ViceState.TryCleanOnRestSuccess(character, def, context);
            }
        }
    }

    private static bool IsLong(RestChange rest) =>
        rest.RestType == RestType.LongRest || (rest.RestType is null && rest.IntendedHours >= 8);

    private static string? CharacterIdOf(WorldChange committed) => committed switch
    {
        RestChange rest => rest.CharacterId,
        StatusRemove remove => remove.CharacterId,
        TravelChange travel => travel.CharacterId,
        ActivityChange activity => activity.CharacterId,
        NeedChange need => need.CharacterId,
        ScheduleChange schedule => schedule.CharacterId,
        _ => null,
    };
}
