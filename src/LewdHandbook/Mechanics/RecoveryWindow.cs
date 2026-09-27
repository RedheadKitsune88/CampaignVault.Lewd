using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// When Recovery Dice may be spent: right after a climax (until that character's next turn starts) or within an hour of
/// finishing a short rest. Opened by the climax / lewd_rest, closed by lewd_recover.
/// </summary>
internal static class RecoveryWindow
{
    public const string Climax = "climax";
    public const string ShortRest = "short_rest";
    private const string KindKey = LewdKeys.ModeTraitPrefix + "recovery_window";
    private const string HoursKey = "lewd.recovery_window_hours";

    public static void Open(Character character, string kind, float nowHours)
    {
        character.SystemStats.Traits[KindKey] = kind;
        character.SystemStats.Attributes[HoursKey] = nowHours;
    }

    public static void Close(Character character, string? onlyKind = null)
    {
        if (onlyKind is not null && !string.Equals(Kind(character), onlyKind, StringComparison.OrdinalIgnoreCase))
            return;
        character.SystemStats.Traits.Remove(KindKey);
        character.SystemStats.Attributes.Remove(HoursKey);
    }

    public static string? Kind(Character character) =>
        character.SystemStats.Traits.TryGetValue(KindKey, out var kind) && !string.IsNullOrWhiteSpace(kind) ? kind : null;

    public static string? OpenKind(Character character, float nowHours)
    {
        var kind = Kind(character);
        if (kind is null)
            return null;
        if (kind == Climax)
            return kind;
        var opened = character.SystemStats.Attributes.GetValueOrDefault(HoursKey, float.MinValue);
        return nowHours - opened <= 1f ? kind : null;
    }
}
