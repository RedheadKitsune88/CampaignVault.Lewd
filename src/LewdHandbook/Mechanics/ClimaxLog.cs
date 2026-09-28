using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// When a character climaxed, on the sheet (game hours), so "climaxes in the past minute / hour" survives scenes.
/// The campaign clock only has hour resolution, so "the past minute" is read as "at the same clock reading".
/// </summary>
internal static class ClimaxLog
{
    private const string Prefix = "lewd.climax_at.";
    private const int Keep = 16;
    public const float Hour = 1f;

    public static void Record(Character character, float nowHours)
    {
        var attrs = character.SystemStats.Attributes;
        foreach (var key in attrs.Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal) && nowHours - attrs[k] >= Hour).ToList())
            attrs.Remove(key);

        var used = attrs.Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal)).ToList();
        if (used.Count >= Keep)
            attrs.Remove(used.OrderBy(k => attrs[k]).First());

        for (var i = 0; i < Keep; i++)
        {
            if (attrs.ContainsKey(Prefix + i))
                continue;
            attrs[Prefix + i] = nowHours;
            return;
        }
    }

    /// <summary>Climaxes recorded no more than <paramref name="windowHours"/> ago (0 = at this very clock reading).</summary>
    public static int Count(Character character, float nowHours, float windowHours) =>
        character.SystemStats.Attributes.Count(kv =>
            kv.Key.StartsWith(Prefix, StringComparison.Ordinal) && nowHours - kv.Value <= windowHours && nowHours - kv.Value >= 0);
}
