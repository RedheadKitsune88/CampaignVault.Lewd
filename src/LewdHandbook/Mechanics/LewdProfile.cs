using System.Globalization;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>
/// A character's in-fiction disposition: willingness (stance + allowed partners), personal limits, kinks and
/// inhibition. Scene overrides on the participant win; otherwise the character's <c>lewd_encounter.*</c> Traits;
/// otherwise willing with no limits. Lists merge (scene adds to the character's). The player's own content limits
/// are <see cref="LewdSettings"/>, not this.
/// </summary>
internal static class LewdProfile
{
    public static string Stance(ModeParticipantState? participant, Character? character)
    {
        var raw = Scalar(participant, LewdKeys.Consent, character, LewdKeys.TraitStance);
        return string.IsNullOrWhiteSpace(raw) ? LewdKeys.ConsentWilling : raw.Trim().ToLowerInvariant();
    }

    public static bool IsRevoked(ModeParticipantState? participant, Character? character) =>
        Stance(participant, character) == LewdKeys.ConsentRevoked;

    public static List<string> AllowedPartners(ModeParticipantState? participant, Character? character) =>
        List(participant, LewdKeys.AllowedPartners, character, LewdKeys.TraitAllowedPartners);

    public static List<string> HardLimits(ModeParticipantState? participant, Character? character) =>
        List(participant, LewdKeys.HardLimits, character, LewdKeys.TraitHardLimits);

    public static List<string> SoftLimits(ModeParticipantState? participant, Character? character) =>
        List(participant, LewdKeys.SoftLimits, character, LewdKeys.TraitSoftLimits);

    public static List<string> Kinks(ModeParticipantState? participant, Character? character) =>
        List(participant, LewdKeys.Kinks, character, LewdKeys.TraitKinks);

    /// <summary>
    /// The Inhibition Bonus. Handbook: a nymphomanic creature's is reduced to 0 unless already lower, and an infatuated
    /// one cannot benefit from a positive bonus, so both cap it at 0. This is the mechanical half only: it does not
    /// make anyone "willing" (see <see cref="Wants"/>), so those conditions never bypass <c>lewdNonConsent</c>.
    /// </summary>
    public static int Inhibition(ModeParticipantState? participant, Character? character)
    {
        int value;
        if (participant is not null && participant.State.ContainsKey(LewdKeys.Inhibition))
        {
            value = ConsentGate.GetInt(participant, LewdKeys.Inhibition);
        }
        else
        {
            var raw = PregnancyState.Text(character, LewdKeys.TraitInhibition);
            value = int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        }

        return value > 0 && (PregnancyState.HasCondition(character, LewdKeys.ConditionNymphomanic) ||
                             PregnancyState.HasCondition(character, LewdKeys.ConditionInfatuated))
            ? 0
            : value;
    }

    /// <summary>Whether <paramref name="actorId"/>'s advance is wanted by the character in the fiction.</summary>
    public static bool Wants(ModeParticipantState? target, Character? character, string? actorId)
    {
        var stance = Stance(target, character);
        if (stance == LewdKeys.ConsentWilling)
            return true;
        if (stance != LewdKeys.ConsentSelective)
            return false;
        var allowed = AllowedPartners(target, character);
        return allowed.Count == 0 ||
               allowed.Any(id => string.Equals(id, actorId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Scalar(ModeParticipantState? participant, string stateKey, Character? character, string traitKey)
    {
        var scene = participant is null ? null : ConsentGate.GetString(participant, stateKey);
        return !string.IsNullOrWhiteSpace(scene) ? scene : PregnancyState.Text(character, traitKey);
    }

    private static List<string> List(ModeParticipantState? participant, string stateKey, Character? character, string traitKey)
    {
        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trait = PregnancyState.Text(character, traitKey);
        var sources = new IEnumerable<string>[]
        {
            string.IsNullOrWhiteSpace(trait)
                ? []
                : trait.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            participant is null ? [] : ConsentGate.GetStringList(participant, stateKey),
        };
        foreach (var item in sources.SelectMany(s => s))
        {
            if (!string.IsNullOrWhiteSpace(item) && seen.Add(item.Trim()))
                merged.Add(item.Trim());
        }

        return merged;
    }
}
