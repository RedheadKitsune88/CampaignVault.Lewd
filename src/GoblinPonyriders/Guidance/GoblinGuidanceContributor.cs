using CampaignVault.Data.Guidance;
using CampaignVault.Models;
using GoblinPonyriders.Changes;
using GoblinPonyriders.Mechanics;

namespace GoblinPonyriders.Guidance;

/// <summary>
/// Nudges for Unified Clans. Installing the plugin does not world_build the faction —
/// when <c>goblinClans</c> is on (or just turned on), remind the LLM to seed
/// <c>factions/unified_clans</c> via world_build / <c>goblin_state</c> seed_faction.
/// </summary>
public sealed class GoblinGuidanceContributor : IPluginGuidanceContributor
{
    private static readonly PluginGuidanceHint SeedHint = new(
        "goblins.seed_unified_clans",
        "goblinClans is on but Unified Clans is not auto-created. Seed with world_build " +
        "(id factions/unified_clans, MilitaryOrder, theme goblin_ponyriders) or commit " +
        "goblin_state action=seed_faction once the faction is loaded to refresh lore. Skills: goblin-clans.",
        Priority: 7)
    {
        Example =
            """{"$type":"world_build","factions":[{"id":"factions/unified_clans","name":"Unified Clans","factionType":"MilitaryOrder"}]}""",
    };

    public Task<IEnumerable<PluginGuidanceHint>> EvaluateAsync(IGuidanceContext ctx, CancellationToken ct = default)
    {
        var settings = GoblinSettings.From(ctx.Config?.SystemOptions);
        if (!settings.Enabled && !JustEnabled(ctx))
            return Task.FromResult<IEnumerable<PluginGuidanceHint>>([]);

        var hints = new List<PluginGuidanceHint>();

        if (JustEnabled(ctx))
            hints.Add(SeedHint);

        if (ctx.AppliedChanges.OfType<GoblinStateChange>().Any())
        {
            hints.Add(new PluginGuidanceHint(
                "goblins.state",
                "Unified Clans beat: bondage via LewdHandbook lewd_bind/lewd_insert; goblin_state owns Defiance Clock, Clan Mark, roles, training. " +
                "If factions/unified_clans is missing, world_build it (seed_faction only refreshes when already loaded). Skills: goblin-clans, goblin-training.",
                Priority: 6));
        }
        else if (settings.Enabled && hints.Count == 0)
        {
            // Existing campaigns that already had goblinClans=on (or plugin freshly dropped in):
            // host delivers each key once unless RepeatAfterDays is set.
            hints.Add(SeedHint with { RepeatAfterDays = 7 });
        }

        return Task.FromResult<IEnumerable<PluginGuidanceHint>>(hints);
    }

    private static bool JustEnabled(IGuidanceContext ctx) =>
        ctx.AppliedChanges.OfType<CampaignUpdateChange>().Any(c =>
            c.SystemOptions is { Count: > 0 } opts &&
            opts.Any(kv =>
                string.Equals(kv.Key, GoblinKeys.ClansOption, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(kv.Value, GoblinKeys.OptionOn, StringComparison.OrdinalIgnoreCase)));
}
