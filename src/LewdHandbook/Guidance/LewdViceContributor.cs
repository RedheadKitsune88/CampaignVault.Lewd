using CampaignVault.Data.Context;
using CampaignVault.Models;
using LewdHandbook.Mechanics;

namespace LewdHandbook.Guidance;

/// <summary>
/// Cravings don't wait for a lewd verb: each turn, a party member's addiction stage (from campaign time) becomes one
/// line, delivered once per stage per session, so the DM knows when withdrawal starts and when temptation needs a save.
/// Read-only: it never moves the clock; lewd_vice and the rest/travel hooks do.
/// </summary>
public sealed class LewdViceContributor : IPluginContextContributor
{
    public async Task<IEnumerable<PluginContextItem>> ContributeAsync(IContextTurn turn, CancellationToken ct = default)
    {
        if (TurnTime(turn) is not { } time)
            return [];
        var now = ViceState.HoursNow(time);
        var items = new List<PluginContextItem>();
        foreach (var id in turn.PartyCharacterIds)
        {
            var character = await turn.LoadCharacterAsync(id, ct).ConfigureAwait(false);
            if (character is null)
                continue;
            foreach (var def in ViceCatalog.All)
            {
                var stage = ViceTrack.Stage(character, def, now);
                if (stage is ViceStage.None or ViceStage.Sated)
                    continue;
                var hours = ViceTrack.HoursSince(character, def, now);
                var dc = ViceState.CurrentDc(character, def);
                var text = stage == ViceStage.Craving
                    ? $"{id} craves {def.Id} ({hours:0}h since last; withdrawal at {def.WithdrawalHours}h)."
                    : $"{id} is in {(stage == ViceStage.Severe ? "severe " : "")}withdrawal from {def.Id} ({hours:0}h): " +
                      $"near it → lewd_vice note_presence (save DC {dc}); long rest → addiction save. Resolve {ViceTrack.Resolve(character, def.Id)}.";
                items.Add(new PluginContextItem($"lewd:vice:{id}:{def.Id}:{stage}", text, Priority: 5));
            }
        }

        return items;
    }

    /// <summary>
    /// The turn's campaign time. PluginSdk 0.7.0 as published lacks <c>IContextTurn.Time</c>, but the host's turn
    /// carries a <c>Time</c> property, so it is read by name. Switch to <c>turn.Time</c> once the Sdk ships it.
    /// </summary>
    internal static CampaignTime? TurnTime(IContextTurn turn) =>
        turn.GetType().GetProperty("Time")?.GetValue(turn) as CampaignTime;
}
