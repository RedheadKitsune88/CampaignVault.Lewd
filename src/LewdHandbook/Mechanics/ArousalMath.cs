using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Handbook arousal maximum: like hit points, with Recovery Dice in place of hit dice. 1st level: the die's maximum +
/// Con modifier; each later level: the die's average (rounded up) + Con modifier. The die comes from the
/// <c>recovery_die</c> Trait, else from the sexual history.
/// </summary>
internal static class ArousalMath
{
    /// <summary>Used when a character has no recovery die or sexual history recorded (the pool template default).</summary>
    public const int FallbackMax = 10;

    private static readonly Dictionary<string, int> HistoryDice = new(StringComparer.OrdinalIgnoreCase)
    {
        ["virgin"] = 6,
        ["willingly_celibate"] = 6,
        ["strictly_vanilla"] = 6,
        ["modest_lover"] = 8,
        ["devoted_partner"] = 8,
        ["promiscuous"] = 8,
        ["experienced_kinkster"] = 10,
        ["erotic_professional"] = 12,
    };

    public static int? RecoveryDieSides(Character? character)
    {
        var explicitDie = LewdDice.ParseSides(AnatomyTraits.GetRecoveryDie(character));
        if (explicitDie is not null)
            return explicitDie;
        var history = AnatomyTraits.GetSexualHistory(character)?.Trim().Replace(' ', '_').Replace('-', '_');
        return history is not null && HistoryDice.TryGetValue(history, out var sides) ? sides : null;
    }

    public static int Level(Character? character) =>
        character?.SystemStats is Dnd5eExtension { Level: > 0 } dnd ? dnd.Level!.Value : 1;

    public static int ProficiencyBonus(Character? character) => 2 + (Level(character) - 1) / 4;

    /// <summary>Each level adds at least 1, as with hit points.</summary>
    public static int Max(int sides, int conMod, int level)
    {
        level = Math.Max(1, level);
        var first = Math.Max(1, sides + conMod);
        var later = Math.Max(1, LewdDice.Average(sides) + conMod);
        return first + (level - 1) * later;
    }

    /// <summary>The derived maximum, or null when the character has no recovery die to derive it from.</summary>
    public static int? DerivedMax(Character? character)
    {
        var sides = RecoveryDieSides(character);
        return sides is null ? null : Max(sides.Value, AbilityScores.Mod(character, "con"), Level(character));
    }
}
