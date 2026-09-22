using System.Globalization;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal sealed record AnatomyImplement(
    string Key,
    string DieExpression,
    IReadOnlyList<string> Tags,
    string? Size,
    bool IsFinesse);

/// <summary>
/// Natural implements live on <see cref="SystemExtension.Traits"/> (string bag), not float Attributes.
/// Example: Traits["anatomy.cock"] = "die=1d8;tags=phallic,natural;size=medium;finesse=false"
/// </summary>
internal static class AnatomyTraits
{
    public static IReadOnlyList<AnatomyImplement> ListAnatomy(Character? character)
    {
        if (character?.SystemStats.Traits is not { Count: > 0 } traits)
            return [];

        var list = new List<AnatomyImplement>();
        foreach (var (key, value) in traits)
        {
            if (!key.StartsWith(LewdKeys.AnatomyPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(value))
                continue;
            list.Add(Parse(key, value));
        }

        return list;
    }

    public static AnatomyImplement? Find(Character? character, string? anatomyKeyOrSuffix)
    {
        if (character is null || string.IsNullOrWhiteSpace(anatomyKeyOrSuffix))
            return null;

        var want = anatomyKeyOrSuffix.Trim();
        if (!want.StartsWith(LewdKeys.AnatomyPrefix, StringComparison.OrdinalIgnoreCase))
            want = LewdKeys.AnatomyPrefix + want;

        if (character.SystemStats.Traits.TryGetValue(want, out var raw) && !string.IsNullOrWhiteSpace(raw))
            return Parse(want, raw);

        // Case-insensitive key scan
        foreach (var (key, value) in character.SystemStats.Traits)
        {
            if (string.Equals(key, want, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
                return Parse(key, value);
        }

        return null;
    }

    public static string? GetSexualHistory(Character? character) =>
        GetTrait(character, LewdKeys.TraitSexualHistory);

    public static string? GetRecoveryDie(Character? character) =>
        GetTrait(character, LewdKeys.TraitRecoveryDie);

    public static string? GetTrait(Character? character, string key)
    {
        if (character?.SystemStats.Traits is not { Count: > 0 } traits)
            return null;
        if (traits.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            return v.Trim();
        foreach (var (k, val) in traits)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(val))
                return val.Trim();
        }

        return null;
    }

    public static AnatomyImplement Parse(string key, string value)
    {
        var die = "1d4";
        var tags = new List<string> { "natural" };
        string? size = null;
        var finesse = false;

        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                // bare token → tag
                tags.Add(part);
                continue;
            }

            var name = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();
            if (name.Equals("die", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("dice", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("damage", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("damageDice", StringComparison.OrdinalIgnoreCase))
            {
                die = val;
            }
            else if (name.Equals("tags", StringComparison.OrdinalIgnoreCase))
            {
                tags = val.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
            else if (name.Equals("size", StringComparison.OrdinalIgnoreCase))
            {
                size = val;
            }
            else if (name.Equals("finesse", StringComparison.OrdinalIgnoreCase))
            {
                finesse = val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                          val.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                          val.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
        }

        return new AnatomyImplement(key, die, tags, size, finesse);
    }

    /// <summary>Parse leading NdM from expressions like 1d8+2 or 2d6.</summary>
    public static bool TryParseDice(string expression, out int count, out int faces)
    {
        count = 1;
        faces = 4;
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var expr = expression.Trim().ToLowerInvariant();
        var plus = expr.IndexOf('+');
        var minus = expr.IndexOf('-', 1);
        var cut = expr.Length;
        if (plus > 0) cut = Math.Min(cut, plus);
        if (minus > 0) cut = Math.Min(cut, minus);
        var core = expr[..cut];
        var d = core.IndexOf('d');
        if (d < 0)
            return int.TryParse(core, NumberStyles.Integer, CultureInfo.InvariantCulture, out faces);

        var left = d == 0 ? "1" : core[..d];
        var right = core[(d + 1)..];
        if (!int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
            count = 1;
        if (!int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out faces))
            return false;
        return count > 0 && faces > 0;
    }
}
