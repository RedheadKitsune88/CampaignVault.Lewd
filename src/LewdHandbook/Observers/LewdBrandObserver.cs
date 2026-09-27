using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

public sealed class LewdBrandObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context) =>
        committed is HpChange or StatusRemove
            or LewdAdvanceChange or LewdClimaxCheckChange or LewdPregnancyChange;

    public Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        var id = CharacterId(committed);
        if (string.IsNullOrWhiteSpace(id) || !context.Characters.TryGetValue(id, out var character))
            return Task.CompletedTask;

        var participant = LewdModeAccess.TryGetActive(context)?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));

        switch (committed)
        {
            case StatusRemove remove:
                BrandState.Restamp(character, remove.Status, context);
                break;
            case HpChange hp when hp.Delta > 0:
                // Altruism triggers on the healer, whom HpChange does not name: lewd_apply_brand action=heal.
                BrandState.RelockDenial(character, context);
                break;
            case LewdPregnancyChange:
                if (BrandState.Has(character, BrandCatalog.Fertility))
                {
                    BrandState.StampFertileHeat(character);
                    BrandState.ClearFertileHeat(character);
                }

                BrandState.RelockDenial(character, context);
                break;
            case LewdAdvanceChange or LewdClimaxCheckChange:
                BrandState.RelockDenial(character, context);
                NoteEchoes(committed, context, id);
                break;
        }

        if (BrandState.TierSum(character) > 0)
            BrandState.Mirror(participant, character);
        return Task.CompletedTask;
    }

    /// <summary>Clears the just-climaxed marker; Echoes itself is <see cref="Handlers.LewdEchoesHandler"/>.</summary>
    private static void NoteEchoes(WorldChange committed, IChangeContext context, string climaxId)
    {
        if (LewdModeAccess.TryGetParticipant(context, climaxId) is { } climaxed)
            climaxed.State[LewdKeys.LustbrandJustClimaxed] = false;
        _ = committed;
    }

    private static string? CharacterId(WorldChange change) => change switch
    {
        HpChange hp => hp.CharacterId,
        StatusRemove remove => remove.CharacterId,
        LewdAdvanceChange advance => advance.TargetId,
        LewdClimaxCheckChange check => check.TargetId,
        LewdPregnancyChange pregnancy => pregnancy.TargetId,
        _ => null,
    };
}
