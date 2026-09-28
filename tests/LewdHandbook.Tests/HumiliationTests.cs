using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public sealed class HumiliationTests
{
    private static Character Adult(string id)
    {
        var c = TestContext.Adult(id);
        c.SystemStats.Willpower = 40;
        c.SystemStats.WillpowerDrained = 0;
        var arousal = LewdPoolHelper.Arousal(c);
        arousal.Max = 20;
        arousal.Current = 0;
        return c;
    }

    [Fact]
    public async Task Shame_drains_willpower_and_stamps_untiered_status()
    {
        var who = Adult("chars/amy");
        var ctx = new TestContext(null, who);
        ctx.Options["lewdNonConsent"] = "on";

        var result = await new LewdHumiliateHandler().ApplyAsync(new LewdHumiliateChange
        {
            CharacterId = who.Id,
            Severity = 2,
            Tags = ["public", "piercing"],
            SourceId = "chars/crowd",
        }, ctx);

        Assert.True(result.Success, result.Message);
        Assert.Equal(36f, who.SystemStats.Willpower);
        Assert.Equal(4f, who.SystemStats.WillpowerDrained);
        var effect = who.SystemStats.StatusEffects.Single(e => e.EffectKey == Humiliation.EffectKey);
        Assert.Null(effect.EffectTier);
        Assert.Equal(-1f, effect.StatModifiers["Charisma"]);
        Assert.Equal(-1f, effect.StatModifiers["Wisdom"]);
        Assert.Contains(ctx.Published, p => p.Topic == LewdHandbook.Events.LewdEvents.Humiliated);
        Assert.Equal(0, who.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current); // ordeal 0 → no arousal
    }

    [Fact]
    public async Task Ordeal_gates_arousal_from_shame()
    {
        var who = Adult("chars/amy");
        ImprintState.Seed(null, who, "ordeal", 2, willing: false, day: 1, context: null);
        var ctx = new TestContext(null, who);
        ctx.Options["lewdNonConsent"] = "on";

        var result = await new LewdHumiliateHandler().ApplyAsync(new LewdHumiliateChange
        {
            CharacterId = who.Id,
            Severity = 3,
            Tags = ["public"],
            ArousalD4 = 3,
        }, ctx);

        Assert.True(result.Success);
        // severity 3 + ordeal 2 → 1d4(3) + 2 = 5
        Assert.Equal(5, who.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current);
        Assert.Equal("5", ctx.Field(LewdHandbook.Events.LewdEvents.Humiliated, "arousalDelta"));
    }

    [Fact]
    public void Arousal_math_ignores_inhibition_inputs()
    {
        Assert.Equal(0, Humiliation.ArousalDelta(1, ordealLevel: 3, d4Face: 4));
        Assert.Equal(0, Humiliation.ArousalDelta(2, ordealLevel: 0, d4Face: 4));
        Assert.Equal(4, Humiliation.ArousalDelta(2, ordealLevel: 1, d4Face: 4));
        Assert.Equal(4, Humiliation.ArousalDelta(3, ordealLevel: 1, d4Face: 4));
        Assert.Equal(7, Humiliation.ArousalDelta(3, ordealLevel: 3, d4Face: 4));
    }

    [Fact]
    public async Task Setting_off_and_hard_limit_refuse()
    {
        var who = Adult("chars/amy");
        var ctx = new TestContext(null, who);
        ctx.Options["lewdHumiliation"] = "off";
        Assert.False((await new LewdHumiliateHandler().ApplyAsync(new LewdHumiliateChange
        {
            CharacterId = who.Id,
            Severity = 1,
        }, ctx)).Success);

        ctx.Options["lewdHumiliation"] = "on";
        ctx.Options["lewdHardLimits"] = "humiliation";
        Assert.False((await new LewdHumiliateHandler().ApplyAsync(new LewdHumiliateChange
        {
            CharacterId = who.Id,
            Severity = 1,
        }, ctx)).Success);
    }

    [Fact]
    public async Task Daily_cap_and_duplicate_scope_skip()
    {
        var who = Adult("chars/amy");
        var ctx = new TestContext(null, who);
        ctx.Options["lewdNonConsent"] = "on";
        var handler = new LewdHumiliateHandler();

        for (var i = 0; i < Humiliation.DailyCap; i++)
        {
            Assert.True((await handler.ApplyAsync(new LewdHumiliateChange
            {
                CharacterId = who.Id,
                Severity = 1,
                SourceId = $"src/{i}",
            }, ctx)).Success);
        }

        var before = who.SystemStats.Willpower;
        Assert.True((await handler.ApplyAsync(new LewdHumiliateChange
        {
            CharacterId = who.Id,
            Severity = 1,
            SourceId = "src/extra",
        }, ctx)).Success);
        Assert.Equal(before, who.SystemStats.Willpower); // skipped
        Assert.Contains(ctx.Messages, m => m.Contains("skipped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Piercing_nudge_on_heavy_or_bell()
    {
        var who = Adult("chars/amy");
        var ctx = new TestContext(null, who);
        var handler = new LewdPiercingNudgeHandler();
        var e = DomainEvent.Create(CoreEvents.Pierced, new Dictionary<string, object?>
        {
            [CoreEvents.Fields.CharacterId] = who.Id,
            [CoreEvents.Fields.Action] = "added",
            [CoreEvents.Fields.Site] = "nipple.left",
            [CoreEvents.Fields.Kind] = "lewd.nipple_ring",
            [CoreEvents.Fields.Load] = "heavy",
            [CoreEvents.Fields.Tags] = new[] { "bell" },
        });

        await handler.HandleAsync(e, ctx);
        Assert.Contains(ctx.Messages, m => m.Contains("lewd_humiliate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ctx.Nudges, n => n.Contains("piercing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Receiver_pain_bonus_uses_ordeal_only()
    {
        var who = Adult("chars/amy");
        Assert.Equal(0, OrdealClimb.ReceiverPainBonus(who, ["pain"]));
        ImprintState.Seed(null, who, "ordeal", 2, willing: true, day: 1, context: null);
        Assert.Equal(2, OrdealClimb.ReceiverPainBonus(who, ["pain"]));
        Assert.Equal(0, OrdealClimb.ReceiverPainBonus(who, ["phallic"]));
    }
}
