using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Handbook: after a short or long rest a (visibly) pregnant creature makes a DC 15 Con save or is poisoned for 1d4 hours.
/// One save per rest: the guard is the campaign hour the rest ended, so two rests on the same day each get their save
/// and the rest event plus a manual <c>action=rest</c> for the same rest never double up.
/// </summary>
internal static class PregnancyRest
{
    public static async Task<string?> SaveAsync(
        IChangeContext context,
        Character character,
        float nowHours,
        int d20,
        int? conOverride,
        CancellationToken ct)
    {
        if (!PregnancyState.IsVisible(character))
            return null;
        if (Math.Abs(PregnancyState.Attr(character, PregnancyState.RestCheckHoursKey) - nowHours) < 0.01f)
            return $"{character.Id} pregnancy rest save already resolved for this rest.";

        var mod = AbilityScores.Resolve(character, "con", conOverride);
        if (d20 == 0 && context.Rolls is null)
            return $"{character.Id} pregnancy rest save pending: emit lewd_pregnancy action=rest with d20 (Rolls unavailable).";

        var roll = await SaveDice.RollAsync(context, "lewd_pregnancy_rest", d20, mod, disadvantage: false, ct,
            who: character, subject: "con").ConfigureAwait(false);
        if (roll.Error is not null)
            return $"{character.Id} pregnancy rest: {roll.Error}";

        PregnancyState.SetAttr(character, PregnancyState.RestCheckHoursKey, nowHours);
        if (roll.Total >= PregnancyMath.RestPoisonDc)
            return $"{character.Id} pregnancy rest save {roll.Summary} vs DC {PregnancyMath.RestPoisonDc}: saved.";

        var hours = await LewdDice.RollAsync(context, "lewd_pregnancy_poison_hours", 1, 4, ct: ct).ConfigureAwait(false);
        PregnancyState.StampPoisoned(character, nowHours, hours.Total);
        return $"{character.Id} pregnancy rest save {roll.Summary} vs DC {PregnancyMath.RestPoisonDc}: poisoned for {hours.Total} hour(s) ({hours.Summary}).";
    }
}
