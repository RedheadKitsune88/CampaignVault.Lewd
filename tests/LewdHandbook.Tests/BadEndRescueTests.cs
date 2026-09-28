using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using LewdHandbook.Observers;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>When non-consent does not allow a permanent bad end, it becomes a fade to black: a rescue with a fixed debuff.</summary>
public sealed class BadEndRescueTests
{
    private static (TestContext Ctx, Character Bob, ModeParticipantState State) Scene(string nonConsent, bool pc = false)
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        var bob = TestContext.Adult("bob");
        bob.IsPc = pc;
        bob.SystemStats = new Dnd5eExtension { Willpower = 75 };
        bob.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 5, Max = 10 };
        var ctx = new TestContext(mode, TestContext.Adult("alice"), bob);
        ctx.Options[LewdKeys.NonConsentOption] = nonConsent;
        return (ctx, bob, mode.Participants.First(p => p.CharacterId == "bob"));
    }

    private static Task<ChangeHandlerResult> Defeat(TestContext ctx, string? outcome = null, bool keep = false) =>
        new LewdBadEndHandler().ApplyAsync(new LewdBadEndChange
        {
            TargetId = "bob", Reason = "defeat", NoEscape = true, Outcome = outcome, KeepBindings = keep,
        }, ctx);

    [Fact]
    public async Task The_verb_becomes_a_rescue_with_a_timed_debuff_and_no_permanent_mark()
    {
        var (ctx, bob, _) = Scene(LewdKeys.NonConsentOff);
        ctx.Time = new CampaignTime { TotalDaysElapsed = 4, Hour = 6 };
        var result = await Defeat(ctx, "found by a caravan at dawn");

        Assert.True(result.Success, result.Message);
        Assert.False(PregnancyState.Flag(bob, LewdKeys.BadEnded));
        Assert.DoesNotContain(bob.SystemStats.StatusEffects, BadEndState.IsBadEndEffect);
        var e = bob.SystemStats.StatusEffects.Single(x => x.EffectKey == BadEndRescue.Key);
        Assert.Equal(-2f, e.StatModifiers["AllSaves"]);
        Assert.Null(e.EffectTier);
        Assert.Equal(4.25f + 1f, e.ExpiresAtDay!.Value, 3);
        Assert.Equal(60f, bob.SystemStats.Willpower);
        Assert.Contains(ctx.Messages, m => m.Contains("found by a caravan at dawn") && m.Contains("not a permanent Bad End"));
        Assert.DoesNotContain(ctx.Published, p => p.Topic == LewdHandbook.Events.LewdEvents.BadEnd);
    }

    [Fact]
    public async Task A_permanent_consequence_is_refused_in_a_rescue()
    {
        var (ctx, bob, _) = Scene(LewdKeys.NonConsentOff);
        var result = await new LewdBadEndHandler().ApplyAsync(new LewdBadEndChange
        {
            TargetId = "bob", Reason = "defeat", NoEscape = true, Consequence = "slave",
        }, ctx);
        Assert.False(result.Success);
        Assert.Empty(bob.SystemStats.StatusEffects.Where(x => x.EffectKey == BadEndRescue.Key));
    }

    [Fact]
    public async Task Bindings_are_removed_unless_the_dm_keeps_them()
    {
        var (ctx, bob, state) = Scene(LewdKeys.NonConsentOff);
        BindingGraph.SetBindings(state, bob, [new BindingEntry { Id = "b1", Kind = "rope", Sites = ["wrists"] }]);
        await Defeat(ctx, keep: true);
        Assert.Single(BindingGraph.GetBindings(state, bob));

        bob.SystemStats.StatusEffects.Clear();
        bob.SystemStats.Traits.Remove("lewd_encounter.rescue.day");
        await Defeat(ctx);
        Assert.Empty(BindingGraph.GetBindings(state, bob));
    }

    [Fact]
    public async Task Overstim_six_rescues_resets_the_scene_and_does_not_re_fire()
    {
        var (ctx, bob, state) = Scene(LewdKeys.NonConsentOff);
        await LewdSettings.ResolveAsync(ctx); // a handler resolves settings before any rule trips
        LewdPoolHelper.SetOverstimulation(state, bob, 6, "test", ctx);
        Assert.True(BadEndRescue.IsPending(bob));
        Assert.Equal(0, ConsentGate.GetInt(state, LewdKeys.Overstimulation));

        var observer = new LewdBadEndObserver();
        await observer.OnCommittedAsync(new CharacterUpdate { CharacterId = "bob" }, ctx);
        Assert.False(BadEndRescue.IsPending(bob));
        Assert.Single(bob.SystemStats.StatusEffects, x => x.EffectKey == BadEndRescue.Key);
        var willpower = bob.SystemStats.Willpower;

        await observer.OnCommittedAsync(new CharacterUpdate { CharacterId = "bob" }, ctx);
        Assert.Equal(willpower, bob.SystemStats.Willpower);
        Assert.Single(bob.SystemStats.StatusEffects, x => x.EffectKey == BadEndRescue.Key);
        Assert.False(PregnancyState.Flag(bob, LewdKeys.BadEnded));
    }

    [Fact]
    public async Task Arousal_max_zero_is_repaired_by_the_rescue()
    {
        var (ctx, bob, _) = Scene(LewdKeys.NonConsentOff);
        bob.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 0, Max = 0 };
        var observer = new LewdBadEndObserver();
        await observer.OnCommittedAsync(new CharacterUpdate { CharacterId = "bob" }, ctx);
        Assert.True(bob.SystemStats.ResourcePools[LewdKeys.PoolArousal].Max >= 1);
        await observer.OnCommittedAsync(new CharacterUpdate { CharacterId = "bob" }, ctx);
        Assert.Single(bob.SystemStats.StatusEffects, x => x.EffectKey == BadEndRescue.Key);
    }

    [Fact]
    public async Task Once_per_day_one_incident_one_debuff()
    {
        var (ctx, bob, _) = Scene(LewdKeys.NonConsentOff);
        await Defeat(ctx);
        var willpower = bob.SystemStats.Willpower;
        await Defeat(ctx);
        Assert.Equal(willpower, bob.SystemStats.Willpower);
        Assert.Single(bob.SystemStats.StatusEffects, x => x.EffectKey == BadEndRescue.Key);
    }

    [Fact]
    public async Task The_debuff_runs_out()
    {
        var (ctx, bob, _) = Scene(LewdKeys.NonConsentOff);
        await Defeat(ctx);
        var e = bob.SystemStats.StatusEffects.Single(x => x.EffectKey == BadEndRescue.Key);
        Assert.NotNull(e.ExpiresAtDay);
        Assert.DoesNotContain(e.StatModifiers.Keys, k => k == "BlocksAllActions");
    }

    [Fact]
    public async Task Not_against_pc_rescues_the_pc_and_marks_the_npc_permanently()
    {
        var (ctx, pc, _) = Scene(LewdKeys.NonConsentNotAgainstPc, pc: true);
        Assert.True((await Defeat(ctx)).Success);
        Assert.False(PregnancyState.Flag(pc, LewdKeys.BadEnded));
        Assert.Contains(pc.SystemStats.StatusEffects, e => e.EffectKey == BadEndRescue.Key);

        var (npcCtx, npc, _) = Scene(LewdKeys.NonConsentNotAgainstPc, pc: false);
        Assert.True((await Defeat(npcCtx)).Success);
        Assert.True(PregnancyState.Flag(npc, LewdKeys.BadEnded));
        Assert.DoesNotContain(npc.SystemStats.StatusEffects, e => e.EffectKey == BadEndRescue.Key);
    }

    [Fact]
    public async Task With_non_consent_on_the_permanent_bad_end_stands()
    {
        var (ctx, bob, _) = Scene(LewdKeys.NonConsentOn);
        Assert.True((await Defeat(ctx)).Success);
        Assert.True(PregnancyState.Flag(bob, LewdKeys.BadEnded));
        Assert.DoesNotContain(bob.SystemStats.StatusEffects, e => e.EffectKey == BadEndRescue.Key);
    }
}
