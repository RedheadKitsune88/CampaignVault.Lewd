using System.Globalization;
using System.Text;
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
    private const string Words =
        "one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen";

    private const string Num = @"(?:0?[1-9]|1[0-7])";

    /// <summary>Words that name a minor outright. Runs on the folded text, so "l0li" and "t.e.e.n" are caught too.</summary>
    [GeneratedRegex(
        @"\b(child|children|kid|kids|kiddo|underage|underaged|under-age|loli|lolita|lolicon|shota|shotacon|preteen|pre-teen|tween|teen|teens|teenage|teenaged|teenager|schoolgirl|schoolboy|highschooler|middleschooler|gradeschooler|adolescent|infant|toddler|prepubescent|pubescent|juvenile|jailbait|youngling|little girl|little boy|young girl|young boy|small girl|small boy)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinorWord();

    private const string Unit = @"(?!\s*(feet|foot|ft|meters?|inches|miles?|days?|weeks?|months?|gold|gp|sp|cp|coins?)\b)";

    /// <summary>A stated age of 17 or younger: "16yo", "16 y/o", "15-year-old", "aged twelve", "turned 17", "sweet sixteen", "under 18".</summary>
    [GeneratedRegex(
        @"\b(" + Num + "|" + Words + @")\s*-?\s*(yo|y\.?o\.?|y/o)\b"
        + @"|\b(" + Num + "|" + Words + @")\s*-?\s*(yrs?\.?|years?)\s*-?\s*(old|of age|young)\b"
        + @"|\b(aged?|ages|age of|age:)\s*:?\s*(" + Num + "|" + Words + @")\b" + Unit
        + @"|\bturn(ed|ing)\s+(" + Num + "|" + Words + @")\b(?=\s*([.,;!?)]|$|years?|yo\b|last|this|in|on|today|recently))"
        + @"|\bsweet\s+(" + Words + @")\b"
        + @"|\b(" + Num + "|" + Words + @")(st|nd|rd|th)?\s+birthday\b"
        + @"|\b(under|below|younger than|not yet|less than)\s+(the\s+age\s+of\s+)?(18|eighteen)\b|\bage\s+of\s+consent\b"
        + @"|\b(looks?|appears?|seems?)\s+(to be\s+|like\s+)?(about\s+|around\s+|maybe\s+)?" + Num + @"\b" + Unit
        + @"(?=\s*(years?|yo\b|or so|[.,;!?)]|$))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinorAge();

    /// <summary>Backstory markers: "when she was a child", "as a teen", "since childhood". Only honoured in Notes.</summary>
    [GeneratedRegex(
        @"\b(when|as|since|until|before|after|once|during|while|long ago)\b[^.,;!?]{0,25}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PastLead();

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

        // Notes is free-form backstory, so "orphaned as a child" is allowed there; a stated present age is not.
        if (FirstMinorTerm(character.Notes, isName: false, backstory: true) is { } fromNotes)
        {
            term = fromNotes;
            return true;
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

    /// <summary>Drops zero-width and soft-hyphen characters, unifies dashes and spaces, folds compatibility forms.</summary>
    private static string Normalize(string text)
    {
        var folded = text.Normalize(NormalizationForm.FormKC);
        var sb = new StringBuilder(folded.Length);
        foreach (var ch in folded)
        {
            if (ch is '\u200B' or '\u200C' or '\u200D' or '\u2060' or '\uFEFF' or '\u00AD')
                continue;
            sb.Append(char.GetUnicodeCategory(ch) == UnicodeCategory.DashPunctuation ? '-' : char.IsWhiteSpace(ch) ? ' ' : ch);
        }

        return sb.ToString();
    }

    /// <summary>Undoes common obfuscation for the word scan only ("l0li", "t33n", "k1d", "t.e.e.n", "s c h o o l g i r l").</summary>
    private static string Deobfuscate(string text)
    {
        var lower = text.ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        for (var i = 0; i < lower.Length; i++)
        {
            var ch = lower[i];
            // A digit or symbol only stands in for a letter when it sits against another letter ("l0li", "t33n"); a lone
            // "3" in "with 3 kids" stays a number so the offspring check still reads it.
            var touchesLetter = (i > 0 && char.IsLetter(lower[i - 1])) || (i + 1 < lower.Length && char.IsLetter(lower[i + 1]));
            sb.Append(touchesLetter
                ? ch switch { '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's', '7' => 't', '@' => 'a', '$' => 's', '!' => 'i', _ => ch }
                : ch);
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"(?<=\b\w)[.\-_*](?=\w\b|\w[.\-_*])", RegexOptions.CultureInvariant)]
    private static partial Regex LetterSeparators();

    internal static string? FirstMinorTerm(string? text, bool isName, bool backstory = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = Normalize(text);

        // Stated ages never apply to a name ("Sixteen Sisters Inn" is not a person), and in backstory only when present-tense.
        if (!isName)
        {
            foreach (Match match in MinorAge().Matches(text))
            {
                if (backstory && PastLead().IsMatch(text[..match.Index]))
                    continue;
                return match.Value.Trim();
            }
        }

        // Digits are letters for the word scan, but a digit-only span ("16yo") is already handled above.
        var scan = Deobfuscate(text);
        scan = LetterSeparators().Replace(scan, "");
        foreach (Match match in MinorWord().Matches(scan))
        {
            var word = match.Value.Trim();
            var lower = word.ToLowerInvariant();
            if (isName && WeakNameWords.Contains(lower))
                continue;
            if (lower is "children" or "kids" or "child" or "kid" &&
                OffspringLead().IsMatch(scan[..match.Index]))
                continue;
            if (lower is "kid" && KidMaterial().IsMatch(scan[(match.Index + match.Length)..]))
                continue;
            if (backstory && lower is "child" or "children" or "kid" or "kids" or "teen" or "teens" or "teenager" or "adolescent" or "infant" or "toddler" or "juvenile" &&
                PastLead().IsMatch(scan[..match.Index]))
                continue;
            return word;
        }

        return null;
    }
}
