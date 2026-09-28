using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public class JoinAndAutoRollTests
{
    [Fact]
    public void A_joiner_starts_without_an_action_and_a_leaver_hands_their_turn_on()
    {
        var machine = new LewdEncounterMode().StateMachine;
        var scene = machine.CreateEncounter("loc", ["alice", "bob"]);

        Assert.True(machine.TryAddParticipant(scene, "cara", out _));
        var cara = scene.Participants[2];
        Assert.Equal(0, cara.ActionBudget[LewdActionBudget.Action]);
        Assert.Equal(0, ConsentGate.GetInt(cara, LewdKeys.Overstimulation));
        Assert.False(machine.TryAddParticipant(scene, "CARA", out var dup));
        Assert.Contains("already", dup);

        Assert.True(machine.TryRemoveParticipant(scene, "alice", out _));
        Assert.Equal("bob", scene.ActiveTurnId);
        Assert.Equal(1, scene.Participants[0].ActionBudget[LewdActionBudget.Action]);
        Assert.False(machine.TryRemoveParticipant(scene, "alice", out _));
    }

    [Fact]
    public void Narration_nudge_is_recorded_once_per_commit()
    {
        var ctx = new TestContext();
        var settings = LewdSettings.From(new Dictionary<string, string> { [LewdKeys.NarrationOption] = LewdKeys.NarrationFade });

        settings.Narrate(ctx);
        settings.Narrate(ctx);

        Assert.Single(ctx.Nudges);
    }

    private static TestContext PairWithRolls(int? face)
    {
        var ctx = new TestContext(null, TestContext.Adult("alice"), TestContext.Adult("bob"));
        if (face is { } f)
            ctx.Rolls = new FixedRolls(f);
        return ctx;
    }

    [Fact]
    public async Task Impregnation_rolls_its_own_d20_when_the_model_supplies_none()
    {
        var withRolls = PairWithRolls(20);
        var rolled = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange { TargetId = "bob", ActorId = "alice" }, withRolls);
        Assert.True(rolled.Success, rolled.Message);
        Assert.Contains(withRolls.Messages, m => m.Contains("impregnated"));

        var noRolls = PairWithRolls(null);
        var refused = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange { TargetId = "bob", ActorId = "alice" }, noRolls);
        Assert.False(refused.Success);
        Assert.Contains("d20", refused.Message);

        var supplied = PairWithRolls(null);
        var given = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange { TargetId = "bob", ActorId = "alice", D20 = 20 }, supplied);
        Assert.True(given.Success, given.Message);
    }
}
