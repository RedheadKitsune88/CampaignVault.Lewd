using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class AbilityScores
{
    public static int Mod(Character? character, string? ability)
    {
        if (character?.SystemStats is not Dnd5eExtension dnd)
            return 0;

        var key = Normalize(ability);
        if (key is not ("str" or "dex" or "con" or "int" or "wis" or "cha"))
            return 0;

        var score = key switch
        {
            "str" => dnd.Strength,
            "dex" => dnd.Dexterity,
            "con" => dnd.Constitution,
            "int" => dnd.Intelligence,
            "wis" => dnd.Wisdom,
            _ => dnd.Charisma,
        };
        return (int)Math.Floor((score - 10) / 2.0);
    }

    /// <summary>Explicit override wins (including 0). Null means read the sheet.</summary>
    public static int Resolve(Character? character, string? ability, int? overrideMod) =>
        overrideMod ?? Mod(character, ability);

    public static string Normalize(string? ability)
    {
        var a = (ability ?? "").Trim().ToLowerInvariant();
        return a switch
        {
            "strength" => "str",
            "dexterity" => "dex",
            "constitution" => "con",
            "intelligence" => "int",
            "wisdom" => "wis",
            "charisma" => "cha",
            _ => a,
        };
    }
}
