using CampaignVault.Data.ChangeHandlers;

namespace LewdHandbook.Mechanics;

/// <summary>The campaign clock as fractional days (<c>TotalDaysElapsed + Hour / 24</c>), the unit <c>StatusEffect.ExpiresAtDay</c> and the host's action block compare against.</summary>
internal static class LewdClock
{
    public static async Task<double?> NowDaysAsync(IChangeContext context)
    {
        try
        {
            var time = await context.GetCurrentTimeAsync().ConfigureAwait(false);
            return time is null ? null : time.TotalDaysElapsed + time.Hour / 24.0;
        }
        catch (Exception ex) when (ex is NullReferenceException or InvalidOperationException)
        {
            return null;
        }
    }
}
