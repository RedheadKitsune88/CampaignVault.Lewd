using CampaignVault.Models;
using CampaignVault.Rulesets.Modes;

namespace LewdHandbook;

/// <summary>
/// Opt-in sexual-encounter interaction mode for ActiveSystem=dnd5e.
/// Phase 0 stub: enter/exit/turn machine shaped like CraftingMode; consent scratch fields only.
/// </summary>
public sealed class LewdEncounterMode : IInteractionMode
{
    public const string ModeIdValue = "lewd_encounter";

    public string ModeId => ModeIdValue;
    public string DisplayName => "Lewd Encounter";
    public IReadOnlyList<string> CompatibleSystems => ["dnd5e"];
    public IModeStateMachine StateMachine { get; } = new LewdEncounterStateMachine();
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
            Participants = participantIds.Select(id => new ModeParticipantState
            {
                CharacterId = id,
                ActionBudget = new Dictionary<string, int> { ["action"] = 1 },
                State = new Dictionary<string, object>
                {
                    [Mechanics.LewdKeys.Consent] = Mechanics.LewdKeys.ConsentWilling,
                    [Mechanics.LewdKeys.HardLimits] = new List<string>(),
                    [Mechanics.LewdKeys.SoftLimits] = new List<string>(),
                    [Mechanics.LewdKeys.Kinks] = new List<string>(),
                    [Mechanics.LewdKeys.Inhibition] = 0,
                    [Mechanics.LewdKeys.ClimaxSuccesses] = 0,
                    [Mechanics.LewdKeys.ClimaxFailures] = 0,
                    [Mechanics.LewdKeys.Edging] = false,
                    [Mechanics.LewdKeys.Overstimulation] = 0,
                    [Mechanics.LewdKeys.ArousalCurrentMirror] = 0,
                    [Mechanics.LewdKeys.ArousalMaxMirror] = 10,
                    [Mechanics.LewdKeys.Bindings] = new List<object>(),
                    [Mechanics.LewdKeys.Posture] = "standing",
                    [Mechanics.LewdKeys.ArmPosition] = "free",
                    [Mechanics.LewdKeys.LegPosition] = "free",
                }
            }).ToList(),
            ActiveTurnId = participantIds.FirstOrDefault()
        };

    public IReadOnlyDictionary<string, int> GetTurnActionBudget(Character participant) =>
        new Dictionary<string, int> { ["action"] = 1 };

    public bool TryConsumeActionSlot(ModeParticipantState state, WorldChange action, out string? errorReason)
    {
        errorReason = null;
        if (!state.ActionBudget.TryGetValue("action", out var remaining) || remaining <= 0)
        {
            errorReason = "No lewd encounter action remaining this turn.";
            return false;
        }

        state.ActionBudget["action"] = remaining - 1;
        return true;
    }

    public bool AdvanceTurn(ModeEncounter encounter)
    {
        if (!encounter.IsActive || encounter.Participants.Count == 0)
            return false;

        var idx = encounter.Participants.FindIndex(p => p.CharacterId == encounter.ActiveTurnId);
        idx = idx < 0 ? 0 : (idx + 1) % encounter.Participants.Count;
        if (idx == 0)
            encounter.Round++;

        foreach (var p in encounter.Participants)
            p.ActionBudget["action"] = 1;

        encounter.ActiveTurnId = encounter.Participants[idx].CharacterId;

        // Handbook: start turn at max arousal → gain edging (climax save via lewd_climax_check).
        var active = encounter.Participants[idx];
        var cur = Mechanics.ConsentGate.GetInt(active, Mechanics.LewdKeys.ArousalCurrentMirror);
        var max = Mechanics.ConsentGate.GetInt(active, Mechanics.LewdKeys.ArousalMaxMirror);
        if (max > 0 && cur >= max)
            active.State[Mechanics.LewdKeys.Edging] = true;

        return true;
    }

    public bool IsComplete(ModeEncounter encounter, out string? outcomeNarrative)
    {
        outcomeNarrative = null;
        var done = encounter.Participants.Any(p =>
            p.State.TryGetValue(Mechanics.LewdKeys.SceneEnd, out var flag) &&
            string.Equals(flag?.ToString(), "true", StringComparison.OrdinalIgnoreCase));
        if (done)
            outcomeNarrative = "Lewd encounter ended.";
        return done;
    }
}
