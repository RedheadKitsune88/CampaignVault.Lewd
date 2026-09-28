using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace GoblinPonyriders.Mechanics;

/// <summary>
/// Unified Clans faction seed helpers. New factions persist via <c>world_build</c> (plugins cannot
/// Store through <see cref="IChangeContext"/>); this updates an already-loaded faction or returns
/// the payload the LLM should commit.
/// </summary>
internal static class GoblinFaction
{
    public const string LoreBlurb =
        "Scattered goblin clans forged into one riding war-culture after tall-folk raids and slave hunts. " +
        "The Cult of the Dragon secretly enhanced several goblin shamans as a terror weapon — sharper minds, " +
        "shared doctrine, cruel discipline — then lost control. The Unified Clans turned that gift against " +
        "everyone: revenge raids, living mounts (Goblin Ponyriders), and marked captives as warnings. " +
        "No gore doctrine; terror and breeding stock over corpses.";

    public static Faction CreateSeed(string? campaignName = null) => new()
    {
        Id = GoblinKeys.FactionId,
        Name = GoblinKeys.FactionName,
        Description = LoreBlurb,
        FactionType = FactionType.MilitaryOrder,
        InfluenceLevel = 55,
        CampaignName = campaignName,
        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["theme"] = "goblin_ponyriders",
            ["plugin"] = GoblinKeys.PluginId,
            ["origin"] = "cult_of_the_dragon_shaman_enhancement_gone_rogue",
            ["doctrine"] = "alive_capture_revenge_terror_release",
            ["speech"] = "broken_common_vicious_taunts",
        },
    };

    public static string WorldBuildHint() =>
        """
        Seed Unified Clans with world_build factions entry:
        { "id": "factions/unified_clans", "name": "Unified Clans", "factionType": "MilitaryOrder",
          "influenceLevel": 55,
          "description": "Scattered goblin clans forged into one riding war-culture after tall-folk raids. Cult of the Dragon enhanced shamans as a terror weapon; they went rogue. Revenge doctrine: alive capture, Goblin Ponyriders mounts, terror-release warnings.",
          "metadata": { "theme": "goblin_ponyriders", "plugin": "com.campaignvault.goblins",
            "origin": "cult_of_the_dragon_shaman_enhancement_gone_rogue",
            "doctrine": "alive_capture_revenge_terror_release" } }
        """;

    public static bool TryGet(IChangeContext context, out Faction faction) =>
        context.Factions.TryGetValue(GoblinKeys.FactionId, out faction!);

    /// <summary>Refresh lore/metadata on an already-loaded faction; returns false if missing.</summary>
    public static bool RefreshIfPresent(IChangeContext context, out string message)
    {
        if (!TryGet(context, out var faction))
        {
            message = $"Faction '{GoblinKeys.FactionId}' is not loaded. {WorldBuildHint()}";
            return false;
        }

        faction.Name = GoblinKeys.FactionName;
        faction.Description = LoreBlurb;
        faction.FactionType = FactionType.MilitaryOrder;
        faction.Metadata["theme"] = "goblin_ponyriders";
        faction.Metadata["plugin"] = GoblinKeys.PluginId;
        faction.Metadata["origin"] = "cult_of_the_dragon_shaman_enhancement_gone_rogue";
        faction.Metadata["doctrine"] = "alive_capture_revenge_terror_release";
        faction.Metadata["speech"] = "broken_common_vicious_taunts";
        faction.LastUpdated = DateTime.UtcNow;
        message = $"{GoblinKeys.FactionName} ({GoblinKeys.FactionId}) lore/metadata refreshed.";
        return true;
    }
}
