using CampaignVault.Plugins;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Retires the old <c>intimacyTone</c> option onto the player-owned pair that replaced it:
/// <c>consensual</c> → lewdNonConsent off; <c>grimdark</c> → on; <c>fade</c> → on plus lewdNarration fade.
/// A value the player already set under a new key is never overwritten. An unknown tone maps to off.
/// Runs at host startup over every campaign; idempotent.
/// </summary>
public sealed class LewdCampaignOptionsUpgrader : IPluginCampaignOptionsUpgrader
{
    public const string LegacyIntimacyTone = "intimacyTone";

    public string PluginId => LewdTraitsUpgrader.PluginIdValue;

    public bool TryUpgrade(IDictionary<string, string> systemOptions)
    {
        var legacyKey = FindKey(systemOptions, LegacyIntimacyTone);
        if (legacyKey is null)
            return false;

        var tone = (systemOptions[legacyKey] ?? "").Trim().ToLowerInvariant();
        systemOptions.Remove(legacyKey);

        var nonConsent = tone is "grimdark" or "fade" ? LewdKeys.NonConsentOn : LewdKeys.NonConsentOff;
        SetIfAbsent(systemOptions, LewdKeys.NonConsentOption, nonConsent);
        if (tone == "fade")
            SetIfAbsent(systemOptions, LewdKeys.NarrationOption, LewdKeys.NarrationFade);
        return true;
    }

    private static void SetIfAbsent(IDictionary<string, string> options, string key, string value)
    {
        if (FindKey(options, key) is null)
            options[key] = value;
    }

    private static string? FindKey(IDictionary<string, string> options, string key) =>
        options.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
}
