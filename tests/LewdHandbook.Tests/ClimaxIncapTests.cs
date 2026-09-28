using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public class ClimaxIncapTests
{
    private static (Character Bob, TestContext Ctx) Alone()
    {
        var bob = TestContext.Adult("bob");
        bob.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 5, Max = 10 };
        return (bob, new TestContext(null, bob) { Time = new CampaignTime { TotalDaysElapsed = 4, Hour = 12 } });
    }

    [Fact]
    public async Task A_forced_climax_outside_a_scene_stamps_a_timed_block_that_lapses_with_the_clock()
    {
        var (bob, ctx) = Alone();

        await new LewdClimaxCheckHandler().ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", ForceClimax = true }, ctx);

        var incap = Assert.Single(bob.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionIncapacitated);
        Assert.Equal(4.5f + 1f / 24f, incap.ExpiresAtDay!.Value, 3);
        Assert.Equal(1f, incap.StatModifiers["BlocksAllActions"]);
        Assert.Contains(ctx.Messages, m => m.Contains("Concentration check DC 16"));
    }

    [Fact]
    public void Scene_end_clears_the_block_and_the_streak()
    {
        var (bob, _) = Alone();
        LewdPoolHelper.StampClimaxIncapacitation(bob, LewdKeys.ConditionIncapacitated, 4.5);
        LewdPoolHelper.StampClimaxIncapacitation(bob, LewdKeys.ConditionStunned, 4.5);

        LewdPoolHelper.EndClimaxIncapacitation(null, bob);

        Assert.DoesNotContain(bob.SystemStats.StatusEffects, e => e.Name is LewdKeys.ConditionIncapacitated or LewdKeys.ConditionStunned);
    }

    [Fact]
    public async Task Concentration_dc_counts_climaxes_at_the_same_clock_reading()
    {
        var (bob, ctx) = Alone();
        var handler = new LewdClimaxCheckHandler();

        await handler.ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", ForceClimax = true }, ctx);
        await handler.ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", ForceClimax = true }, ctx);

        Assert.Contains(ctx.Messages, m => m.Contains("Concentration check DC 17"));
        Assert.Equal(2, ClimaxLog.Count(bob, 4 * 24 + 12, ClimaxLog.Hour));
    }

    [Theory]
    [InlineData(10, false)] // 10 + Con 0 = 10 < DC 13 (one climax this hour)
    [InlineData(13, true)]
    public async Task Recovery_save_is_dc_12_plus_climaxes_in_the_last_hour(int face, bool recovers)
    {
        var (bob, ctx) = Alone();
        await new LewdClimaxCheckHandler().ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", ForceClimax = true }, ctx);

        var result = await new LewdRecoverHandler().ApplyAsync(new LewdRecoverChange { CharacterId = "bob", Save = true, D20 = face }, ctx);

        Assert.True(result.Success, result.Message);
        Assert.Contains(ctx.Messages, m => m.Contains("DC 13"));
        Assert.Equal(!recovers, LewdPoolHelper.HasClimaxIncapacitation(bob));
    }

    [Fact]
    public async Task Recovery_save_needs_a_climax_incapacitation()
    {
        var (_, ctx) = Alone();

        var result = await new LewdRecoverHandler().ApplyAsync(new LewdRecoverChange { CharacterId = "bob", Save = true, D20 = 20 }, ctx);

        Assert.False(result.Success);
    }
}

public class HandbookConditionTests
{
    private static Character Bearer(string condition, int inhibition)
    {
        var c = TestContext.Adult("bob");
        c.SystemStats.Traits[LewdKeys.TraitInhibition] = inhibition.ToString(System.Globalization.CultureInfo.InvariantCulture);
        c.SystemStats.StatusEffects.Add(new StatusEffect { Name = condition, ConditionName = condition });
        return c;
    }

    [Theory]
    [InlineData("nymphomanic", 3, 0)]
    [InlineData("infatuated", 3, 0)]
    [InlineData("nymphomanic", -2, -2)]
    [InlineData("intoxicated", 3, 3)]
    public void Nymphomanic_and_infatuated_cap_a_positive_inhibition_at_zero(string condition, int inhibition, int expected)
    {
        Assert.Equal(expected, LewdProfile.Inhibition(null, Bearer(condition, inhibition)));
    }

    [Fact]
    public void Nymphomanic_does_not_make_an_unwilling_character_wanting()
    {
        var c = Bearer("nymphomanic", 3);
        c.SystemStats.Traits[LewdKeys.TraitStance] = LewdKeys.ConsentUnwilling;

        Assert.False(LewdProfile.Wants(null, c, "alice"));
    }

    [Theory]
    [InlineData("intoxicated", "wis", true)]
    [InlineData("nymphomanic", "int", true)]
    [InlineData("intoxicated", "con", false)]
    [InlineData("flustered", "cha", true)]
    [InlineData("flustered", "int", false)]
    public void Conditions_put_the_right_saves_at_disadvantage(string condition, string ability, bool disadvantage)
    {
        Assert.Equal(disadvantage, SaveConditions.Disadvantage(Bearer(condition, 0), ability));
    }
}
