using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

internal static class LewdModeAccess
{
    /// <summary>Active <c>lewd_encounter</c> only — ignores other modes on the stack.</summary>
    public static ModeEncounter? TryGetActive(IChangeContext context)
    {
        if (context.ActiveModes.TryGetValue(LewdEncounterMode.ModeIdValue, out var mode) && mode.IsActive)
            return mode;
        return null;
    }

    public static ModeParticipantState? TryGetParticipant(IChangeContext context, string characterId)
    {
        var mode = TryGetActive(context);
        return mode?.Participants.FirstOrDefault(p =>
            string.Equals(p.CharacterId, characterId, StringComparison.OrdinalIgnoreCase));
    }
}
