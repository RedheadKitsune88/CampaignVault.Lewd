using CampaignVault.Models;
using CampaignVault.Rulesets.Modes;
using Xunit;

namespace LewdHandbook.Tests;

public class LewdEncounterModeTests
{
    [Fact]
    public void Mode_exposes_stable_id_and_dnd5e_compatibility()
    {
        IInteractionMode mode = new LewdEncounterMode();

        Assert.Equal("lewd_encounter", mode.ModeId);
        Assert.Equal(["dnd5e"], mode.CompatibleSystems);
        Assert.NotNull(mode.StateMachine);
    }

    [Fact]
    public void CreateEncounter_seeds_consent_scratch_state()
    {
        var mode = new LewdEncounterMode();
        var encounter = mode.StateMachine.CreateEncounter("loc-1", ["alice", "bob"]);

        Assert.Equal("lewd_encounter", encounter.ModeId);
        Assert.True(encounter.IsActive);
        Assert.Equal("alice", encounter.ActiveTurnId);
        Assert.Equal(2, encounter.Participants.Count);

        var alice = encounter.Participants[0];
        Assert.Equal("willing", alice.State["consent"]?.ToString());
        Assert.Equal(0, Convert.ToInt32(alice.State["climax_successes"]));
        Assert.Equal(1, alice.ActionBudget["action"]);
    }

    [Fact]
    public void AdvanceTurn_wraps_and_refills_budget()
    {
        var sm = new LewdEncounterMode().StateMachine;
        var encounter = sm.CreateEncounter("loc-1", ["alice", "bob"]);

        Assert.True(sm.TryConsumeActionSlot(encounter.Participants[0], new ModeTransitionChange(), out _));
        Assert.Equal(0, encounter.Participants[0].ActionBudget["action"]);

        Assert.True(sm.AdvanceTurn(encounter));
        Assert.Equal("bob", encounter.ActiveTurnId);
        Assert.Equal(1, encounter.Participants[0].ActionBudget["action"]);
        Assert.Equal(1, encounter.Round);

        Assert.True(sm.AdvanceTurn(encounter));
        Assert.Equal("alice", encounter.ActiveTurnId);
        Assert.Equal(2, encounter.Round);
    }

    [Fact]
    public void IsComplete_when_scene_end_flag_set()
    {
        var sm = new LewdEncounterMode().StateMachine;
        var encounter = sm.CreateEncounter("loc-1", ["alice"]);

        Assert.False(sm.IsComplete(encounter, out _));

        encounter.Participants[0].State["scene_end"] = "true";
        Assert.True(sm.IsComplete(encounter, out var narrative));
        Assert.Contains("ended", narrative, StringComparison.OrdinalIgnoreCase);
    }
}
