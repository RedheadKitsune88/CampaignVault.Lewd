using CampaignVault.Data.ChangeHandlers;

namespace LewdHandbook.Mechanics;

internal enum IntimacyToneKind
{
    Consensual,
    Fade,
    Grimdark,
}

internal static class IntimacyTone
{
    public static async Task<IntimacyToneKind> ResolveAsync(IChangeContext context, CancellationToken ct = default)
    {
        try
        {
            var opts = await context.GetSystemOptionsAsync().ConfigureAwait(false);
            if (opts.TryGetValue(LewdKeys.IntimacyToneOption, out var raw) &&
                !string.IsNullOrWhiteSpace(raw))
            {
                return Parse(raw);
            }

            if (context.Config?.SystemOptions is { } cfg &&
                cfg.TryGetValue(LewdKeys.IntimacyToneOption, out var cfgRaw) &&
                !string.IsNullOrWhiteSpace(cfgRaw))
            {
                return Parse(cfgRaw);
            }
        }
        catch
        {
            // Degrade to default.
        }

        return IntimacyToneKind.Consensual;
    }

    public static IntimacyToneKind Parse(string? raw)
    {
        if (string.Equals(raw, LewdKeys.ToneFade, StringComparison.OrdinalIgnoreCase))
            return IntimacyToneKind.Fade;
        if (string.Equals(raw, LewdKeys.ToneGrimdark, StringComparison.OrdinalIgnoreCase))
            return IntimacyToneKind.Grimdark;
        return IntimacyToneKind.Consensual;
    }
}
