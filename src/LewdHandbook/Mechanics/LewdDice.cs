using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Non-d20 dice (recovery dice, 1d4 hours, 1d12 psychic). Faces the model supplied win; else the host rolls; with
/// neither, each die counts its average rounded up (the 5e fixed-HP rule), and the summary says so.
/// </summary>
internal static class LewdDice
{
    public readonly record struct Result(int Total, IReadOnlyList<int> Faces, string Summary);

    public static async Task<Result> RollAsync(
        IChangeContext context,
        string tag,
        int count,
        int sides,
        IReadOnlyList<int>? supplied = null,
        CancellationToken ct = default)
    {
        if (count <= 0 || sides <= 0)
            return new Result(0, [], "no dice");

        List<int> faces;
        string how;
        if (supplied is { Count: > 0 })
        {
            faces = supplied.Take(count).Select(f => Math.Clamp(f, 1, sides)).ToList();
            while (faces.Count < count)
                faces.Add(Average(sides));
            how = supplied.Count >= count ? "given" : "given + average";
        }
        else if (context.Rolls is not null)
        {
            var roll = await context.Rolls.RollAsync(
                new RollRequest { Tag = tag, Expression = $"{count}d{sides}" }, ct).ConfigureAwait(false);
            faces = roll.IndividualDice.Take(count).Select(f => Math.Clamp(f, 1, sides)).ToList();
            if (faces.Count < count)
                faces = [.. faces, .. Enumerable.Repeat(Average(sides), count - faces.Count)];
            how = "rolled";
        }
        else
        {
            faces = Enumerable.Repeat(Average(sides), count).ToList();
            how = "average; no Rolls";
        }

        return new Result(faces.Sum(), faces, $"{count}d{sides} [{string.Join(",", faces)}] ({how})");
    }

    /// <summary>Average of one die, rounded up (d8 → 5).</summary>
    public static int Average(int sides) => sides / 2 + 1;

    /// <summary>Parses "d8", "1d8" or "8" to the number of sides; null when unreadable.</summary>
    public static int? ParseSides(string? die)
    {
        var s = (die ?? "").Trim().ToLowerInvariant();
        var d = s.IndexOf('d');
        if (d >= 0)
            s = s[(d + 1)..];
        return int.TryParse(s, out var sides) && sides is >= 2 and <= 100 ? sides : null;
    }
}
