using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public class ClimaxMathTests
{
    [Fact]
    public void Third_failure_climaxes()
    {
        var r = ClimaxMath.ResolveSave(d20: 2, inhibitionBonus: 0, successes: 0, failures: 2, arousalCurrent: 10, arousalMax: 10);
        Assert.Equal(ClimaxOutcomeKind.Climaxed, r.Kind);
        Assert.False(r.EdgingAfter);
        Assert.Equal(0, r.Failures);
    }

    [Fact]
    public void Third_success_holds_edge()
    {
        var r = ClimaxMath.ResolveSave(18, 0, successes: 2, failures: 1, arousalCurrent: 10, arousalMax: 10);
        Assert.Equal(ClimaxOutcomeKind.HeldEdge, r.Kind);
        Assert.Equal(9, r.ArousalAfter);
        Assert.False(r.EdgingAfter);
    }

    [Fact]
    public void Natural_20_holds_immediately()
    {
        var r = ClimaxMath.ResolveSave(20, -2, 0, 2, 10, 10);
        Assert.Equal(ClimaxOutcomeKind.HeldEdge, r.Kind);
        Assert.Equal(9, r.ArousalAfter);
    }

    [Fact]
    public void Natural_1_counts_as_two_failures()
    {
        var r = ClimaxMath.ResolveSave(1, 5, 0, 1, 10, 10);
        Assert.Equal(ClimaxOutcomeKind.Climaxed, r.Kind); // 1 + 2 = 3
    }

    [Fact]
    public void Instant_climax_from_auto_path()
    {
        var r = ClimaxMath.ApplyAutoFailures(0, 0, 0, 10, 10, instantClimax: true);
        Assert.Equal(ClimaxOutcomeKind.InstantClimax, r.Kind);
    }
}
