using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>Handbook conditions that put a save at disadvantage, so the plugin's own saves honour them.</summary>
internal static class SaveConditions
{
    /// <summary>
    /// Intoxicated (and so nymphomanic, which is hyperaroused + intoxicated) has disadvantage on Int, Wis and Cha saves;
    /// flustered on Wis and Cha saves.
    /// </summary>
    public static bool Disadvantage(Character? character, string ability)
    {
        if (character is null)
            return false;

        var a = (ability ?? "").Trim().ToLowerInvariant();
        var mental = a is "int" or "wis" or "cha";
        if (mental && (PregnancyState.HasCondition(character, LewdKeys.ConditionIntoxicated) ||
                       PregnancyState.HasCondition(character, LewdKeys.ConditionNymphomanic)))
            return true;
        return a is "wis" or "cha" && PregnancyState.HasCondition(character, "flustered");
    }
}
