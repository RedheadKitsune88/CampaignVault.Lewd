using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>Staying tied up wears people down on a fixed ladder, driven by the host's time hook.</summary>
public sealed class RestraintStrainTests
{
    private static async Task<(TestContext Ctx, Character C)> BoundAsync(bool erotic = false, params string[] sites)
    {
        var guard = TestContext.Adult("chars/guard");
        var c = TestContext.Adult("chars/convict");
        c.SystemStats = new Dnd5eExtension { Dexterity = 14 };
        c.SystemStats.StatusEffects.Add(new StatusEffect { Name = "grappled", ConditionName = "grappled", AppliedBy = "combat" });
        var ctx = new TestContext(null, guard, c);
        var result = await new LewdBindHandler().ApplyAsync(new LewdBindChange
        {
            ActorId = "chars/guard", TargetId = "chars/convict", Kind = "rope", Sites = sites.Length == 0 ? ["wrists", "ankles"] : [.. sites],
            Erotic = erotic, Willing = erotic,
        }, ctx);
        Assert.True(result.Success, result.Message);
        return (ctx, c);
    }

    private static Task Pass(TestContext ctx, double hours, double total) =>
        new RestraintTimeObserver().OnTimeAdvancedAsync(
            new TimeAdvance("rest", hours, total, ["chars/convict"], null, null), ctx);

    private static StatusEffect? Arms(Character c) => c.SystemStats.StatusEffects.FirstOrDefault(e => e.EffectKey == RestraintStrain.ArmsKey);
    private static StatusEffect? Legs(Character c) => c.SystemStats.StatusEffects.FirstOrDefault(e => e.EffectKey == RestraintStrain.LegsKey);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3.9, 0)]
    [InlineData(4, 1)]
    [InlineData(12, 2)]
    [InlineData(24, 3)]
    [InlineData(100, 3)]
    public void The_ladder_thresholds_are_fixed(double hours, int level) => Assert.Equal(level, RestraintStrain.LevelFor(hours));

    [Fact]
    public async Task Nothing_happens_before_four_hours()
    {
        var (ctx, c) = await BoundAsync();
        await Pass(ctx, 2, 50);

        Assert.Null(Arms(c));
        Assert.Null(Legs(c));
    }

    [Fact]
    public async Task Arms_and_legs_escalate_separately_and_stack()
    {
        var (ctx, c) = await BoundAsync();

        await Pass(ctx, 6, 100);
        Assert.Equal("Cramped arms", Arms(c)!.Name);
        Assert.Equal(-1, Arms(c)!.StatModifiers["AttackRoll"]);
        Assert.Equal("Stiff legs", Legs(c)!.Name);
        Assert.Equal(-5, Legs(c)!.StatModifiers["Speed"]);
        Assert.True(Arms(c)!.ExpiresAtDay > 100 / 24f);

        await Pass(ctx, 6, 106); // 12h
        Assert.Equal(-2, Arms(c)!.StatModifiers["AttackRoll"]);
        Assert.Equal(-10, Legs(c)!.StatModifiers["Speed"]);

        await Pass(ctx, 6, 112);
        await Pass(ctx, 6, 118); // 24h
        Assert.Equal("Dead arms", Arms(c)!.Name);
        Assert.Equal("Failing legs", Legs(c)!.Name);
        // Still inside the host's serious tier: at most ±3 (Speed ±20) and 24h.
        Assert.All(Arms(c)!.StatModifiers.Values, v => Assert.InRange(Math.Abs(v), 0, 3));
        Assert.True(Legs(c)!.ExpiresAtDay <= 118 / 24f + 1f + 0.001f);
        Assert.Single(c.SystemStats.StatusEffects, e => e.EffectKey == RestraintStrain.ArmsKey);
    }

    [Fact]
    public async Task Only_the_area_that_is_tied_is_affected()
    {
        var (ctx, c) = await BoundAsync(false, "ankles");
        await Pass(ctx, 6, 100);

        Assert.Null(Arms(c));
        Assert.NotNull(Legs(c));
    }

    [Fact]
    public async Task The_aftermath_is_tagged_apart_from_events_and_never_counts_toward_the_event_debuff_cap()
    {
        var (ctx, c) = await BoundAsync();
        for (var i = 0; i < 2; i++)
            c.SystemStats.StatusEffects.Add(new StatusEffect
            {
                Name = $"Sprain{i}", EffectKey = $"k{i}", EffectTier = "moderate", StatModifiers = { ["AllChecks"] = -1 },
            });

        await Pass(ctx, 6, 100);

        foreach (var e in new[] { Arms(c)!, Legs(c)! })
        {
            Assert.Equal("restraint_aftermath", e.AppliedBy);
            Assert.Null(e.EffectTier); // the host's cap only counts tiered (apply_effect) effects
        }
    }

    [Fact]
    public async Task Consensual_scene_gear_stops_at_the_first_stage_unless_the_campaign_runs_non_consent()
    {
        var (ctx, c) = await BoundAsync(erotic: true);
        for (var h = 6; h <= 60; h += 6)
            await Pass(ctx, 6, 100 + h);
        Assert.Equal("Cramped arms", Arms(c)!.Name);
        Assert.Equal(75f - 5f - 5f, c.SystemStats.Willpower); // one stage per area, nothing further

        var (ctx2, c2) = await BoundAsync(erotic: true);
        ctx2.Options[LewdKeys.NonConsentOption] = LewdKeys.NonConsentOn;
        for (var h = 6; h <= 60; h += 6)
            await Pass(ctx2, 6, 100 + h);
        Assert.Equal("Dead arms", Arms(c2)!.Name);
    }

    [Fact]
    public async Task A_gag_alone_does_not_strain_the_limbs()
    {
        var (ctx, c) = await BoundAsync(false, "mouth");
        await Pass(ctx, 6, 100);
        await Pass(ctx, 6, 106);

        Assert.Null(Arms(c));
        Assert.Null(Legs(c));
    }

    [Fact]
    public async Task Freeing_them_stops_the_escalation_and_the_effect_runs_out()
    {
        var (ctx, c) = await BoundAsync();
        await Pass(ctx, 6, 100);
        await new LewdUnbindHandler().ApplyAsync(
            new LewdUnbindChange { ActorId = "chars/guard", TargetId = "chars/convict", RemoveAll = true }, ctx);
        await Pass(ctx, 6, 106);

        Assert.Equal("Cramped arms", Arms(c)!.Name); // still there (aftereffect), no longer growing
        Assert.Empty(BindingGraph.GetBindings(null, c));
    }

    [Fact]
    public async Task A_character_the_age_gate_refuses_is_left_alone()
    {
        var (ctx, c) = await BoundAsync();
        c.LifeStage = LifeStage.Child;
        await Pass(ctx, 12, 100);

        Assert.Null(Arms(c));
    }

    [Fact]
    public async Task Restraint_sync_does_not_strip_the_aftermath()
    {
        var (ctx, c) = await BoundAsync();
        await Pass(ctx, 6, 100);
        Restraint.Sync(c, BindingGraph.GetBindings(null, c));

        Assert.NotNull(Arms(c));
        Assert.NotNull(Legs(c));
    }

    [Fact]
    public async Task Captivity_drains_willpower_per_stage_and_records_it_for_core_to_restore()
    {
        var (ctx, c) = await BoundAsync();
        c.SystemStats.Willpower = 50f;

        await Pass(ctx, 6, 100);

        Assert.Equal(40f, c.SystemStats.Willpower); // arms 5 + legs 5
        Assert.Equal(10f, c.SystemStats.WillpowerDrained); // core's rest step gives this back, and only this
        Assert.Contains(ctx.Messages, m => m.Contains("willpower −5"));
    }

    [Fact]
    public async Task The_plugin_no_longer_restores_willpower_itself_core_does_on_rest()
    {
        var (ctx, c) = await BoundAsync();
        c.SystemStats.Willpower = 50f;
        await Pass(ctx, 6, 100);
        await new LewdUnbindHandler().ApplyAsync(
            new LewdUnbindChange { ActorId = "chars/guard", TargetId = "chars/convict", RemoveAll = true }, ctx);

        await new RestraintTimeObserver().OnTimeAdvancedAsync(
            new TimeAdvance("rest", 4, 110, ["chars/convict"], null, null), ctx);

        Assert.Equal(40f, c.SystemStats.Willpower);
    }

    [Fact]
    public async Task Drain_stops_at_zero_and_reports_what_was_lost()
    {
        var c = TestContext.Adult("chars/x");
        c.SystemStats.Willpower = 8f;

        Assert.Equal(8f, Willpower.Drain(c, 15f));
        Assert.Equal(0f, c.SystemStats.Willpower);
        Assert.Equal(8f, c.SystemStats.WillpowerDrained);
    }
}
