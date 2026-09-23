using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

public sealed class LewdBrandObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context) =>
        committed is RestChange or HpChange or StatusRemove
            or LewdAdvanceChange or LewdClimaxCheckChange or LewdPregnancyChange;

    public Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        var id = CharacterId(committed);
        if (string.IsNullOrWhiteSpace(id) || !context.Characters.TryGetValue(id, out var character))
            return Task.CompletedTask;

        var participant = context.ActiveMode?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, id, StringComparison.OrdinalIgnoreCase));

        switch (committed)
        {
            case StatusRemove remove:
                BrandState.Restamp(character, remove.Status, context);
                break;
            case RestChange rest when IsShortOrLong(rest):
                BrandState.OnRest(character, participant, IsLong(rest), context);
                break;
            case HpChange hp when hp.Delta > 0:
                BrandState.OnHeal(character, participant, hp.Delta, context);
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

    private static void NoteEchoes(WorldChange committed, IChangeContext context, string climaxId)
    {
        var mode = context.ActiveMode;
        if (mode is null)
            return;
        var climaxed = mode.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, climaxId, StringComparison.OrdinalIgnoreCase));
        if (climaxed is null || !ConsentGate.GetBool(climaxed, LewdKeys.LustbrandJustClimaxed))
            return;
        climaxed.State[LewdKeys.LustbrandJustClimaxed] = false;
        foreach (var other in mode.Participants)
        {
            if (string.Equals(other.CharacterId, climaxId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!context.Characters.TryGetValue(other.CharacterId, out var otherChar))
                continue;
            if (!BrandState.Has(otherChar, BrandCatalog.Echoes))
                continue;
            context.RecordMessage(
                $"{other.CharacterId} has Brand of Echoes. If within 5 ft of {climaxId}'s climax, emit lewd_climax_check forceClimax. Do not assume distance.");
        }

        _ = committed;
    }

    private static bool IsShortOrLong(RestChange rest) =>
        rest.RestType is RestType.LongRest or RestType.ShortRest ||
        (rest.RestType is null && rest.IntendedHours >= 1);

    private static bool IsLong(RestChange rest) =>
        rest.RestType == RestType.LongRest || (rest.RestType is null && rest.IntendedHours >= 8);

    private static string? CharacterId(WorldChange change) => change switch
    {
        RestChange rest => rest.CharacterId,
        HpChange hp => hp.CharacterId,
        StatusRemove remove => remove.CharacterId,
        LewdAdvanceChange advance => advance.TargetId,
        LewdClimaxCheckChange check => check.TargetId,
        LewdPregnancyChange pregnancy => pregnancy.TargetId,
        _ => null,
    };
}
