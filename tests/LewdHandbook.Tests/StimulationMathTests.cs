using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public class StimulationMathTests
{
    [Fact]
    public void Numbing_absorbs_before_arousal()
    {
        var r = StimulationMath.Apply(arousalCurrent: 3, arousalMax: 20, numbingCurrent: 5, stimulation: 7);
        Assert.Equal(0, r.NumbingAfter);
        Assert.Equal(5, r.AbsorbedByNumbing);
        Assert.Equal(5, r.ArousalAfter); // 3 + 2
        Assert.Equal(2, r.AppliedToArousal);
        Assert.False(r.InstantClimax);
    }

    [Fact]
    public void Stim_at_max_arousal_adds_auto_failures()
    {
        var r = StimulationMath.Apply(10, 10, 0, 3, isCritical: false);
        Assert.Equal(1, r.AutoClimaxFailures);
        Assert.True(r.WasAlreadyAtMax);
        Assert.Equal(10, r.ArousalAfter);
    }

    [Fact]
    public void Critical_stim_at_max_adds_two_failures()
    {
        var r = StimulationMath.Apply(10, 10, 0, 1, isCritical: true);
        Assert.Equal(2, r.AutoClimaxFailures);
    }

    [Fact]
    public void Stim_magnitude_at_least_max_is_instant_climax()
    {
        var r = StimulationMath.Apply(0, 10, 0, 10);
        Assert.True(r.InstantClimax);
        Assert.Equal(10, r.ArousalAfter);
    }

    [Fact]
    public void Long_rest_reduces_by_half_max()
    {
        Assert.Equal(7, StimulationMath.LongRestArousalReduction(12, 10)); // 12 - (10/2)
        Assert.Equal(0, StimulationMath.LongRestArousalReduction(3, 10));
    }
}
