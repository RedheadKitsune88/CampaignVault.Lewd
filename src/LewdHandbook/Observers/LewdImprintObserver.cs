using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

public sealed class LewdImprintObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context) =>
        committed is RestChange or StatusRemove;

    public async Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        var id = committed switch
        {
            RestChange rest => rest.CharacterId,
            StatusRemove remove => remove.CharacterId,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(id) || !context.Characters.TryGetValue(id, out var character))
            return;

        var participant = context.ActiveMode?.Participants.FirstOrDefault(p =>
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
            case RestChange rest:
            {
                var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
                ImprintState.ApplyPendingJump(participant, character, day, context);
                if (IsLong(rest))
                    await ImprintState.OnLongRestAsync(character, participant, context, ct).ConfigureAwait(false);
                break;
            }
        }
    }

    private static bool IsLong(RestChange rest) =>
        rest.RestType == RestType.LongRest || (rest.RestType is null && rest.IntendedHours >= 8);
}
