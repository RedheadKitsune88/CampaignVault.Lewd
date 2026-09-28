using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// What the plugin does to willpower (0-100, the host's <see cref="SystemExtension.Willpower"/>): captivity wears it down. Whether
/// that matters, and how it comes back, is core's business: weak willpower moves charm, fear, compulsion and mental saves, and each
/// rest step returns what <see cref="SystemExtension.WillpowerDrained"/> says was taken. The plugin only records the loss there.
/// </summary>
internal static class Willpower
{
    /// <summary>Lowers willpower by up to <paramref name="amount"/> (not below 0) and records it as recoverable. Returns what was lost.</summary>
    public static float Drain(Character character, float amount)
    {
        var stats = character.SystemStats;
        var lost = Math.Min(Math.Max(0f, amount), Math.Max(0f, stats.Willpower));
        if (lost <= 0)
            return 0;
        stats.Willpower -= lost;
        stats.WillpowerDrained += lost;
        return lost;
    }
}
