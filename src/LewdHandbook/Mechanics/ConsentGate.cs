using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal enum ConsentAuthorizeResult
{
    Allow,
    Fail,
    FadeNoStim,
}

internal static class ConsentGate
{
    /// <summary>
    /// Hard limits + revoked always fail-closed. Unwilling handling depends on intimacyTone.
    /// </summary>
    public static ConsentAuthorizeResult AuthorizeAdvance(
        ModeParticipantState target,
        string actorId,
        string? stimulationType,
        IEnumerable<string>? tags,
        IntimacyToneKind tone,
        out string? error)
    {
        error = null;
        var consent = GetString(target, LewdKeys.Consent) ?? LewdKeys.ConsentWilling;

        if (string.Equals(consent, LewdKeys.ConsentRevoked, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Target '{target.CharacterId}' has revoked consent; advance refused.";
            return ConsentAuthorizeResult.Fail;
        }

        var hard = GetStringList(target, LewdKeys.HardLimits);
        if (hard.Count > 0)
        {
            var probe = BuildProbe(stimulationType, tags);
            var hit = hard.FirstOrDefault(h => probe.Contains(h));
            if (hit is not null)
            {
                error = $"Target '{target.CharacterId}' hard limit '{hit}' blocks this advance.";
                return ConsentAuthorizeResult.Fail;
            }
        }

        if (string.Equals(consent, LewdKeys.ConsentSelective, StringComparison.OrdinalIgnoreCase))
        {
            var allowed = GetStringList(target, LewdKeys.AllowedPartners);
            if (allowed.Count > 0 &&
                !allowed.Any(id => string.Equals(id, actorId, StringComparison.OrdinalIgnoreCase)))
            {
                // Selective miss → treat as unwilling for tone purposes
                return AuthorizeUnwilling(target, tone, out error);
            }
        }

        if (string.Equals(consent, LewdKeys.ConsentUnwilling, StringComparison.OrdinalIgnoreCase))
            return AuthorizeUnwilling(target, tone, out error);

        return ConsentAuthorizeResult.Allow;
    }

    private static ConsentAuthorizeResult AuthorizeUnwilling(
        ModeParticipantState target,
        IntimacyToneKind tone,
        out string? error)
    {
        error = null;
        return tone switch
        {
            IntimacyToneKind.Grimdark => ConsentAuthorizeResult.Allow,
            IntimacyToneKind.Fade => ConsentAuthorizeResult.FadeNoStim,
            _ => FailConsensual(target, out error),
        };
    }

    private static ConsentAuthorizeResult FailConsensual(ModeParticipantState target, out string? error)
    {
        error = $"Target '{target.CharacterId}' is unwilling; intimacyTone=consensual refuses the advance.";
        return ConsentAuthorizeResult.Fail;
    }

    /// <summary>Legacy bool API used by older call sites/tests.</summary>
    public static bool TryAuthorizeAdvance(
        ModeParticipantState target,
        string actorId,
        string? stimulationType,
        IEnumerable<string>? tags,
        out string? error) =>
        AuthorizeAdvance(target, actorId, stimulationType, tags, IntimacyToneKind.Consensual, out error) ==
        ConsentAuthorizeResult.Allow;

    /// <summary>Willing partners treat Inhibition as 0 unless already negative (handbook).</summary>
    public static int EffectiveInhibition(ModeParticipantState target, bool advanceIsWanted)
    {
        var raw = GetInt(target, LewdKeys.Inhibition);
        if (advanceIsWanted)
            return Math.Min(0, raw);
        return raw;
    }

    public static bool IsAdvanceWanted(ModeParticipantState target, string actorId)
    {
        var consent = GetString(target, LewdKeys.Consent) ?? LewdKeys.ConsentWilling;
        if (string.Equals(consent, LewdKeys.ConsentWilling, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(consent, LewdKeys.ConsentSelective, StringComparison.OrdinalIgnoreCase))
        {
            var allowed = GetStringList(target, LewdKeys.AllowedPartners);
            return allowed.Count == 0 ||
                   allowed.Any(id => string.Equals(id, actorId, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    public static int AdjustStimulationForTags(
        ModeParticipantState target,
        int stimulation,
        string? stimulationType,
        IEnumerable<string>? tags)
    {
        if (stimulation <= 0)
            return stimulation;

        var probe = BuildProbe(stimulationType, tags);
        var soft = GetStringList(target, LewdKeys.SoftLimits);
        var kinks = GetStringList(target, LewdKeys.Kinks);
        var softHit = soft.Any(probe.Contains);
        var kinkHit = kinks.Any(probe.Contains);

        var amount = stimulation;
        if (softHit)
            amount = Math.Max(0, amount / 2);
        if (kinkHit)
            amount = (int)Math.Floor(amount * 1.5);

        return amount;
    }

    private static HashSet<string> BuildProbe(string? stimulationType, IEnumerable<string>? tags)
    {
        var probe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(stimulationType))
            probe.Add(stimulationType.Trim());
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
