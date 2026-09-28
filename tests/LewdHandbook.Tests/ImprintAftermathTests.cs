using CampaignVault.Models;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>Being forced deeper into an imprint leaves a timed, non-stacking shock; willing imprints cost nothing.</summary>
public sealed class ImprintAftermathTests
{
    private static Character Person()
    {
        var c = TestContext.Adult("chars/bob");
        c.SystemStats = new Dnd5eExtension { Willpower = 75 };
        return c;
    }

    private static StatusEffect? Shock(Character c) => c.SystemStats.StatusEffects.FirstOrDefault(e => e.EffectKey == ImprintAftermath.Key);

    [Fact]
    public void An_unwilling_climb_stamps_a_timed_untiered_debuff_and_drains_willpower()
    {
        var c = Person();
        ImprintState.Tick(null, c, "cruelty", willing: false, delta: 3, day: 2, convert: false, context: null, nowDays: 2.5);

        var e = Shock(c)!;
        Assert.StartsWith("Shaken", e.Name);
        Assert.Equal(-1f, e.StatModifiers["Wisdom"]);
        Assert.Equal(ImprintAftermath.AppliedBy, e.AppliedBy);
        Assert.Null(e.EffectTier);
        Assert.Equal(2.5f + 8f / 24f, e.ExpiresAtDay!.Value, 3);
        Assert.Equal(71f, c.SystemStats.Willpower);
        Assert.Equal(4f, c.SystemStats.WillpowerDrained);
    }

    [Fact]
    public void A_willing_imprint_costs_nothing()
    {
        var c = Person();
        ImprintState.Tick(null, c, "wanton", willing: true, delta: 3, day: 0, convert: false, context: null, nowDays: 0);
        Assert.Null(Shock(c));
        Assert.Equal(75f, c.SystemStats.Willpower);
    }

    [Fact]
    public void A_tick_that_does_not_change_level_adds_nothing()
    {
        var c = Person();
        ImprintState.Tick(null, c, "cruelty", willing: false, delta: 3, day: 0, convert: false, context: null, nowDays: 0);
        var willpower = c.SystemStats.Willpower;
        ImprintState.Tick(null, c, "cruelty", willing: false, delta: 1, day: 0, convert: false, context: null, nowDays: 0);
        Assert.Equal(willpower, c.SystemStats.Willpower);
    }

    [Fact]
    public void Tracks_share_one_effect_and_a_lower_climb_never_weakens_it()
    {
        var c = Person();
        ImprintState.Tick(null, c, "cruelty", willing: false, delta: 3, day: 0, convert: false, context: null, nowDays: 0);
        ImprintState.Tick(null, c, "cruelty", willing: false, delta: 1, day: 0, convert: false, context: null, nowDays: 0);
        ImprintState.Tick(null, c, "cruelty", willing: false, delta: 3, day: 0, convert: false, context: null, nowDays: 0); // 7 points: level 2
        Assert.StartsWith("Rattled", Shock(c)!.Name);

        ImprintState.Tick(null, c, "ordeal", willing: false, delta: 3, day: 1, convert: false, context: null, nowDays: 1); // another track, level 1
        Assert.Single(c.SystemStats.StatusEffects, e => e.EffectKey == ImprintAftermath.Key);
        Assert.StartsWith("Rattled", Shock(c)!.Name);
        Assert.Equal(-2f, Shock(c)!.StatModifiers["Wisdom"]);
        Assert.True(Shock(c)!.ExpiresAtDay > 1f + 8f / 24f - 0.001f);
    }

    [Fact]
    public void Willpower_never_drops_below_zero()
    {
        var c = Person();
        c.SystemStats.Willpower = 2;
        ImprintState.Tick(null, c, "cruelty", willing: false, delta: 3, day: 0, convert: false, context: null, nowDays: 0);
        Assert.Equal(0f, c.SystemStats.Willpower);
    }
}
