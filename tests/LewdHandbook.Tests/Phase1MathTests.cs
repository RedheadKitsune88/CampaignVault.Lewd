using CampaignVault.Models;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public class Phase1MathTests
{
    [Fact]
    public void Fourth_climax_while_incapacitated_adds_overstim()
    {
        var first = OverstimMath.OnClimax(0, wasIncapacitated: false, overstimBefore: 0);
        Assert.Equal(1, first.ClimaxStreak);
        Assert.Equal(0, first.OverstimulationAfter);
        Assert.Null(first.IncapacitationCondition);

        var second = OverstimMath.OnClimax(first.ClimaxStreak, wasIncapacitated: true, 0);
        Assert.Equal(2, second.ClimaxStreak);
        Assert.Equal("stunned", second.IncapacitationCondition);

        var third = OverstimMath.OnClimax(second.ClimaxStreak, true, 0);
        Assert.Equal(3, third.ClimaxStreak);
        Assert.Equal("paralyzed", third.IncapacitationCondition);
        Assert.False(third.OverstimIncreased);

        var fourth = OverstimMath.OnClimax(third.ClimaxStreak, true, 0);
        Assert.Equal(4, fourth.ClimaxStreak);
        Assert.Equal(1, fourth.OverstimulationAfter);
        Assert.True(fourth.OverstimIncreased);
    }

    [Fact]
    public void Recovered_climax_resets_streak()
    {
        var tick = OverstimMath.OnClimax(4, wasIncapacitated: false, overstimBefore: 1);
        Assert.Equal(1, tick.ClimaxStreak);
        Assert.False(tick.OverstimIncreased);
    }

    [Fact]
    public void Extended_edging_ticks_after_inhibition_hours()
    {
        Assert.Null(OverstimMath.ExtendedEdgingOverstim(10, inhibitionBonus: 2, overstimBefore: 0));
        Assert.Equal(1, OverstimMath.ExtendedEdgingOverstim(30, inhibitionBonus: 2, overstimBefore: 0));
        Assert.Null(OverstimMath.ExtendedEdgingOverstim(31, inhibitionBonus: 2, overstimBefore: 1));
    }

    [Fact]
    public void Verbal_cap_for_virgin_not_for_kinkster()
    {
        var virgin = Target("virgin");
        Assert.Equal(2, ConsentGate.CapVerbalStimulation(virgin, null, "skilled", ["verbal"], 8, flirtBeats: 1));
        Assert.Equal(8, ConsentGate.CapVerbalStimulation(virgin, null, "skilled", ["verbal", "physical"], 8, flirtBeats: 1));

        var kink = Target("experienced_kinkster");
        Assert.Equal(8, ConsentGate.CapVerbalStimulation(kink, null, "skilled", ["verbal"], 8, flirtBeats: 0));
    }

    [Fact]
    public void Piercing_kink_matches_penetration_alias()
    {
        var t = Target("virgin");
        t.State[LewdKeys.Kinks] = new List<string> { "penetration" };
        Assert.Equal(15, ConsentGate.AdjustStimulationForTags(t, 10, "piercing", null));
    }

    [Fact]
    public void Verbal_climax_blocked_until_physical()
    {
        var virgin = Target("virgin");
        Assert.True(ConsentGate.BlocksVerbalClimax(virgin, null, "skilled", ["verbal"]));
        virgin.State[LewdKeys.HadPhysical] = true;
        Assert.False(ConsentGate.BlocksVerbalClimax(virgin, null, "skilled", ["verbal"]));
    }

    private static ModeParticipantState Target(string history) =>
        new()
        {
            CharacterId = "bob",
            State = new Dictionary<string, object>
            {
                [LewdKeys.TraitSexualHistory] = history,
                [LewdKeys.Kinks] = new List<string>(),
                [LewdKeys.SoftLimits] = new List<string>(),
            }
        };
}
