using CampaignVault.Plugins;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Migrates Lewd Traits keys so <c>NpcCard.SystemTraits</c> shows the right ones:
/// <list type="bullet">
/// <item>catalog/track/bookkeeping keys move under the <c>lewd_encounter.</c> prefix (hidden outside a scene);</item>
/// <item><c>bindings</c> and <c>posture</c> move back out (a captive's cuffs outlast the scene);</item>
/// <item>anatomy aliases are renamed to the canonical slot, and a character that already has a lewd profile gets the
/// universal slots (mouth, hands, ass) filled in.</item>
/// </list>
/// Idempotent: safe on every character load. The host hands over only the Traits bag, so the upgrader cannot see a
/// character's life stage or body plan: it fills slots only where a lewd profile already exists, and the runtime derives
/// the same defaults for everyone else (<see cref="AnatomyTraits.Effective"/>).
/// </summary>
public sealed class LewdTraitsUpgrader : IPluginTraitsUpgrader
{
    public const string PluginIdValue = "com.campaignvault.lewd-handbook";

    public string PluginId => PluginIdValue;

    public bool TryUpgrade(IDictionary<string, string> traits)
    {
        var changed = false;
        changed |= MoveExact(traits, LewdKeys.LegacyTraitSexualHistory, LewdKeys.TraitSexualHistory);
        changed |= MoveExact(traits, LewdKeys.LegacyTraitRecoveryDie, LewdKeys.TraitRecoveryDie);
        changed |= MoveExact(traits, LewdKeys.LegacyTraitImplementProficiencies, LewdKeys.TraitImplementProficiencies);
        changed |= MovePrefixed(traits, LewdKeys.LegacyAnatomyPrefix, LewdKeys.AnatomyPrefix);
        changed |= MovePrefixed(traits, LewdKeys.LegacyVicePrefix, LewdKeys.ModeTraitPrefix + LewdKeys.LegacyVicePrefix, ViceState.TraitSuffixes);
        // Retired in 0.2: the pregnancy rest guard is the Attribute pregnancy.rest_check_hours now.
        changed |= TryRemove(traits, LewdKeys.PregnancyRestPoisonDay, out _);

        // Bookkeeping that used to clutter every card.
        foreach (var bare in LewdKeys.HiddenBareTraits)
            changed |= MoveExact(traits, bare, LewdKeys.ModeTraitPrefix + bare);
        foreach (var prefix in LewdKeys.HiddenBarePrefixes)
            changed |= MovePrefixed(traits, prefix, LewdKeys.ModeTraitPrefix + prefix);

        // Restraint is visible outside a scene: the prefix goes away again.
        changed |= MoveExact(traits, LewdKeys.LegacyModeTraitBindings, LewdKeys.TraitBindings);
        changed |= MoveExact(traits, LewdKeys.LegacyModeTraitPosture, LewdKeys.TraitPosture);

        changed |= NormalizeAnatomyAliases(traits);
        changed |= FillUniversalAnatomy(traits);
        return changed;
    }

    /// <summary><c>anatomy.penis</c> → <c>anatomy.cock</c>. An alias next to its canonical key is left for the reader to ignore.</summary>
    private static bool NormalizeAnatomyAliases(IDictionary<string, string> traits)
    {
        var changed = false;
        foreach (var key in traits.Keys.ToList())
        {
            if (!key.StartsWith(LewdKeys.AnatomyPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var canonical = AnatomyTraits.NormalizeAnatomyKey(key);
            if (string.Equals(canonical, key, StringComparison.Ordinal))
                continue;
            if (!TryRemove(traits, key, out var value))
                continue;
            if (!traits.ContainsKey(canonical))
                traits[canonical] = value;
            changed = true;
        }

        return changed;
    }

    private static readonly string[] ProfileKeys =
    [
        LewdKeys.AnatomyPrefix, LewdKeys.TraitSexualHistory, LewdKeys.TraitRecoveryDie, LewdKeys.TraitImplementProficiencies,
        LewdKeys.TraitStance, LewdKeys.TraitAllowedPartners, LewdKeys.TraitHardLimits, LewdKeys.TraitSoftLimits,
        LewdKeys.TraitKinks, LewdKeys.TraitInhibition,
    ];

    /// <summary>
    /// The DM has opted this character into the lewd profile. Vice, fertility and other bookkeeping alone do not count:
    /// they can arise for anyone, and the upgrader cannot see life stage.
    /// </summary>
    private static bool HasLewdProfile(IDictionary<string, string> traits) =>
        traits.Keys.Any(k => ProfileKeys.Any(p => k.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Adds mouth/hands/ass defaults to a character that already has a lewd profile. Never overwrites anything, and never
    /// touches a slot the DM marked <c>none</c> or a <c>plan=custom</c> body.
    /// </summary>
    private static bool FillUniversalAnatomy(IDictionary<string, string> traits)
    {
        var plan = LewdKeys.AnatomyPrefix + AnatomySlots.PlanSuffix;
        if (!HasLewdProfile(traits) ||
            traits.Any(t => string.Equals(t.Key, plan, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(t.Value?.Trim(), AnatomySlots.PlanCustom, StringComparison.OrdinalIgnoreCase)))
            return false;

        var changed = false;
        foreach (var slot in AnatomySlots.All.Where(s => s.Universal))
        {
            var names = slot.Aliases.Prepend(slot.Name).ToList();
            if (traits.Keys.Any(k => names.Any(n => string.Equals(k, LewdKeys.AnatomyPrefix + n, StringComparison.OrdinalIgnoreCase))))
                continue;
            traits[LewdKeys.AnatomyPrefix + slot.Name] = slot.Default;
            changed = true;
        }

        return changed;
    }

    private static bool MoveExact(IDictionary<string, string> traits, string from, string to)
    {
        if (!TryRemove(traits, from, out var value))
            return false;
        if (!traits.ContainsKey(to))
            traits[to] = value;
        return true;
    }

    /// <summary>
    /// Moves <paramref name="fromPrefix"/>* → <paramref name="toPrefix"/>* .
    /// When <paramref name="requiredSuffixes"/> is set, only keys whose final segment is in that set move
    /// (vice Traits only — Attributes stay on the bare <c>vice.</c> prefix).
    /// </summary>
    private static bool MovePrefixed(
        IDictionary<string, string> traits,
        string fromPrefix,
        string toPrefix,
        IReadOnlyList<string>? requiredSuffixes = null)
    {
        var changed = false;
        foreach (var key in traits.Keys.ToList())
        {
            if (!key.StartsWith(fromPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            // Skip keys already under the mode prefix (e.g. lewd_encounter.vice.* must not match vice.*).
            if (key.StartsWith(LewdKeys.ModeTraitPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var suffix = key[fromPrefix.Length..];
            if (requiredSuffixes is not null)
            {
                var lastDot = suffix.LastIndexOf('.');
                var leaf = lastDot >= 0 ? suffix[(lastDot + 1)..] : suffix;
                if (!requiredSuffixes.Any(s => string.Equals(s, leaf, StringComparison.OrdinalIgnoreCase)))
                    continue;
            }

            if (!TryRemove(traits, key, out var value))
                continue;
            var dest = toPrefix + suffix;
            if (!traits.ContainsKey(dest))
                traits[dest] = value;
            changed = true;
        }

        return changed;
    }

    private static bool TryRemove(IDictionary<string, string> traits, string key, out string value)
    {
        if (traits.Remove(key, out value!))
            return true;
        foreach (var existing in traits.Keys.ToList())
        {
            if (!string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))
                continue;
            value = traits[existing];
            traits.Remove(existing);
            return true;
        }

        value = "";
        return false;
    }
}
