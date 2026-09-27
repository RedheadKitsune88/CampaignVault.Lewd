using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using LewdHandbook.Observers;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>Rest, Recovery Dice, arousal maximum, the pregnancy clock and brand bookkeeping (items 1–3, 5).</summary>
public sealed class RestRecoveryTests
{
    private static Character Sheet(string id, int con = 14, int level = 1, string? die = "d8")
    {
        var c = TestContext.Adult(id);
        c.SystemStats = new Dnd5eExtension { Constitution = con, Level = level };
        if (die is not null)
            c.SystemStats.Traits[LewdKeys.TraitRecoveryDie] = die;
        return c;
    }

    [Theory]
    [InlineData(8, 2, 1, 10)]  // d8 max 8 + Con 2
    [InlineData(8, 2, 3, 24)]  // + 2 × (avg 5 + 2)
    [InlineData(6, -3, 2, 4)]  // each level adds at least 1
    public void Arousal_max_follows_recovery_die_con_and_level(int sides, int con, int level, int expected) =>
        Assert.Equal(expected, ArousalMath.Max(sides, con, level));

    [Fact]
    public void Arousal_max_falls_back_to_the_sexual_history_die_and_is_kept_without_one()
    {
        var kinkster = Sheet("a", con: 10, die: null);
        kinkster.SystemStats.Traits[LewdKeys.TraitSexualHistory] = "experienced_kinkster";
        Assert.Equal(10, LewdPoolHelper.Arousal(kinkster).Max);

        var unknown = Sheet("b", die: null);
        unknown.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 3, Max = 17 };
        Assert.Equal(17, LewdPoolHelper.Arousal(unknown).Max);
    }

    [Fact]
    public async Task Level_up_re_derives_the_max_through_the_sheet_observer()
    {
        var bob = Sheet("bob", con: 14, level: 1);
        bob.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 4, Max = 10 };
        var ctx = new TestContext(null, bob);

        ((Dnd5eExtension)bob.SystemStats).Level = 2;
        await new LewdSheetObserver().OnCommittedAsync(new LevelUpChange { CharacterId = "bob" }, ctx);

        Assert.Equal(17, bob.SystemStats.ResourcePools[LewdKeys.PoolArousal].Max); // 10 + (5 + 2)
    }

    [Fact]
    public async Task Rest_event_becomes_one_lewd_rest()
    {
        var e = DomainEvent.Create(CoreEvents.Rested, new { characterId = "chars/b", restType = "ShortRest" });

        var changes = await new LewdRestEventHandler().HandleAsync(e, new TestContext());

        var rest = Assert.IsType<LewdRestChange>(Assert.Single(changes));
        Assert.Equal("chars/b", rest.CharacterId);
        Assert.Equal("short", rest.RestType);
    }

    [Fact]
    public async Task Long_rest_halves_arousal_by_max_and_clears_numbing()
    {
        var bob = Sheet("bob"); // max 10
        bob.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 9, Max = 10 };
        bob.SystemStats.ResourcePools[LewdKeys.PoolNumbing] = new ResourcePool { Current = 4, Max = 0 };
        var ctx = new TestContext(null, bob);

        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "bob", RestType = "long" }, ctx);

        Assert.Equal(4, bob.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current);
        Assert.Equal(0, bob.SystemStats.ResourcePools[LewdKeys.PoolNumbing].Current);
    }

    [Fact]
    public async Task Rest_leaves_characters_without_lewd_pools_alone()
    {
        var plain = TestContext.Adult("plain");
        var ctx = new TestContext(null, plain);

        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "plain", RestType = "long" }, ctx);

        Assert.Empty(plain.SystemStats.ResourcePools);
    }

    [Fact]
    public async Task Recovery_dice_after_a_short_rest_roll_die_plus_con_each()
    {
        var bob = Sheet("bob", con: 14, level: 5); // prof +3, Con +2
        bob.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 30, Max = 38 };
        bob.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = new ResourcePool { Current = 5, Max = 5 };
        var ctx = new TestContext(null, bob);

        var refused = await new LewdRecoverHandler().ApplyAsync(new LewdRecoverChange { CharacterId = "bob", Dice = 1 }, ctx);
        Assert.False(refused.Success); // no climax or short rest yet

        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "bob", RestType = "short" }, ctx);
        var tooMany = await new LewdRecoverHandler().ApplyAsync(new LewdRecoverChange { CharacterId = "bob", Dice = 4 }, ctx);
        Assert.False(tooMany.Success);
        Assert.Contains("1..3", tooMany.Message);

        var spent = await new LewdRecoverHandler().ApplyAsync(
            new LewdRecoverChange { CharacterId = "bob", Dice = 2, Faces = [3, 6] }, ctx);

        Assert.True(spent.Success, spent.Message);
        Assert.Equal(17, bob.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current); // 30 − (3+2 + 6+2)
        Assert.Equal(3, bob.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice].Current);
        var again = await new LewdRecoverHandler().ApplyAsync(new LewdRecoverChange { CharacterId = "bob", Dice = 1 }, ctx);
        Assert.False(again.Success); // one spend per window
    }

    [Fact]
    public async Task Recovery_dice_after_a_climax_set_incapacitation_rounds_and_expire_at_the_next_turn()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["bob", "alice"]);
        mode.ModeId = LewdEncounterMode.ModeIdValue;
        var bobState = mode.Participants[0];
        var bob = Sheet("bob", con: 10, level: 5);
        bob.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = new ResourcePool { Current = 3, Max = 5 };
        var arousal = new ResourcePool { Current = 20, Max = 20 };
        bob.SystemStats.ResourcePools[LewdKeys.PoolArousal] = arousal;
        var ctx = new TestContext(mode, bob, TestContext.Adult("alice"));

        var note = LewdAdvanceHandler.ApplyClimaxResult(
            bobState, bob, arousal, new ClimaxSaveResult(ClimaxOutcomeKind.Climaxed, 0, 3, 0, false, "climax"), context: ctx);
        Assert.Contains("lewd_recover", note);

        var spent = await new LewdRecoverHandler().ApplyAsync(
            new LewdRecoverChange { CharacterId = "bob", Dice = 3, Faces = [1, 1, 1] }, ctx);
        Assert.True(spent.Success, spent.Message);
        Assert.Equal(3, ConsentGate.GetInt(bobState, LewdKeys.ClimaxIncapTurns));

        // Next climax: not spent before bob's next turn starts → the chance is gone.
        LewdAdvanceHandler.ApplyClimaxResult(
            bobState, bob, arousal, new ClimaxSaveResult(ClimaxOutcomeKind.Climaxed, 0, 3, 0, false, "climax"), context: ctx);
        await new LewdTurnStartHandler().ApplyAsync(new LewdTurnStartChange { CharacterId = "bob" }, ctx);
        var late = await new LewdRecoverHandler().ApplyAsync(new LewdRecoverChange { CharacterId = "bob", Dice = 1 }, ctx);
        Assert.False(late.Success);
    }

    [Fact]
    public void Brand_list_collapses_duplicates_to_the_highest_tier()
    {
        var brands = BrandState.Parse("ruin:1, denial:5, ruin:3");

        Assert.Equal(2, brands.Count);
        Assert.Equal(3, brands.Single(b => b.Id == "ruin").Tier);
    }

    [Fact]
    public void Ruin_without_dice_deals_1d12_and_would_be_lethal_damage_leaves_1_hp_and_raises_the_tier()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["bob"]);
        var bob = Sheet("bob");
        bob.SystemStats.Traits[LewdKeys.Lustbrands] = "ruin:1";
        bob.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = new ResourcePool { Current = 0, Max = 1 };
        bob.MaxHp = 10;
        bob.CurrentHp = 4;
        var arousal = new ResourcePool { Current = 10, Max = 10 };

        var note = LewdAdvanceHandler.ApplyClimaxResult(
            mode.Participants[0], bob, arousal, new ClimaxSaveResult(ClimaxOutcomeKind.Climaxed, 0, 3, 0, false, "climax"),
            ruin: new BrandState.RuinDice(RecoveryFace: 1, PsychicFace: 7));

        Assert.Contains("7 psychic", note);
        Assert.Equal(1, bob.CurrentHp);
        Assert.Equal(2, BrandState.Tier(bob, "ruin"));
        Assert.Equal(1, ConsentGate.GetInt(mode.Participants[0], LewdKeys.Overstimulation));
    }
}

/// <summary>Pregnancy on campaign time: showing, term, birth, the once-per-rest save and the timed poison.</summary>
public sealed class PregnancyClockTests
{
    private static (TestContext Ctx, Character Bob) Pregnant(string type = "traditional", int progress = 0)
    {
        var bob = TestContext.Adult("bob");
        bob.SystemStats = new Dnd5eExtension { Constitution = 10 };
        bob.SystemStats.Traits[LewdKeys.Pregnant] = "true";
        bob.SystemStats.Traits[LewdKeys.PregnancyType] = type;
        bob.SystemStats.Traits[LewdKeys.PregnancyProgress] = progress.ToString();
        bob.SystemStats.Traits[LewdKeys.PregnancySource] = "alice";
        return (new TestContext(null, bob, TestContext.Adult("alice")), bob);
    }

    [Fact]
    public void Traditional_pregnancy_shows_at_25_and_reaches_term_after_270_days()
    {
        var (ctx, bob) = Pregnant();
        PregnancyState.Sync(bob, 0, ctx);
        Assert.False(PregnancyState.IsVisible(bob));

        PregnancyState.Sync(bob, 24 * 70, ctx);
        Assert.Equal("25", bob.SystemStats.Traits[LewdKeys.PregnancyProgress]);
        Assert.Contains(bob.SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionPregnant);

        PregnancyState.Sync(bob, 24 * 270, ctx);
        Assert.Equal("100", bob.SystemStats.Traits[LewdKeys.PregnancyProgress]);
        Assert.Equal("true", bob.SystemStats.Traits[LewdKeys.PregnancyDue]);
        Assert.Contains(ctx.Nudges, n => n.Contains("action=birth"));
    }

    [Fact]
    public void A_pregnancy_from_before_the_clock_continues_from_its_recorded_progress()
    {
        var (ctx, bob) = Pregnant("nontraditional", progress: 50);

        PregnancyState.Sync(bob, 1000, ctx); // clock starts here, at 50
        PregnancyState.Sync(bob, 1000 + 36, ctx); // + half of a 3-day term

        Assert.Equal("100", bob.SystemStats.Traits[LewdKeys.PregnancyProgress]);
    }

    [Fact]
    public async Task Birth_clears_the_pregnancy_and_publishes()
    {
        var (ctx, bob) = Pregnant("nontraditional", progress: 100);

        var result = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange { TargetId = "bob", Action = "birth" }, ctx);

        Assert.True(result.Success, result.Message);
        Assert.Equal("false", bob.SystemStats.Traits[LewdKeys.Pregnant]);
        Assert.DoesNotContain(bob.SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionPregnant);
        Assert.False(bob.SystemStats.Attributes.ContainsKey(PregnancyState.StartHoursKey));
        Assert.Equal("birth", ctx.Field(LewdHandbook.Events.LewdEvents.Pregnancy, "state"));
    }

    [Fact]
    public async Task Rest_poison_rolls_its_hours_and_lifts_when_they_pass()
    {
        var (ctx, bob) = Pregnant("nontraditional");
        ctx.Rolls = new FixedRolls(2); // save 2 + 0 < 15; poisoned for 2 hours

        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "bob", RestType = "long" }, ctx);
        Assert.Contains(bob.SystemStats.StatusEffects, e => e.ConditionName == "poisoned");

        ctx.Time = new CampaignTime { Hour = ctx.Time.Hour + 3 };
        await new LewdPregnancyObserver().OnCommittedAsync(new ActivityChange { CharacterId = "bob", MinutesElapsed = 180 }, ctx);
        Assert.DoesNotContain(bob.SystemStats.StatusEffects, e => e.ConditionName == "poisoned");
    }

    [Fact]
    public async Task Advance_moves_the_clock_with_the_progress()
    {
        var (ctx, bob) = Pregnant(progress: 10);

        await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange { TargetId = "bob", Action = "advance", ProgressDelta = 40 }, ctx);
        PregnancyState.Sync(bob, ViceState.HoursNow(ctx.Time), ctx);

        Assert.Equal("50", bob.SystemStats.Traits[LewdKeys.PregnancyProgress]);
        Assert.Contains(bob.SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionPregnant);
    }
}
