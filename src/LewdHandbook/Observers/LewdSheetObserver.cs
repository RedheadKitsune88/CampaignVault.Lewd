using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

/// <summary>
/// Keeps the arousal maximum in step with the sheet: a new character, a changed Con / recovery die / sexual history, or
/// a level-up re-derives it (recovery die + Con per level). Characters without a recovery die are left alone.
/// </summary>
public sealed class LewdSheetObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context) =>
        committed is CharacterCreate or CharacterUpdate or LevelUpChange;

    public Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        var id = committed switch
        {
            CharacterCreate create => create.CharacterId,
            CharacterUpdate update => update.CharacterId,
            LevelUpChange level => level.CharacterId,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(id) || !context.Characters.TryGetValue(id, out var character))
            return Task.CompletedTask;
        if (ArousalMath.RecoveryDieSides(character) is null)
            return Task.CompletedTask;

        var arousal = LewdPoolHelper.Arousal(character);
        var participant = LewdModeAccess.TryGetParticipant(context, id);
        if (participant is not null)
            LewdPoolHelper.MirrorArousal(participant, arousal);
        return Task.CompletedTask;
    }
}
