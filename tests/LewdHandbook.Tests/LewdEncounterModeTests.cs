using LewdHandbook.Changes;
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
    public void CreateEncounter_seeds_scene_state_without_stance()
    {
        var mode = new LewdEncounterMode();
        var encounter = mode.StateMachine.CreateEncounter("loc-1", ["alice", "bob"]);

        Assert.Equal("lewd_encounter", encounter.ModeId);
        Assert.True(encounter.IsActive);
        Assert.Equal("alice", encounter.ActiveTurnId);
        Assert.Equal(2, encounter.Participants.Count);

        var alice = encounter.Participants[0];
        // Stance is not seeded: it comes from the character's traits unless the scene overrides it.
        Assert.False(alice.State.ContainsKey("consent"));
        Assert.Equal("willing", LewdHandbook.Mechanics.LewdProfile.Stance(alice, null));
        Assert.Equal(0, Convert.ToInt32(alice.State["climax_successes"]));
        Assert.Equal(1, alice.ActionBudget["action"]);
    }

    [Fact]
    public void Only_the_turn_owner_can_advance_and_turn_hands_the_action_on()
    {
        var sm = new LewdEncounterMode().StateMachine;
        var encounter = sm.CreateEncounter("loc-1", ["alice", "bob"]);
        var advance = new LewdAdvanceChange { ActorId = "alice", TargetId = "bob" };

        Assert.True(sm.TryConsumeActionSlot(encounter.Participants[0], advance, out _));
        Assert.False(sm.TryConsumeActionSlot(encounter.Participants[0], advance, out var again));
        Assert.Contains("no lewd action left", again);
        Assert.False(sm.TryConsumeActionSlot(encounter.Participants[1], advance, out _));
        // Non-advance verbs are free.
        Assert.True(sm.TryConsumeActionSlot(encounter.Participants[1], new LewdBadEndChange(), out _));

        Assert.True(sm.AdvanceTurn(encounter));
        Assert.Equal("bob", encounter.ActiveTurnId);
        Assert.Equal(0, encounter.Participants[0].ActionBudget["action"]);
        Assert.Equal(1, encounter.Participants[1].ActionBudget["action"]);
        Assert.Equal(1, encounter.Round);

        Assert.True(sm.AdvanceTurn(encounter));
        Assert.Equal("alice", encounter.ActiveTurnId);
        Assert.Equal(1, encounter.Participants[0].ActionBudget["action"]);
        Assert.Equal(2, encounter.Round);
    }

    [Fact]
    public void Encounter_never_completes_on_its_own()
    {
        var sm = new LewdEncounterMode().StateMachine;
        var encounter = sm.CreateEncounter("loc-1", ["alice"]);
        encounter.Participants[0].State["scene_end"] = "true";

        Assert.False(sm.IsComplete(encounter, out _));
    }

    [Fact]
    public void ValidateEntry_refuses_minor_unspecified_missing_and_minor_descriptions()
    {
        var mode = new LewdEncounterMode();
        var adult = new Character { Id = "a", Name = "Mara", LifeStage = LifeStage.Adult };
        var elder = new Character { Id = "e", Name = "Old Tom", LifeStage = LifeStage.Elder };
        var child = new Character { Id = "c", Name = "Pip", LifeStage = LifeStage.Child };
        var unset = new Character { Id = "u", Name = "Nobody" };
        var lookalike = new Character
        {
            Id = "l", Name = "Lia", LifeStage = LifeStage.Adult, VisualTags = ["schoolgirl uniform"],
        };
        var loaded = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = adult, ["e"] = elder, ["c"] = child, ["u"] = unset, ["l"] = lookalike,
        };
        IReadOnlyDictionary<string, Character> view = loaded;

        Assert.Null(mode.ValidateEntry(["a", "e"], view, null!));
        Assert.Contains("never involve minors", mode.ValidateEntry(["a", "c"], view, null!));
        Assert.Contains("no lifeStage", mode.ValidateEntry(["a", "u"], view, null!));
        Assert.Contains("not in the commit context", mode.ValidateEntry(["a", "ghost"], view, null!));
        Assert.Contains("schoolgirl", mode.ValidateEntry(["a", "l"], view, null!));
    }

    [Theory]
    [InlineData("a 16-year-old", true)]
    [InlineData("aged 12", true)]
    [InlineData("a teenager", true)]
    [InlineData("scar from childhood", false)]
    [InlineData("kidnapped once", false)]
    [InlineData("a 30-year-old smith", false)]
    [InlineData("Minor noble", false)]
    public void AgeGate_description_tripwire(string appearance, bool trips)
    {
        var character = new Character { Id = "x", Name = "X", LifeStage = LifeStage.Adult, CurrentAppearance = appearance };

        Assert.Equal(trips, LewdHandbook.Mechanics.AgeGate.DescribesMinor(character, out _));
    }
}
