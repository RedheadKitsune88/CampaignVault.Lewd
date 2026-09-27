using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

public sealed class LewdImprintObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context) =>
        committed is StatusRemove;

    public async Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        var id = committed switch
        {
            StatusRemove remove => remove.CharacterId,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(id) || !context.Characters.TryGetValue(id, out var character))
            return;

        var participant = LewdModeAccess.TryGetActive(context)?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));

        switch (committed)
        {
            case StatusRemove remove:
            {
                var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
                ImprintState.ApplyPendingJump(participant, character, day, context);
                ImprintState.Restamp(character, remove.Status);
                break;
            }
        }
    }
}
