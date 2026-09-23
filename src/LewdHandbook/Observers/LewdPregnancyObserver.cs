using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Observers;

/// <summary>
/// Host <see cref="RestChange"/> is the pregnancy rest-poison clock. The explicit
/// <c>lewd_pregnancy action=rest</c> verb remains as a Rolls-null fallback.
/// </summary>
public sealed class LewdPregnancyObserver : IWorldChangeObserver
{
    public bool IsInterestedIn(WorldChange committed, IChangeContext context) =>
        committed is RestChange;

    public async Task OnCommittedAsync(WorldChange committed, IChangeContext context, CancellationToken ct = default)
    {
        if (committed is not RestChange rest)
            return;
        if (string.IsNullOrWhiteSpace(rest.CharacterId) ||
            !context.Characters.TryGetValue(rest.CharacterId, out var character))
            return;
        if (!PregnancyState.Flag(character, LewdKeys.Pregnant))
            return;

        var day = await ImprintState.DayAsync(context, ct).ConfigureAwait(false);
        if (PregnancyState.Int(character, LewdKeys.PregnancyRestPoisonDay) == day && day != 0)
            return;

        if (context.Rolls is null)
        {
            context.RecordMessage(
                $"{rest.CharacterId} pregnant rest poison pending. Emit lewd_pregnancy action=rest with d20 and targetConModifier (Rolls unavailable).");
            return;
        }

        var mod = AbilityScores.Mod(character, "con");
        var roll = await SaveDice.RollAsync(
            context, "lewd_pregnancy_rest", faceOrZero: 0, mod, disadvantage: false, ct).ConfigureAwait(false);
        if (roll.Error is not null)
        {
            context.RecordMessage($"{rest.CharacterId} pregnancy rest: {roll.Error}");
            return;
        }

        PregnancyState.Set(character, LewdKeys.PregnancyRestPoisonDay, day.ToString());
        if (roll.Total < PregnancyMath.RestPoisonDc)
            PregnancyState.StampPoisoned(character);

        context.RecordMessage(
            $"Lewd pregnancy rest {rest.CharacterId}: {roll.Summary} vs DC {PregnancyMath.RestPoisonDc} → {(roll.Total < PregnancyMath.RestPoisonDc ? "poisoned 1d4 hours" : "saved")}.");
    }
}
