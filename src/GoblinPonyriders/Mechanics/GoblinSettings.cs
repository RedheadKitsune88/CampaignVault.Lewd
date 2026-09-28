using CampaignVault.Data.ChangeHandlers;

namespace GoblinPonyriders.Mechanics;

internal sealed record GoblinSettings(bool Enabled, IReadOnlyList<string> HardLimits)
{
    public static readonly GoblinSettings Default = new(false, []);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IChangeContext, GoblinSettings> Resolved = new();

    public static GoblinSettings? Peek(IChangeContext? context) =>
        context is not null && Resolved.TryGetValue(context, out var settings) ? settings : null;

    public static async Task<GoblinSettings> ResolveAsync(IChangeContext context, CancellationToken ct = default)
    {
        Dictionary<string, string>? options = null;
        try
        {
            options = await context.GetSystemOptionsAsync().ConfigureAwait(false);
        }
        catch
        {
            // Fall through.
        }

        var settings = From(options is { Count: > 0 } ? options : context.Config?.SystemOptions);
        Resolved.AddOrUpdate(context, settings);
        return settings;
    }

    public static GoblinSettings From(IReadOnlyDictionary<string, string>? options)
    {
        if (options is null)
            return Default;

        var enabled = string.Equals(Read(options, GoblinKeys.ClansOption), GoblinKeys.OptionOn, StringComparison.OrdinalIgnoreCase);
        return new GoblinSettings(enabled, ParseList(Read(options, GoblinKeys.HardLimitsOption)));
    }

    public bool HitsHardLimit(IEnumerable<string> tags, out string? hit)
    {
        hit = null;
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                continue;
            var match = HardLimits.FirstOrDefault(h => string.Equals(h, tag, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                hit = match;
                return true;
            }
        }

        return false;
    }

    private static string? Read(IReadOnlyDictionary<string, string> options, string key) =>
        options.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static IReadOnlyList<string> ParseList(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .ToArray();
}
