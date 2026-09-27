using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class PregnancyState
{
    /// <summary>Reads a Trait, falling back to the key's pre-migration spelling for a document the upgrader hasn't reached.</summary>
    private static bool TryRead(Character? character, string key, out string? raw)
    {
        raw = null;
        var traits = character?.SystemStats.Traits;
        if (traits is null)
            return false;
        if (traits.TryGetValue(key, out raw))
            return true;
        var legacy = LewdKeys.LegacyTraitKey(key);
        return legacy is not null && traits.TryGetValue(legacy, out raw);
    }

    public static bool Flag(Character? character, string key) =>
        TryRead(character, key, out var raw) &&
        (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) || raw == "1");

    public static int Int(Character? character, string key) =>
        TryRead(character, key, out var raw) && int.TryParse(raw, out var n) ? n : 0;

    public static string? Text(Character? character, string key) =>
        TryRead(character, key, out var raw) ? raw : null;

    public static bool HasCondition(Character? character, string conditionName) =>
        character?.SystemStats.StatusEffects.Any(e =>
            string.Equals(e.ConditionName, conditionName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name, conditionName, StringComparison.OrdinalIgnoreCase)) == true;

    public static void Set(Character character, string key, string value)
    {
        character.SystemStats.Traits[key] = value;
        if (LewdKeys.LegacyTraitKey(key) is { } legacy)
            character.SystemStats.Traits.Remove(legacy);
    }

    /// <summary>Removes a Trait and, for a migrated key, its pre-migration copy, so a one-shot value cannot resurface.</summary>
    public static void Remove(Character character, string key)
    {
        character.SystemStats.Traits.Remove(key);
        if (LewdKeys.LegacyTraitKey(key) is { } legacy)
            character.SystemStats.Traits.Remove(legacy);
    }

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
        ClearClock(character);
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

    // Numeric clock (Attributes, campaign hours): conception, term length, last rest check, poison expiry.
    public const string StartHoursKey = "pregnancy.start_hours";
    public const string TermHoursKey = "pregnancy.term_hours";
    public const string RestCheckHoursKey = "pregnancy.rest_check_hours";
    public const string PoisonedUntilKey = "pregnancy.poisoned_until_hours";
    public const string AppliedBy = "lewd_pregnancy";

    public static float Attr(Character character, string key, float fallback = -1f) =>
        character.SystemStats.Attributes.TryGetValue(key, out var v) ? v : fallback;

    public static void SetAttr(Character character, string key, float value) =>
        character.SystemStats.Attributes[key] = value;

    public static bool IsNontraditional(Character character) =>
        string.Equals(Text(character, LewdKeys.PregnancyType), "nontraditional", StringComparison.OrdinalIgnoreCase);

    /// <summary>Side effects (the Pregnant condition, rest poison saves) start once the pregnancy shows.</summary>
    public static bool IsVisible(Character character) =>
        Flag(character, LewdKeys.Pregnant) &&
        (IsNontraditional(character) || Int(character, LewdKeys.PregnancyProgress) >= PregnancyMath.VisibleProgress);

    /// <summary>Starts the clock so that <paramref name="progress"/> is where the pregnancy stands right now.</summary>
    public static void StartClock(Character character, float nowHours, int progress, float termHours)
    {
        termHours = Math.Max(1f, termHours);
        SetAttr(character, TermHoursKey, termHours);
        SetAttr(character, StartHoursKey, nowHours - termHours * PregnancyMath.ClampProgress(progress) / PregnancyMath.ProgressMax);
    }

    public static void ClearClock(Character character)
    {
        character.SystemStats.Attributes.Remove(StartHoursKey);
        character.SystemStats.Attributes.Remove(TermHoursKey);
        character.SystemStats.Attributes.Remove(RestCheckHoursKey);
        character.SystemStats.Traits.Remove(LewdKeys.PregnancyDue);
        character.SystemStats.Traits.Remove(LewdKeys.PregnancyRestPoisonDay);
    }

    /// <summary>
    /// Moves progress with campaign time, stamps the Pregnant condition once it shows, flags the term as due at 100,
    /// and lifts the pregnancy poison once its hours are up. A pregnancy from before the clock existed starts it here,
    /// from its recorded progress. Returns a note when something changed.
    /// </summary>
    public static string? Sync(Character character, float nowHours, IChangeContext? context)
    {
        ExpirePoison(character, nowHours);
        if (!Flag(character, LewdKeys.Pregnant))
            return null;

        var before = Int(character, LewdKeys.PregnancyProgress);
        if (Attr(character, StartHoursKey) < 0 || Attr(character, TermHoursKey) <= 0)
            StartClock(character, nowHours, before, PregnancyMath.DefaultTermHours(IsNontraditional(character)));

        var start = Attr(character, StartHoursKey);
        var term = Attr(character, TermHoursKey);
        var progress = PregnancyMath.ClampProgress((int)Math.Floor((nowHours - start) / term * PregnancyMath.ProgressMax));
        progress = Math.Max(before, progress); // time never undoes progress a verb added
        Set(character, LewdKeys.PregnancyProgress, progress.ToString());

        if (IsVisible(character))
            StampPregnant(character, Text(character, LewdKeys.PregnancySource));

        if (progress >= PregnancyMath.ProgressMax && !Flag(character, LewdKeys.PregnancyDue))
        {
            Set(character, LewdKeys.PregnancyDue, "true");
            context?.RecordPhysicalStateNudge(
                $"{character.Id}'s pregnancy has reached term. Emit lewd_pregnancy action=birth when it happens in the fiction.");
            return $"{character.Id} pregnancy at term (100).";
        }

        return progress != before ? $"{character.Id} pregnancy progress {before}→{progress}." : null;
    }

    public static void StampPoisoned(Character character, float nowHours, int hours)
    {
        SetAttr(character, PoisonedUntilKey, nowHours + Math.Max(1, hours));
        if (HasCondition(character, "poisoned"))
            return;
        character.SystemStats.StatusEffects.Add(new StatusEffect
        {
            Name = "Poisoned",
            Category = "Poison",
            ConditionName = "poisoned",
            AppliedBy = AppliedBy,
            RecoveryHint = $"Pregnancy rest failure. Lasts {Math.Max(1, hours)} hour(s); the engine lifts it when that time has passed.",
        });
    }

    private static void ExpirePoison(Character character, float nowHours)
    {
        var until = Attr(character, PoisonedUntilKey);
        if (until < 0 || nowHours < until)
            return;
        character.SystemStats.Attributes.Remove(PoisonedUntilKey);
        character.SystemStats.StatusEffects.RemoveAll(e =>
            string.Equals(e.AppliedBy, AppliedBy, StringComparison.Ordinal) &&
            string.Equals(e.ConditionName, "poisoned", StringComparison.OrdinalIgnoreCase));
    }
}
