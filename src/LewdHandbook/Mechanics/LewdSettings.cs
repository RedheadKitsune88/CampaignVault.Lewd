using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>How sexual content is narrated. Mechanics always run in full; this only steers the narrator.</summary>
internal enum LewdNarration
{
    Explicit,
    Suggestive,
    Fade,
}

/// <summary>Whether the fiction may contain coercion at all, and whether the player's character can be its target.</summary>
internal enum LewdNonConsent
{
    Off,
    NotAgainstPc,
    On,
}

/// <summary>
/// Player-owned campaign options (plugin.json <c>playerOnly</c>): narration style, the non-consent policy and
/// campaign-wide hard limits. The in-fiction willingness of a character is <see cref="LewdProfile"/>, not this.
/// </summary>
internal sealed record LewdSettings(LewdNarration Narration, LewdNonConsent NonConsent, IReadOnlyList<string> HardLimits)
{
    public static readonly LewdSettings Default = new(LewdNarration.Suggestive, LewdNonConsent.Off, []);

    public static async Task<LewdSettings> ResolveAsync(IChangeContext context, CancellationToken ct = default)
    {
        Dictionary<string, string>? options = null;
        try
        {
            options = await context.GetSystemOptionsAsync().ConfigureAwait(false);
        }
        catch
        {
            // Fall through to Config, then defaults — never looser than the defaults.
        }

        return From(options is { Count: > 0 } ? options : context.Config?.SystemOptions);
    }

    public static LewdSettings From(IReadOnlyDictionary<string, string>? options)
    {
        if (options is null)
            return Default;

        return new LewdSettings(
            ParseNarration(Read(options, LewdKeys.NarrationOption)),
            ParseNonConsent(Read(options, LewdKeys.NonConsentOption)),
            ParseList(Read(options, LewdKeys.HardLimitsOption)));
    }

    public static LewdNarration ParseNarration(string? raw) => Normalize(raw) switch
    {
        LewdKeys.NarrationExplicit => LewdNarration.Explicit,
        LewdKeys.NarrationFade => LewdNarration.Fade,
        _ => LewdNarration.Suggestive,
    };

    /// <summary>Unknown values fail closed to <see cref="LewdNonConsent.Off"/>.</summary>
    public static LewdNonConsent ParseNonConsent(string? raw) => Normalize(raw) switch
    {
        LewdKeys.NonConsentOn => LewdNonConsent.On,
        LewdKeys.NonConsentNotAgainstPc => LewdNonConsent.NotAgainstPc,
        _ => LewdNonConsent.Off,
    };

    /// <summary>
    /// Whether an unwanted act against <paramref name="target"/> may resolve. Callers only ask when the act is
    /// unwanted in the fiction; wanted acts never need this.
    /// </summary>
    public bool AllowsUnwanted(Character? target, string targetId, out string? error)
    {
        error = NonConsent switch
        {
            LewdNonConsent.On => null,
            LewdNonConsent.NotAgainstPc when target is { IsPc: false } => null,
            LewdNonConsent.NotAgainstPc =>
                $"'{targetId}' is the player's character and lewdNonConsent=not_against_pc: nothing unwanted resolves against them.",
            _ => $"'{targetId}' is unwilling and lewdNonConsent=off: unwanted acts do not resolve. Keep the scene consensual or move on.",
        };
        return error is null;
    }

    public bool HitsHardLimit(IEnumerable<string> probe, out string? hit)
    {
        hit = HardLimits.FirstOrDefault(limit => probe.Contains(limit, StringComparer.OrdinalIgnoreCase));
        return hit is not null;
    }

    /// <summary>One-line narration directive; null when narration is explicit.</summary>
    public string? NarrationDirective => Narration switch
    {
        LewdNarration.Fade => "lewdNarration=fade: fade to black. Mechanics resolved; narrate before and after, never the act.",
        LewdNarration.Suggestive => "lewdNarration=suggestive: imply, don't describe explicitly. Mechanics resolved in full.",
        _ => null,
    };

    public void Narrate(IChangeContext context)
    {
        if (NarrationDirective is { } directive)
            context.RecordPhysicalStateNudge(directive);
    }

    private static string? Read(IReadOnlyDictionary<string, string> options, string key)
    {
        if (options.TryGetValue(key, out var direct))
            return direct;
        foreach (var (k, v) in options)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                return v;
        }

        return null;
    }

    private static string Normalize(string? raw) => (raw ?? "").Trim().ToLowerInvariant().Replace('-', '_');

    private static IReadOnlyList<string> ParseList(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
