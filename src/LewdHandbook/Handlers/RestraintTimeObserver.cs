using CampaignVault.Data.ChangeHandlers;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Handlers;

/// <summary>
/// Time passing on a bound character wears them down (see <see cref="RestraintStrain"/>). Runs after the host's own sweep,
/// for the characters that lived the span. Adults only, like every lewd mechanic.
/// </summary>
public sealed class RestraintTimeObserver : IWorldTimeObserver
{
    public async Task OnTimeAdvancedAsync(TimeAdvance advance, IChangeContext context, CancellationToken ct = default)
    {
        if (advance.Hours <= 0 || advance.CharacterIds.Count == 0)
            return;

        var settings = await LewdSettings.ResolveAsync(context, ct).ConfigureAwait(false);
        foreach (var id in advance.CharacterIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!context.Characters.TryGetValue(id, out var character) || character.SystemStats is null)
                continue;
            if (!AgeGate.TryPass(character, id, out _))
                continue;

            var bindings = BindingGraph.GetBindings(null, character);
            if (bindings.Count == 0)
                continue;

            foreach (var message in RestraintStrain.Advance(character, bindings, advance.Hours, advance.TotalHoursSoFar / 24.0, settings.NonConsent))
            {
                context.RecordMessage(message);
                context.RecordPhysicalStateNudge(message);
            }
        }
    }
}
