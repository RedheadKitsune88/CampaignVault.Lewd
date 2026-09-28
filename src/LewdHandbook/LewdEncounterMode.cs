using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using CampaignVault.Rulesets.Modes;

namespace LewdHandbook;

/// <summary>
/// Opt-in sexual-encounter interaction mode for ActiveSystem=dnd5e. The state machine only rotates turns;
/// turn-start rules run in <c>lewd_turn_start</c> (via core.mode_turn_started.v1) where character sheets are visible.
/// </summary>
public sealed class LewdEncounterMode : IInteractionMode
{
    public const string ModeIdValue = "lewd_encounter";

    public string ModeId => ModeIdValue;
    public string DisplayName => "Lewd Encounter";
    public IReadOnlyList<string> CompatibleSystems => ["dnd5e"];
    public IModeStateMachine StateMachine { get; } = new LewdEncounterStateMachine();

    /// <summary>Every participant must be a loaded, recorded adult — see <see cref="Mechanics.AgeGate"/>.</summary>
    public string? ValidateEntry(
        IReadOnlyList<string> participantIds,
        IReadOnlyDictionary<string, Character> loaded,
        IChangeContext context)
    {
        foreach (var id in participantIds.Where(i => !string.IsNullOrWhiteSpace(i)))
        {
            loaded.TryGetValue(id, out var character);
            if (!Mechanics.AgeGate.TryPass(character, id, out var error))
                return error;
        }

        return null;
    }
}

file sealed class LewdEncounterStateMachine : IModeStateMachine
{
    public ModeEncounter CreateEncounter(string locationId, IReadOnlyList<string> participantIds) =>
        new()
        {
            LocationId = locationId,
            ModeId = LewdEncounterMode.ModeIdValue,
            IsActive = true,
            Round = 1,
            // Only whoever's turn it is can act; mode_transition action=turn hands the action on.
            Participants = participantIds.Select((id, i) => NewParticipant(id, i == 0)).ToList(),
            ActiveTurnId = participantIds.FirstOrDefault()
        };

    /// <summary>A joiner acts from the next time the turn reaches them, so they start with no action.</summary>
    public bool TryAddParticipant(ModeEncounter encounter, string participantId, out string? errorReason)
    {
        errorReason = null;
        if (encounter.Participants.Any(p => string.Equals(p.CharacterId, participantId, StringComparison.OrdinalIgnoreCase)))
        {
            errorReason = $"'{participantId}' is already in this encounter.";
            return false;
        }

        encounter.Participants.Add(NewParticipant(participantId, hasTurn: false));
        encounter.ActiveTurnId ??= participantId;
        return true;
    }

    /// <summary>Whoever inherits the leaver's turn gets the action that was the leaver's.</summary>
    public bool TryRemoveParticipant(ModeEncounter encounter, string participantId, out string? errorReason)
    {
        errorReason = null;
        var index = encounter.Participants.FindIndex(p =>
            string.Equals(p.CharacterId, participantId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            errorReason = $"'{participantId}' is not in this encounter.";
            return false;
        }

        var wasActive = string.Equals(encounter.ActiveTurnId, encounter.Participants[index].CharacterId, StringComparison.OrdinalIgnoreCase);
        encounter.Participants.RemoveAt(index);
        if (encounter.Participants.Count == 0)
        {
            encounter.ActiveTurnId = null;
        }
        else if (wasActive)
        {
            var next = encounter.Participants[index % encounter.Participants.Count];
            encounter.ActiveTurnId = next.CharacterId;
            next.ActionBudget[Mechanics.LewdActionBudget.Action] = 1;
        }

        return true;
    }

    private static ModeParticipantState NewParticipant(string id, bool hasTurn) =>
        new()
        {
            CharacterId = id,
            ActionBudget = new Dictionary<string, int> { [Mechanics.LewdActionBudget.Action] = hasTurn ? 1 : 0 },
            State = new Dictionary<string, object>
            {
                [Mechanics.LewdKeys.ClimaxSuccesses] = 0,
                [Mechanics.LewdKeys.ClimaxFailures] = 0,
                [Mechanics.LewdKeys.Edging] = false,
                [Mechanics.LewdKeys.Overstimulation] = 0,
                [Mechanics.LewdKeys.ClimaxStreak] = 0,
                [Mechanics.LewdKeys.ClimaxIncapacitated] = false,
                [Mechanics.LewdKeys.EdgingBeats] = 0,
                [Mechanics.LewdKeys.HadPhysical] = false,
                [Mechanics.LewdKeys.FlirtBeats] = 0,
                [Mechanics.LewdKeys.BadEnded] = false,
                [Mechanics.LewdKeys.Lustbrands] = "",
                [Mechanics.LewdKeys.LustbrandGlow] = "",
                [Mechanics.LewdKeys.LustbrandInhib] = 0,
                [Mechanics.LewdKeys.Imprints] = "",
                [Mechanics.LewdKeys.IntrusiveThoughts] = "",
                [Mechanics.LewdKeys.ImprintInhib] = 0,
                [Mechanics.LewdKeys.ArousalCurrentMirror] = 0,
                [Mechanics.LewdKeys.ArousalMaxMirror] = 10,
                [Mechanics.LewdKeys.ClimaxIncapTurns] = 0,
                [Mechanics.LewdKeys.Bindings] = "[]",
                [Mechanics.LewdKeys.Posture] = "standing",
                [Mechanics.LewdKeys.ArmPosition] = "free",
                [Mechanics.LewdKeys.LegPosition] = "free",
            }
        };

    public IReadOnlyDictionary<string, int> GetTurnActionBudget(Character participant) =>
        new Dictionary<string, int> { ["action"] = 1 };

    /// <summary>Core calls this for mode-scoped verbs; only an advance costs the actor's action (binds charge themselves).</summary>
    public bool TryConsumeActionSlot(ModeParticipantState state, WorldChange action, out string? errorReason)
    {
        errorReason = null;
        return action is not Changes.LewdAdvanceChange || Mechanics.LewdActionBudget.TrySpend(state, out errorReason);
    }

    public bool AdvanceTurn(ModeEncounter encounter)
    {
        if (!encounter.IsActive || encounter.Participants.Count == 0)
            return false;

        var current = encounter.Participants.FindIndex(p =>
            string.Equals(p.CharacterId, encounter.ActiveTurnId, StringComparison.OrdinalIgnoreCase));
        var idx = current < 0 ? 0 : (current + 1) % encounter.Participants.Count;
        if (current >= 0 && idx == 0)
            encounter.Round++;

        encounter.ActiveTurnId = encounter.Participants[idx].CharacterId;
        foreach (var p in encounter.Participants)
            p.ActionBudget[Mechanics.LewdActionBudget.Action] =
                string.Equals(p.CharacterId, encounter.ActiveTurnId, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        return true;
    }

    /// <summary>A lewd encounter ends by an explicit mode_transition exit, never on its own.</summary>
    public bool IsComplete(ModeEncounter encounter, out string? outcomeNarrative)
    {
        outcomeNarrative = null;
        return false;
    }
}
