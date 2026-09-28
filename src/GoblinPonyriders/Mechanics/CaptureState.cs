using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace GoblinPonyriders.Mechanics;

internal static class CaptureState
{
    public static bool IsCaptured(Character character) => Traits.Flag(character, GoblinKeys.Capture);

    public static void Begin(
        Character character,
        IChangeContext context,
        string? assignedName = null,
        int startingDefiance = 7)
    {
        Traits.SetFlag(character, GoblinKeys.Capture, true);
        Traits.SetFlag(character, GoblinKeys.TerrorReleased, false);
        DefianceClock.Set(character, startingDefiance, context, "capture");
        if (!string.IsNullOrWhiteSpace(assignedName))
            Traits.Set(character, GoblinKeys.AssignedName, assignedName.Trim());
        if (string.IsNullOrWhiteSpace(Traits.Get(character, GoblinKeys.TrainingPhase)))
            Traits.Set(character, GoblinKeys.TrainingPhase, TrainingPhases.RaidCamp);
        Traits.SetInt(character, GoblinKeys.TrainingDay, 1);

        StampCaptureStatus(character);
        context.RecordMessage(
            $"{character.Id} entered Capture State (Defiance {DefianceClock.Get(character)}/10). " +
            "Apply Lewd bindings (collar+hobble+wrists+gag) via lewd_bind; do not invent a second bondage engine.");
    }

    public static void End(Character character, IChangeContext context, bool terrorRelease, string? reason = null)
    {
        Traits.SetFlag(character, GoblinKeys.Capture, false);
        ClearCaptureStatus(character);
        if (terrorRelease)
        {
            Traits.SetFlag(character, GoblinKeys.TerrorReleased, true);
            context.RecordMessage(
                $"{character.Id} terror-released by Unified Clans{(reason is null ? "" : $" ({reason})")}: " +
                "stripped, minimally restrained or free, used as a walking warning. Seed a rumor.");
        }
        else
        {
            context.RecordMessage(
                $"{character.Id} left Capture State{(reason is null ? "" : $" ({reason})")}.");
        }
    }

    private static void StampCaptureStatus(Character character)
    {
        var effects = character.SystemStats.StatusEffects;
        effects.RemoveAll(e =>
            string.Equals(e.AppliedBy, GoblinKeys.AppliedBy, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.ConditionName, GoblinKeys.ConditionCapture, StringComparison.OrdinalIgnoreCase));
        effects.Add(new StatusEffect
        {
            Name = "Capture State",
            ConditionName = GoblinKeys.ConditionCapture,
            Category = "Condition",
            AppliedBy = GoblinKeys.AppliedBy,
            RecoveryHint = "Held by Unified Clans — collar/hobble expected; rescue or escape ends it.",
        });
    }

    private static void ClearCaptureStatus(Character character) =>
        character.SystemStats.StatusEffects.RemoveAll(e =>
            string.Equals(e.AppliedBy, GoblinKeys.AppliedBy, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.ConditionName, GoblinKeys.ConditionCapture, StringComparison.OrdinalIgnoreCase));
}
