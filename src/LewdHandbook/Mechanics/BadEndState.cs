using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class BadEndState
{
    public const string EffectName = "Bad-Ended";

    public const string RecoveryHint =
        "Permanent sexual defeat. A follow-up commit chooses the consequence. This effect is not a level drain.";

    public static bool IsMarked(ModeParticipantState? participant, Character? character) =>
        (participant is not null && ConsentGate.GetBool(participant, LewdKeys.BadEnded)) ||
        PregnancyState.Flag(character, LewdKeys.BadEnded);

    public static void Apply(
        ModeParticipantState? participant,
        Character? character,
        string reason,
        string? sourceId = null,
        string? consequence = null,
        IChangeContext? context = null)
    {
        var already = IsMarked(participant, character);
        if (participant is not null)
        {
            participant.State[LewdKeys.BadEnded] = true;
            if (!already || !participant.State.ContainsKey(LewdKeys.BadEndReason))
                participant.State[LewdKeys.BadEndReason] = reason;
            if (consequence is not null)
                participant.State[LewdKeys.BadEndConsequence] = consequence;
        }

        if (character is not null)
        {
            PregnancyState.Set(character, LewdKeys.BadEnded, "true");
            if (!already || PregnancyState.Text(character, LewdKeys.BadEndReason) is null)
                PregnancyState.Set(character, LewdKeys.BadEndReason, reason);
            if (consequence is not null)
                PregnancyState.Set(character, LewdKeys.BadEndConsequence, consequence);
            Stamp(character, sourceId);
        }

        if (context is null)
            return;
        if (already && consequence is null)
            return;

        var who = character?.Id ?? participant?.CharacterId ?? "target";
        var stored = consequence is null ? "" : $" Consequence '{consequence}' stored, not applied.";
        context.RecordMessage(
            $"{who} bad_ended ({reason}).{stored} Follow-up take_turn chooses level_drain, imprint, slave, seedbed, curse, lustbrand, class_change, vice, or narrated. This commit did not drain a level.");
    }

    public static void StoreImprint(
        ModeParticipantState? participant,
        Character character,
        string track,
        int jump,
        string origin)
    {
        character.SystemStats.Traits[LewdKeys.BadEndImprintTrack] = track;
        character.SystemStats.Traits[LewdKeys.BadEndImprintJump] = jump.ToString();
        character.SystemStats.Traits[LewdKeys.BadEndImprintOrigin] = origin;
        if (participant is null)
            return;
        participant.State[LewdKeys.BadEndImprintTrack] = track;
        participant.State[LewdKeys.BadEndImprintJump] = jump;
        participant.State[LewdKeys.BadEndImprintOrigin] = origin;
    }

    public static void StoreVice(ModeParticipantState? participant, Character character, string viceId)
    {
        character.SystemStats.Traits[LewdKeys.BadEndViceId] = viceId;
        if (participant is not null)
            participant.State[LewdKeys.BadEndViceId] = viceId;
    }

    public static void Stamp(Character character, string? sourceId)
    {
        var effects = character.SystemStats.StatusEffects;
        if (effects.Any(IsBadEndEffect))
            return;
        effects.Add(new StatusEffect
        {
            Name = EffectName,
            Category = "Condition",
            ConditionName = LewdKeys.ConditionBadEnded,
            AppliedBy = string.IsNullOrWhiteSpace(sourceId) ? "lewd_bad_end" : sourceId,
            RecoveryHint = RecoveryHint,
        });
    }

    public static bool IsBadEndEffect(StatusEffect effect) =>
        string.Equals(effect.ConditionName, LewdKeys.ConditionBadEnded, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(effect.Name, EffectName, StringComparison.OrdinalIgnoreCase);
}
