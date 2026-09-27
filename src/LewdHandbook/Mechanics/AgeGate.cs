using System.Text.RegularExpressions;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// Hard gate: every character a lewd verb touches must be recorded as an adult (<see cref="LifeStage.Adult"/> or
/// <see cref="LifeStage.Elder"/>). Unspecified fails closed. A description that reads as a minor fails too, even
/// when the stage says adult — the stage is set by the same model that writes the description, so both must agree.
/// Not configurable by any campaign option.
/// </summary>
internal static partial class AgeGate
{
    [GeneratedRegex(
        @"\b(child|children|kid|kids|kiddo|underage|under-age|loli|lolicon|shota|shotacon|preteen|pre-teen|tween|teen|teens|teenage|teenager|schoolgirl|schoolboy|adolescent|infant|toddler|prepubescent|pubescent|juvenile|little girl|little boy|young girl|young boy)\b" +
        @"|\b([1-9]|1[0-7])[- ]?(years?|yrs?|y/o)[- ]?old\b|\baged? ([1-9]|1[0-7])\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinorDescriptor();

    public static bool TryPass(Character? character, string id, out string? error)
    {
        if (character is null)
        {
            error = $"'{id}' is not in the commit context, so their life stage cannot be checked. Lewd verbs never involve minors.";
            return false;
        }

        if (LifeStageRules.IsMinor(character.LifeStage))
        {
            error = $"'{id}' is recorded as {character.LifeStage}. Lewd verbs never involve minors.";
            return false;
        }

        if (!LifeStageRules.IsAdult(character.LifeStage))
        {
            error = $"'{id}' has no lifeStage. If they are an adult, set it first with character_update lifeStage=adult (or elder). " +
                    "Lewd verbs never involve minors.";
            return false;
        }

        if (DescribesMinor(character, out var term))
        {
            error = $"'{id}' is described as \"{term}\". Lewd verbs refuse characters whose description reads as a minor. " +
                    "If the description is wrong for an adult, correct it; otherwise leave them out.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Checks every non-blank id against <see cref="IChangeContext.Characters"/>.</summary>
    public static bool TryPassAll(IChangeContext context, out string? error, params string?[] ids)
    {
        foreach (var id in ids.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            context.Characters.TryGetValue(id!, out var character);
            if (!TryPass(character, id!, out error))
                return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Scans the name, appearance, visual tags and features. A few everyday uses are not read as a minor: someone's
    /// offspring ("her children", "mother of two kids"), the leather ("kid gloves") and the weak words kid / child in a
    /// name ("The Kid"). There is deliberately no switch to turn the scan off for a character.
    /// </summary>
    public static bool DescribesMinor(Character character, out string? term)
    {
        if (FirstMinorTerm(character.Name, isName: true) is { } fromName)
        {
            term = fromName;
            return true;
        }

        var fields = new List<string?> { character.CurrentAppearance };
        fields.AddRange(character.VisualTags);
        fields.AddRange(character.DistinctiveFeatures);
        foreach (var field in fields)
        {
            if (FirstMinorTerm(field, isName: false) is { } found)
            {
                term = found;
                return true;
            }
        }

        term = null;
        return false;
    }

    private static readonly HashSet<string> WeakNameWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "kid", "kids", "kiddo", "child", "children",
    };

    [GeneratedRegex(
        @"\b(her|his|their|my|your|our|its|two|three|four|five|six|seven|several|many|\d+|of|with|has|had|have|raising|raised)\s+(own\s+)?(young\s+)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OffspringLead();

    [GeneratedRegex(@"^[- ]?(gloves?|leather|skin|boots?|hide)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KidMaterial();

    internal static string? FirstMinorTerm(string? text, bool isName)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        foreach (Match match in MinorDescriptor().Matches(text))
        {
            var word = match.Value.Trim();
            var lower = word.ToLowerInvariant();
            if (isName && WeakNameWords.Contains(lower))
                continue;
            if (lower is "children" or "kids" or "child" or "kid" &&
                OffspringLead().IsMatch(text[..match.Index]))
                continue;
            if (lower is "kid" && KidMaterial().IsMatch(text[(match.Index + match.Length)..]))
                continue;
            return word;
        }

        return null;
    }
}
