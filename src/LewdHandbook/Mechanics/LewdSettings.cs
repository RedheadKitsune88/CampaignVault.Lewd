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

/// <summary>Whether an internal finish may enqueue impregnation after a climax deposit.</summary>
internal enum LewdCreampiePregnancy
{
    Off,
    Prompt,
    Auto,
}

/// <summary>
/// Player-owned campaign options (plugin.json <c>playerOnly</c>): narration style, the non-consent policy and
/// campaign-wide hard limits. The in-fiction willingness of a character is <see cref="LewdProfile"/>, not this.
/// </summary>
internal sealed record LewdSettings(
    LewdNarration Narration,
    LewdNonConsent NonConsent,
    IReadOnlyList<string> HardLimits,
    bool MoodBuffs = true,
    bool Humiliation = true,
    bool Fluids = false,
    bool ExternalMarks = true,
    bool FluidViceHook = true,
    LewdCreampiePregnancy CreampiePregnancy = LewdCreampiePregnancy.Off,
    bool InsertedToys = false,
    bool Leaks = true)
{
    public static readonly LewdSettings Default = new(LewdNarration.Suggestive, LewdNonConsent.Off, []);

    // Synchronous rules (a bad end tripped inside a stat helper) cannot await the options. Whatever a handler or observer already
    // resolved for this commit's context is remembered here; nothing resolved means "unknown", never "off".
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IChangeContext, LewdSettings> Resolved = new();

    /// <summary>The settings already resolved for this commit, or null when nothing has resolved them yet.</summary>
    public static LewdSettings? Peek(IChangeContext? context) =>
        context is not null && Resolved.TryGetValue(context, out var settings) ? settings : null;

    public static async Task<LewdSettings> ResolveAsync(IChangeContext context, CancellationToken ct = default)
    {
        var settings = await ResolveCoreAsync(context).ConfigureAwait(false);
        Resolved.AddOrUpdate(context, settings);
        return settings;
    }

    private static async Task<LewdSettings> ResolveCoreAsync(IChangeContext context)
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
            ParseList(Read(options, LewdKeys.HardLimitsOption)),
            Normalize(Read(options, LewdKeys.MoodBuffsOption)) != "off",
            Normalize(Read(options, LewdKeys.HumiliationOption)) != "off",
            Normalize(Read(options, LewdKeys.FluidsOption)) == LewdKeys.OptionOn,
            Normalize(Read(options, LewdKeys.ExternalMarksOption)) != LewdKeys.OptionOff,
            Normalize(Read(options, LewdKeys.FluidViceHookOption)) != LewdKeys.OptionOff,
            ParseCreampiePregnancy(Read(options, LewdKeys.CreampiePregnancyOption)),
            Normalize(Read(options, LewdKeys.InsertedToysOption)) == LewdKeys.OptionOn,
            Normalize(Read(options, LewdKeys.LeaksOption)) != LewdKeys.OptionOff);
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

    /// <summary>Unknown values fail closed to <see cref="LewdCreampiePregnancy.Off"/>.</summary>
    public static LewdCreampiePregnancy ParseCreampiePregnancy(string? raw) => Normalize(raw) switch
    {
        LewdKeys.CreampiePregnancyPrompt => LewdCreampiePregnancy.Prompt,
        LewdKeys.CreampiePregnancyAuto => LewdCreampiePregnancy.Auto,
        _ => LewdCreampiePregnancy.Off,
    };

    /// <summary>Hard-limit tags that refuse engine-owned sexual dirt.</summary>
    public static readonly string[] FluidHardLimitTags = ["fluids", "marking", "creampie", "cum", "sexual_fluids"];

    public static readonly string[] InsertHardLimitTags = ["plugs", "beads", "insertion", "toys", "anal"];

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

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IChangeContext, object> Narrated = new();

    /// <summary>The narration nudge, once per commit however many lewd verbs it holds.</summary>
    public void Narrate(IChangeContext context)
    {
        if (NarrationDirective is not { } directive || !Narrated.TryAdd(context, directive))
            return;
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
