using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// "Within 5 ft" from what the engine knows: a physical engagement (grapple, embrace), a chain between them, or a Touch
/// spatial position is within; Near / Far / Distant is not. Anything else (Close, or nothing recorded) is unknown.
/// </summary>
internal static class Proximity
{
    public static bool? WithinFiveFeet(Character a, Character b)
    {
        if (Engaged(a, b.Id) || Engaged(b, a.Id) || BindingGraph.AreLinked(a, b))
            return true;

        var band = Band(a, b.Id) ?? Band(b, a.Id);
        if (band is null)
            return null;
        if (string.Equals(band, SpatialDistanceBand.Touch, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(band, SpatialDistanceBand.Close, StringComparison.OrdinalIgnoreCase))
            return null;
        return false;
    }

    private static bool Engaged(Character c, string otherId) =>
        c.SystemStats.EngagementRelations.Any(r =>
            r.Category == EngagementCategory.Physical &&
            string.Equals(r.TargetId, otherId, StringComparison.OrdinalIgnoreCase));

    private static string? Band(Character c, string otherId) =>
        c.SystemStats?.SpatialPositions
            .FirstOrDefault(p => string.Equals(p.TargetId, otherId, StringComparison.OrdinalIgnoreCase))
            ?.DistanceBand;
}
