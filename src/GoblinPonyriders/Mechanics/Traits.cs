using CampaignVault.Models;

namespace GoblinPonyriders.Mechanics;

internal static class Traits
{
    public static string Get(Character character, string key) =>
        character.SystemStats.Traits.TryGetValue(key, out var value) ? value ?? "" : "";

    public static int GetInt(Character character, string key, int fallback = 0) =>
        int.TryParse(Get(character, key), out var n) ? n : fallback;

    public static bool Flag(Character character, string key) =>
        string.Equals(Get(character, key), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Get(character, key), "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Get(character, key), "yes", StringComparison.OrdinalIgnoreCase);

    public static void Set(Character character, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            character.SystemStats.Traits.Remove(key);
        else
            character.SystemStats.Traits[key] = value;
    }

    public static void SetInt(Character character, string key, int value) =>
        Set(character, key, value.ToString());

    public static void SetFlag(Character character, string key, bool value) =>
        Set(character, key, value ? "true" : null);
}
