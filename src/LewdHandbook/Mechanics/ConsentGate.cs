using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class ConsentGate
{
    /// <summary>
    /// Revoked stance, the player's campaign hard limits and the character's own hard limits always refuse.
    /// An advance the character doesn't want resolves only when <see cref="LewdSettings.NonConsent"/> allows it.
    /// </summary>
    public static bool AuthorizeAdvance(
        ModeParticipantState target,
        Character? targetChar,
        string actorId,
        string? stimulationType,
        IEnumerable<string>? tags,
        LewdSettings settings,
        out string? error)
    {
        if (LewdProfile.IsRevoked(target, targetChar))
        {
            error = $"Target '{target.CharacterId}' has revoked consent; advance refused.";
            return false;
        }

        var probe = BuildProbe(stimulationType, tags);
        if (settings.HitsHardLimit(probe, out var campaignHit))
        {
            error = $"Campaign hard limit '{campaignHit}' blocks this advance.";
            return false;
        }

        var personal = LewdProfile.HardLimits(target, targetChar).FirstOrDefault(probe.Contains);
        if (personal is not null)
        {
            error = $"Target '{target.CharacterId}' hard limit '{personal}' blocks this advance.";
            return false;
        }

        if (!LewdProfile.Wants(target, targetChar, actorId))
            return settings.AllowsUnwanted(targetChar, target.CharacterId, out error);

        error = null;
        return true;
    }

    /// <summary>
    /// Inhibition bonus against an advance: a willing partner treats it as 0 unless already negative (handbook),
    /// then Lustbrand tiers and unwilling imprint levels lower it further.
    /// </summary>
    public static int EffectiveInhibition(
        ModeParticipantState target, bool advanceIsWanted, Character? character = null, string? actorId = null)
    {
        var raw = LewdProfile.Inhibition(target, character);
        if (advanceIsWanted)
            raw = Math.Min(0, raw);
        return raw - InhibitionPenalty(target, character, actorId);
    }

    /// <summary>
    /// Climax saves use raw Inhibition (no willing clamp), still lowered by brands and imprints — imprints tied to someone
    /// count when that someone gave the latest stimulation.
    /// </summary>
    public static int ClimaxInhibition(ModeParticipantState target, Character? character) =>
        LewdProfile.Inhibition(target, character) - InhibitionPenalty(target, character, LastStimulatedBy(character));

    private static int InhibitionPenalty(ModeParticipantState target, Character? character, string? actorId) =>
        character is null
            ? GetInt(target, LewdKeys.LustbrandInhib) + GetInt(target, LewdKeys.ImprintInhib)
            : BrandState.TierSum(character) + ImprintState.InhibitionPenalty(character, actorId);

    private const string LastStimulatedByKey = LewdKeys.ModeTraitPrefix + "last_stim_by";

    public static void RecordStimulatedBy(Character? character, string? actorId)
    {
        if (character is not null && !string.IsNullOrWhiteSpace(actorId))
            character.SystemStats.Traits[LastStimulatedByKey] = actorId;
    }

    public static string? LastStimulatedBy(Character? character) => PregnancyState.Text(character, LastStimulatedByKey);

    public static bool IsAdvanceWanted(ModeParticipantState target, string actorId, Character? character = null) =>
        LewdProfile.Wants(target, character, actorId);

    public static int AdjustStimulationForTags(
        ModeParticipantState target,
        Character? character,
        int stimulation,
        string? stimulationType,
        IEnumerable<string>? tags)
    {
        if (stimulation <= 0)
            return stimulation;

        var probe = BuildProbe(stimulationType, tags);
        var soft = LewdProfile.SoftLimits(target, character);
        var kinks = LewdProfile.Kinks(target, character);
        var softHit = soft.Any(probe.Contains);
        var kinkHit = kinks.Any(probe.Contains);

        var amount = stimulation;
        if (softHit)
            amount = Math.Max(0, amount / 2);
        if (kinkHit)
            amount = (int)Math.Floor(amount * 1.5);

        return amount;
    }

    public static bool IsVerbalOrNonContact(string? kind, IEnumerable<string>? tags)
    {
        if (ContainsTag(tags, "physical", "contact", "touch", "penetration", "phallic"))
            return false;

        if (ContainsTag(tags, "verbal", "noncontact", "non-contact", "flirt", "dirty_talk", "words"))
            return true;

        var k = kind?.Trim().ToLowerInvariant();
        return k is "skilled" or "indirect" && !ContainsTag(tags, "spell_touch", "touch");
    }

    /// <summary>
    /// Handbook verbal/non-contact cap. Inexperienced histories: max 2 unless physical or ≥3 flirt beats.
    /// Returns the capped amount (never raises).
    /// </summary>
    public static int CapVerbalStimulation(
        ModeParticipantState target,
        Character? character,
        string? kind,
        IEnumerable<string>? tags,
        int stimulation,
        int flirtBeats)
    {
        if (stimulation <= 0 || !IsVerbalOrNonContact(kind, tags))
            return stimulation;

        if (flirtBeats >= 3 || ContainsTag(tags, "physical", "contact", "touch"))
            return stimulation;

        var history = SexualHistory(target, character);
        if (AllowsFullVerbalDice(history, target, character))
            return stimulation;

        return Math.Min(stimulation, 2);
    }

    public static bool BlocksVerbalClimax(
        ModeParticipantState target,
        Character? character,
        string? kind,
        IEnumerable<string>? tags)
    {
        if (!IsVerbalOrNonContact(kind, tags))
            return false;
        if (GetBool(target, LewdKeys.HadPhysical))
            return false;

        var history = SexualHistory(target, character);
        return !AllowsFullVerbalDice(history, target, character);
    }

    public static string? SexualHistory(ModeParticipantState target, Character? character)
    {
        var fromTraits = AnatomyTraits.GetSexualHistory(character);
        if (!string.IsNullOrWhiteSpace(fromTraits))
            return fromTraits.Trim().ToLowerInvariant();

        // Participant bag is already mode-scoped; accept either short or mode-prefixed keys.
        return (GetString(target, LewdKeys.LegacyTraitSexualHistory)
                ?? GetString(target, LewdKeys.TraitSexualHistory))
            ?.Trim().ToLowerInvariant();
    }

    private static bool AllowsFullVerbalDice(string? history, ModeParticipantState target, Character? character)
    {
        if (string.IsNullOrWhiteSpace(history))
            return false;

        if (history is "experienced_kinkster" or "promiscuous" or "erotic_professional")
            return true;

        if (history == "devoted_partner")
            return LewdProfile.AllowedPartners(target, character).Count > 0;

        return false;
    }

    private static bool ContainsTag(IEnumerable<string>? tags, params string[] needles)
    {
        if (tags is null)
            return false;
        foreach (var t in tags)
        {
            if (string.IsNullOrWhiteSpace(t))
                continue;
            foreach (var n in needles)
            {
                if (string.Equals(t.Trim(), n, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public static HashSet<string> BuildProbe(string? stimulationType, IEnumerable<string>? tags)
    {
        var probe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(stimulationType))
        {
            var type = stimulationType.Trim();
            probe.Add(type);
            foreach (var alias in StimTypeAliases(type))
                probe.Add(alias);
        }
        if (tags is not null)
        {
            foreach (var t in tags)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    probe.Add(t.Trim());
            }
        }

        return probe;
    }

    private static IEnumerable<string> StimTypeAliases(string type)
    {
        switch (type.ToLowerInvariant())
        {
            case "piercing":
                yield return "penetration";
                yield return "phallic";
                break;
            case "penetration":
            case "phallic":
                yield return "piercing";
                break;
            case "bludgeoning":
                yield return "impact";
                yield return "spanking";
                break;
            case "slashing":
                yield return "claws";
                yield return "edgeplay";
                break;
            case "thunder":
                yield return "vibration";
                yield return "verbal";
                break;
            case "poison":
                yield return "aphrodisiac";
                yield return "heat";
                break;
            case "psychic":
                yield return "verbal";
                yield return "mental";
                break;
            case "fire":
                yield return "wax";
                yield return "heat";
                break;
            case "cold":
                yield return "ice";
                break;
            case "lightning":
                yield return "shock";
                break;
        }
    }

    public static string? GetString(ModeParticipantState p, string key) =>
        p.State.TryGetValue(key, out var v) ? v?.ToString() : null;

    public static int GetInt(ModeParticipantState p, string key)
    {
        if (!p.State.TryGetValue(key, out var v) || v is null)
            return 0;
        return Convert.ToInt32(v);
    }

    public static bool GetBool(ModeParticipantState p, string key)
    {
        if (!p.State.TryGetValue(key, out var v) || v is null)
            return false;
        if (v is bool b)
            return b;
        return bool.TryParse(v.ToString(), out var parsed) && parsed;
    }

    public static List<string> GetStringList(ModeParticipantState p, string key)
    {
        if (!p.State.TryGetValue(key, out var v) || v is null)
            return [];

        if (v is IEnumerable<object> objs)
            return objs.Select(o => o?.ToString() ?? "").Where(s => s.Length > 0).ToList();
        if (v is IEnumerable<string> strs)
            return strs.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (v is string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return [];
            if (s.StartsWith('['))
            {
                return s.Trim('[', ']')
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.Trim().Trim('"'))
                    .Where(x => x.Length > 0)
                    .ToList();
            }

            return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return [];
    }
}
