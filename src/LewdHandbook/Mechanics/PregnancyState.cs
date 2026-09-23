using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class PregnancyState
{
    public static bool Flag(Character? character, string key) =>
        character?.SystemStats.Traits.TryGetValue(key, out var raw) == true &&
        (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) || raw == "1");

    public static int Int(Character? character, string key)
    {
        if (character?.SystemStats.Traits.TryGetValue(key, out var raw) == true &&
            int.TryParse(raw, out var n))
            return n;
        return 0;
    }

    public static string? Text(Character? character, string key)
    {
        if (character?.SystemStats.Traits.TryGetValue(key, out var raw) == true)
            return raw;
        return null;
    }

    public static bool HasCondition(Character? character, string conditionName) =>
        character?.SystemStats.StatusEffects.Any(e =>
            string.Equals(e.ConditionName, conditionName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, conditionName, StringComparison.OrdinalIgnoreCase)) == true;

    public static void Set(Character character, string key, string value) =>
        character.SystemStats.Traits[key] = value;

    public static void Mirror(ModeParticipantState? participant, Character character)
    {
        if (participant is null)
            return;
        participant.State[LewdKeys.Pregnant] = Flag(character, LewdKeys.Pregnant);
        participant.State[LewdKeys.PregnancyProgress] = Int(character, LewdKeys.PregnancyProgress);
        participant.State[LewdKeys.PregnancyType] = Text(character, LewdKeys.PregnancyType) ?? "";
        participant.State[LewdKeys.PregnancySource] = Text(character, LewdKeys.PregnancySource) ?? "";
        participant.State[LewdKeys.PregnancyOffspring] = Int(character, LewdKeys.PregnancyOffspring);
        if (Flag(character, LewdKeys.BadEnded))
            participant.State[LewdKeys.BadEnded] = true;
        participant.State[LewdKeys.BadEndReason] = Text(character, LewdKeys.BadEndReason) ?? "";
        participant.State[LewdKeys.BadEndConsequence] = Text(character, LewdKeys.BadEndConsequence) ?? "";
    }

    public static void StampPregnant(Character character, string? sourceId)
    {
        var effects = character.SystemStats.StatusEffects;
        if (effects.Any(e => string.Equals(e.ConditionName, LewdKeys.ConditionPregnant, StringComparison.OrdinalIgnoreCase)))
            return;
        effects.Add(new StatusEffect
        {
            Name = LewdKeys.ConditionPregnant,
            Category = "Condition",
            ConditionName = LewdKeys.ConditionPregnant,
            AppliedBy = sourceId,
            RecoveryHint = "Disadvantage on Strength and Dexterity saves. Attacks crit on 18–20. On a crit, Constitution save DC = damage or the pregnancy ends. Short or long rest: DC 15 Constitution or poisoned 1d4 hours. Remove on birth or termination.",
        });
    }

    public static void ClearPregnant(Character character)
    {
        Set(character, LewdKeys.Pregnant, "false");
        Set(character, LewdKeys.PregnancyProgress, "0");
        character.SystemStats.Traits.Remove(LewdKeys.PregnancyType);
        character.SystemStats.Traits.Remove(LewdKeys.PregnancySource);
        character.SystemStats.Traits.Remove(LewdKeys.PregnancyOffspring);
        var effects = character.SystemStats.StatusEffects;
        effects.RemoveAll(e =>
            string.Equals(e.ConditionName, LewdKeys.ConditionPregnant, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, LewdKeys.ConditionPregnant, StringComparison.OrdinalIgnoreCase));
    }

    public static void StampPoisoned(Character character)
    {
        if (HasCondition(character, "poisoned"))
            return;
        character.SystemStats.StatusEffects.Add(new StatusEffect
        {
            Name = "Poisoned",
            Category = "Poison",
            ConditionName = "poisoned",
            RecoveryHint = "Pregnancy rest failure. Lasts 1d4 hours; remove when that time has passed.",
        });
    }
}
