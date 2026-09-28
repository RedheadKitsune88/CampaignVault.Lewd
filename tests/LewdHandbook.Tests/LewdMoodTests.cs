using CampaignVault.Models;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>Mood buffs are engine-owned and guarded: one at a time, no repeats per day, capped, switchable.</summary>
public sealed class LewdMoodTests
{
    private static Character Person()
    {
        var c = TestContext.Adult("chars/amy");
        c.SystemStats = new Dnd5eExtension();
        return c;
    }

    private static readonly LewdSettings On = LewdSettings.Default;
    private static StatusEffect? Mood(Character c) => c.SystemStats.StatusEffects.FirstOrDefault(e => e.EffectKey == LewdMood.Key);

    [Fact]
    public void A_grant_is_a_timed_untiered_buff()
    {
        var c = Person();
        Assert.NotNull(LewdMood.TryGrant(c, LewdMood.WarmGlow, "bob", 3, 3.5, On));
        var e = Mood(c)!;
        Assert.Equal(1f, e.StatModifiers["Persuasion"]);
        Assert.Null(e.EffectTier);
        Assert.Equal(LewdMood.AppliedBy, e.AppliedBy);
        Assert.Equal(3.5f + 1f / 24f, e.ExpiresAtDay!.Value, 3);
    }

    [Fact]
    public void The_setting_switches_it_off()
    {
        var c = Person();
        Assert.Null(LewdMood.TryGrant(c, LewdMood.WarmGlow, "bob", 0, 0, LewdSettings.From(new Dictionary<string, string> { ["lewdMoodBuffs"] = "off" })));
        Assert.Null(Mood(c));
    }

    [Fact]
    public void The_same_trigger_and_partner_pays_once_a_day_but_again_tomorrow()
    {
        var c = Person();
        Assert.NotNull(LewdMood.TryGrant(c, LewdMood.WarmGlow, "bob", 1, 1.0, On));
        Assert.Null(LewdMood.TryGrant(c, LewdMood.WarmGlow, "bob", 1, 1.5, On));
        Assert.NotNull(LewdMood.TryGrant(c, LewdMood.WarmGlow, "bob", 2, 2.0, On));
    }

    [Fact]
    public void Only_one_mood_lives_at_a_time_and_a_stronger_one_replaces_a_weaker()
    {
        var c = Person();
        LewdMood.TryGrant(c, LewdMood.WarmGlow, "bob", 1, 1.0, On);
        Assert.Null(LewdMood.TryGrant(c, LewdMood.WarmGlow, "cat", 1, 1.01, On)); // still live, same rank
        Assert.NotNull(LewdMood.TryGrant(c, LewdMood.Afterglow, "amy+bob", 1, 1.02, On));
        Assert.Single(c.SystemStats.StatusEffects, e => e.EffectKey == LewdMood.Key);
        Assert.Equal("Afterglow", Mood(c)!.Name);
        Assert.Null(LewdMood.TryGrant(c, LewdMood.WarmGlow, "dan", 1, 1.03, On)); // weaker never replaces
        Assert.Equal("Afterglow", Mood(c)!.Name);
    }

    [Fact]
    public void An_expired_mood_can_be_replaced_by_a_new_one()
    {
        var c = Person();
        LewdMood.TryGrant(c, LewdMood.WarmGlow, "bob", 1, 1.0, On);
        Assert.NotNull(LewdMood.TryGrant(c, LewdMood.WarmGlow, "cat", 1, 1.5, On));
    }

    [Fact]
    public void The_daily_cap_holds()
    {
        var c = Person();
        for (var i = 0; i < LewdMood.DailyCap; i++)
            Assert.NotNull(LewdMood.TryGrant(c, LewdMood.WarmGlow, $"p{i}", 5, 5.0 + i * 0.1, On));
        Assert.Null(LewdMood.TryGrant(c, LewdMood.WarmGlow, "extra", 5, 5.5, On));
        Assert.NotNull(LewdMood.TryGrant(c, LewdMood.WarmGlow, "extra", 6, 6.0, On));
    }
}
