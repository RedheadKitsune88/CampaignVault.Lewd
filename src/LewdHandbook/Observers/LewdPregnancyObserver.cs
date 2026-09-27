using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

/// <summary>
/// Pregnancy follows campaign time: travel, activities and anything that passes minutes move its progress, show it,
/// flag the term and lift an expired rest poison. Rests go through <c>lewd_rest</c>, which also rolls the rest save.
/// </summary>
public sealed class LewdPregnancyObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context) =>
        CharacterIdOf(committed) is not null && (committed is TravelChange || committed.MinutesElapsed is > 0);

    public async Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        var id = CharacterIdOf(committed);
        if (string.IsNullOrWhiteSpace(id) || !context.Characters.TryGetValue(id, out var character))
            return;
        if (!PregnancyState.Flag(character, LewdKeys.Pregnant) &&
            PregnancyState.Attr(character, PregnancyState.PoisonedUntilKey) < 0)
            return;

        var now = await ViceState.HoursNowAsync(context, ct).ConfigureAwait(false);
        var note = PregnancyState.Sync(character, now, context);
        if (note is not null)
            context.RecordMessage(note);
    }

    private static string? CharacterIdOf(WorldChange committed) => committed switch
    {
        TravelChange travel => travel.CharacterId,
        ActivityChange activity => activity.CharacterId,
        NeedChange need => need.CharacterId,
        ScheduleChange schedule => schedule.CharacterId,
        _ => null,
    };
}
