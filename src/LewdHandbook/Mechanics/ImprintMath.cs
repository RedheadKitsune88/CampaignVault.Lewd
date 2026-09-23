namespace LewdHandbook.Mechanics;

internal static class ImprintMath
{
    public static readonly string[] Tracks = ["wanton", "training", "breeding", "ordeal", "cruelty"];
    public static readonly string[] Sources = ["training", "wanton", "bad_end", "cruelty", "exposure"];

    public const int ResistBase = 12;
    public const int DeconditionBase = 10;
    public const int RestDc = 14;
    public const int Level1Points = 3;
    public const int Level2Points = 7;
    public const int Level3Points = 12;

    private static readonly HashSet<string> WantonTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "wanton", "public", "multi", "group", "orgy", "exhibition", "promiscuous",
    };

    private static readonly HashSet<string> TrainingTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "training", "petplay", "pet", "bitchsuit", "obedience", "collar", "trained",
    };

    private static readonly HashSet<string> BreedingTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "breeding", "repro", "seed", "seedbed", "impregnate", "impregnation", "creampie",
    };

    private static readonly HashSet<string> OrdealTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "ordeal", "forced", "denial", "masochism", "helpless", "trauma", "incapacitated",
    };

    private static readonly HashSet<string> CrueltyTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "cruelty", "sadism", "pain", "suffering", "torture", "ruin",
    };

    public static bool IsTrack(string? id) =>
        Tracks.Contains(Normalize(id));

    public static bool IsSource(string? id) =>
        Sources.Contains(Normalize(id));

    public static string Normalize(string? id) => id?.Trim().ToLowerInvariant() ?? "";

    public static int LevelFor(int points) =>
        points >= Level3Points ? 3 : points >= Level2Points ? 2 : points >= Level1Points ? 1 : 0;

    public static int PointsFor(int level) => level switch
    {
        >= 3 => Level3Points,
        2 => Level2Points,
        1 => Level1Points,
        _ => 0,
    };

    public static int ResistDc(int level, int dcMod) => ResistBase + Math.Max(0, level) + dcMod;

    public static int DeconditionDc(int level, bool unwilling, string method) =>
        method == "rest"
            ? RestDc
            : DeconditionBase + Math.Max(0, level) + (unwilling && method != "therapy" ? 2 : 0);

    public static int TherapyDrop(int margin) => margin >= 10 ? 3 : margin >= 5 ? 2 : 1;

    public static IReadOnlyList<string> TracksFor(IEnumerable<string>? tags, bool bitchsuit)
    {
        var found = new List<string>();
        if (bitchsuit || Hits(tags, TrainingTags))
            found.Add("training");
        if (Hits(tags, WantonTags))
            found.Add("wanton");
        if (Hits(tags, BreedingTags))
            found.Add("breeding");
        if (Hits(tags, OrdealTags))
            found.Add("ordeal");
        if (Hits(tags, CrueltyTags))
            found.Add("cruelty");
        return found;
    }

    public static bool IsCruelty(string? tag) =>
        tag is not null && CrueltyTags.Contains(tag);

    public static bool IsSuffering(IEnumerable<string>? tags, int targetOverstim) =>
        targetOverstim >= 1 || Hits(tags, CrueltyTags);

    private static bool Hits(IEnumerable<string>? tags, HashSet<string> set)
    {
        if (tags is null)
            return false;
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag) && set.Contains(tag.Trim()))
                return true;
        }

        return false;
    }
}
