using CampaignVault.Data;
using CampaignVault.Data.Context;
using CampaignVault.Models;

namespace LewdHandbook.Guidance;

/// <summary>
/// Retroactive <c>lifeStage</c>: characters from before 0.6.0 (and new ones the DM forgot) are Unspecified, and every
/// lewd verb refuses them. In a campaign with lewd_encounter enabled, this names the party / involved characters still
/// unset, once per session per set, so the DM records their stage from the fiction before a scene needs it. The
/// plugin never guesses a stage itself.
/// </summary>
public sealed class LewdLifeStageContributor : IPluginContextContributor
{
    /// <summary>Loads per turn, so a large involved list stays cheap.</summary>
    internal const int MaxChecked = 8;

    public async Task<IEnumerable<PluginContextItem>> ContributeAsync(IContextTurn turn, CancellationToken ct = default)
    {
        if (turn.Config?.EnabledModeIds?.Contains(LewdEncounterMode.ModeIdValue, StringComparer.OrdinalIgnoreCase) != true)
            return [];

        var ids = turn.PartyCharacterIds
            .Concat(turn.InvolvedEntityIds)
            .Where(id => id.StartsWith(CanonicalId.Characters, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxChecked)
            .ToList();

        var unset = new List<string>();
        foreach (var id in ids)
        {
            var character = await turn.LoadCharacterAsync(id, ct).ConfigureAwait(false);
            if (character is { LifeStage: LifeStage.Unspecified })
                unset.Add(character.Id);
        }

        if (unset.Count == 0)
            return [];

        unset.Sort(StringComparer.OrdinalIgnoreCase);
        var list = string.Join(", ", unset);
        return
        [
            new PluginContextItem(
                $"lewd:lifestage:{list}",
                $"lifeStage unset: {list}. Record it from the fiction (character_update lifeStage adult|elder|adolescent|child); " +
                "lewd verbs refuse unset characters.",
                Priority: 7),
        ];
    }
}
