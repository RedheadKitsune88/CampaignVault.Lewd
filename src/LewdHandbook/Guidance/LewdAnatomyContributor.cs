using CampaignVault.Data.Context;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Guidance;

/// <summary>
/// Completeness check on <c>lewd_encounter</c> entry: bodies differ, so the plugin derives mouth/hands/ass but cannot
/// know whether a character has a cock, a pussy or breasts. For each adult participant that has not said (a declared
/// part or an explicit <c>none</c>), one line asks the DM to fill the gap before narrating. The key carries the missing
/// set, so it is re-delivered only when the answer changes.
/// </summary>
public sealed class LewdAnatomyContributor : IPluginContextContributor
{
    /// <summary>Loads per turn, so a large participant list stays cheap.</summary>
    internal const int MaxChecked = 8;

    public async Task<IEnumerable<PluginContextItem>> ContributeAsync(IContextTurn turn, CancellationToken ct = default)
    {
        var entered = turn.AppliedChanges.OfType<ModeTransitionChange>().FirstOrDefault(m =>
            string.Equals(m.ModeId, LewdEncounterMode.ModeIdValue, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.Action, "enter", StringComparison.OrdinalIgnoreCase));
        if (entered is null)
            return [];

        var gaps = new List<string>();
        foreach (var id in entered.ParticipantIds.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxChecked))
        {
            var character = await turn.LoadCharacterAsync(id, ct).ConfigureAwait(false);
            // Never prompt for anatomy of someone the age gate refuses.
            if (character is null || !AgeGate.TryPass(character, id, out _))
                continue;

            var missing = Missing(character);
            if (missing.Count > 0)
                gaps.Add($"{character.Id} ({string.Join(",", missing)})");
        }

        if (gaps.Count == 0)
            return [];

        gaps.Sort(StringComparer.OrdinalIgnoreCase);
        var list = string.Join("; ", gaps);
        return
        [
            new PluginContextItem(
                $"lewd:anatomy:{list}",
                $"Anatomy undeclared: {list}. Before narrating, character_update Trait lewd_encounter.anatomy.<slot> " +
                "(die=…;tags=…) for parts they have, or =none for parts they lack.",
                Priority: 6),
        ];
    }

    /// <summary>The asked slots (cock, pussy, breasts) the character neither declared nor marked absent.</summary>
    internal static List<string> Missing(Character character) =>
        AnatomySlots.Asked.Where(slot => !AnatomyTraits.IsDeclared(character, slot)).ToList();
}
