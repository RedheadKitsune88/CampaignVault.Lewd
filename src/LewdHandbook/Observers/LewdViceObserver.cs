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

        var participant = LewdModeAccess.TryGetActive(context)?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));
        var now = await ViceState.HoursNowAsync(context, ct).ConfigureAwait(false);

        switch (committed)
        {
            case StatusRemove remove:
                ViceState.Restamp(character, remove.Status);
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

    private static string? CharacterIdOf(WorldChange committed) => committed switch
    {
        StatusRemove remove => remove.CharacterId,
        TravelChange travel => travel.CharacterId,
        ActivityChange activity => activity.CharacterId,
        NeedChange need => need.CharacterId,
        ScheduleChange schedule => schedule.CharacterId,
        _ => null,
    };
}
