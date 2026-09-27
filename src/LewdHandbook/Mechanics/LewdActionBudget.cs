using CampaignVault.Models;

namespace LewdHandbook.Mechanics;

/// <summary>One action per participant, on their own turn. Refilled by <c>mode_transition action=turn</c>.</summary>
internal static class LewdActionBudget
{
    public const string Action = "action";

    public static bool TrySpend(ModeParticipantState state, out string? error)
    {
        error = null;
        if (!state.ActionBudget.TryGetValue(Action, out var left) || left <= 0)
        {
            error = $"{state.CharacterId} has no lewd action left: one action per participant, on their own turn.";
            return false;
        }

        state.ActionBudget[Action] = left - 1;
        return true;
    }
}
