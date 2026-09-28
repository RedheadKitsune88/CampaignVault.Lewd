using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Events;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>Turn start, scene end, stance, bindings on the character, and the review fixes.</summary>
public class SceneFlowTests
{
    private static (ModeEncounter Mode, ModeParticipantState Bob, Character BobChar, TestContext Ctx) Scene()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        var bobChar = TestContext.Adult("bob");
        bobChar.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 0, Max = 10 };
        var ctx = new TestContext(mode, TestContext.Adult("alice"), bobChar);
        return (mode, mode.Participants[1], bobChar, ctx);
    }

    [Fact]
    public async Task Mode_events_map_to_turn_start_and_scene_end_for_lewd_only()
    {
        var handler = new LewdModeEventHandler();
        var ctx = new TestContext();

        var turn = await handler.HandleAsync(
            DomainEvent.Create(CoreEvents.ModeTurnStarted, new { modeId = "lewd_encounter", characterId = "bob" }), ctx);
        var exit = await handler.HandleAsync(
            DomainEvent.Create(CoreEvents.ModeExited, new { modeId = "lewd_encounter", participantIds = new[] { "alice", "bob" } }), ctx);
        var other = await handler.HandleAsync(
            DomainEvent.Create(CoreEvents.ModeTurnStarted, new { modeId = "crafting", characterId = "bob" }), ctx);

        Assert.Equal("bob", Assert.IsType<LewdTurnStartChange>(Assert.Single(turn)).CharacterId);
        Assert.Equal(["alice", "bob"], Assert.IsType<LewdSceneEndChange>(Assert.Single(exit)).ParticipantIds);
        Assert.Empty(other);
    }

    [Fact]
    public async Task Turn_start_at_max_arousal_gains_edging_and_asks_for_save()
    {
        var (_, bob, bobChar, ctx) = Scene();
        bobChar.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current = 10;

        var result = await new LewdTurnStartHandler().ApplyAsync(new LewdTurnStartChange { CharacterId = "bob" }, ctx);

        Assert.True(result.Success);
        Assert.True(ConsentGate.GetBool(bob, LewdKeys.Edging));
        Assert.Equal(1, ConsentGate.GetInt(bob, LewdKeys.EdgingBeats));
        Assert.Contains(bobChar.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionEdging);
        Assert.Contains(ctx.Messages, m => m.Contains("lewd_climax_check"));
    }

    [Fact]
    public async Task Climax_incapacitation_lasts_through_next_turn_then_ends()
    {
        var (_, bob, bobChar, ctx) = Scene();
        var arousal = bobChar.SystemStats.ResourcePools[LewdKeys.PoolArousal];
        var climax = new ClimaxSaveResult(ClimaxOutcomeKind.InstantClimax, 0, 0, 10, false, "test");

        LewdAdvanceHandler.ApplyClimaxResult(bob, bobChar, arousal, climax, context: ctx);
        LewdAdvanceHandler.ApplyClimaxResult(bob, bobChar, arousal, climax, context: ctx);
        Assert.Contains(bobChar.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionStunned);

        var turn = new LewdTurnStartHandler();
        for (var i = 0; i < 2; i++)
        {
            await turn.ApplyAsync(new LewdTurnStartChange { CharacterId = "bob" }, ctx);
            Assert.True(ConsentGate.GetBool(bob, LewdKeys.ClimaxIncapacitated));
        }

        await turn.ApplyAsync(new LewdTurnStartChange { CharacterId = "bob" }, ctx);
        Assert.False(ConsentGate.GetBool(bob, LewdKeys.ClimaxIncapacitated));
        Assert.Equal(0, ConsentGate.GetInt(bob, LewdKeys.ClimaxStreak));
        Assert.DoesNotContain(bobChar.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionStunned);

        // A fresh climax after recovery is a first climax again: no stun.
        LewdAdvanceHandler.ApplyClimaxResult(bob, bobChar, arousal, climax, context: ctx);
        Assert.DoesNotContain(bobChar.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionStunned);
    }

    [Fact]
    public async Task Extended_edging_raises_overstimulation_on_the_sheet()
    {
        var (_, bob, bobChar, ctx) = Scene();
        bob.State[LewdKeys.Edging] = true;
        bob.State[LewdKeys.EdgingBeats] = OverstimMath.BeatsPerEdgingHour - 1;

        await new LewdTurnStartHandler().ApplyAsync(new LewdTurnStartChange { CharacterId = "bob" }, ctx);

        Assert.Equal(1, ConsentGate.GetInt(bob, LewdKeys.Overstimulation));
        Assert.Contains(bobChar.SystemStats.StatusEffects, e => e.Name == "Overstimulation 1");
        Assert.Contains(bobChar.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionIntoxicated);
    }

    [Fact]
    public async Task Binding_hardens_at_its_round()
    {
        var (mode, bob, bobChar, ctx) = Scene();
        await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Kind = "tar", Sites = ["torso"], HardenAtRound = 2 }, ctx);
        mode.Round = 2;

        await new LewdTurnStartHandler().ApplyAsync(new LewdTurnStartChange { CharacterId = "bob" }, ctx);

        var binding = Assert.Single(BindingGraph.GetBindings(bob, bobChar));
        Assert.True(binding.Hardened);
        Assert.Equal(25, binding.EscapeDc);
    }

    [Fact]
    public async Task Scene_end_clears_scene_conditions_but_bindings_and_their_conditions_stay()
    {
        var (mode, bob, bobChar, ctx) = Scene();
        await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Sites = ["mouth"], Implies = ["gagged"] }, ctx);
        var arousal = bobChar.SystemStats.ResourcePools[LewdKeys.PoolArousal];
        var climax = new ClimaxSaveResult(ClimaxOutcomeKind.InstantClimax, 0, 0, 10, true, "test");
        LewdAdvanceHandler.ApplyClimaxResult(bob, bobChar, arousal, climax, context: ctx);
        LewdAdvanceHandler.ApplyClimaxResult(bob, bobChar, arousal, climax, context: ctx);

        mode.IsActive = false;
        var result = await new LewdSceneEndHandler().ApplyAsync(new LewdSceneEndChange { ParticipantIds = ["alice", "bob"] }, ctx);

        Assert.True(result.Success);
        Assert.DoesNotContain(bobChar.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionStunned);
        Assert.DoesNotContain(bobChar.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionEdging);
        Assert.Contains(bobChar.SystemStats.StatusEffects, e => e.Name == "gagged");
        Assert.Single(BindingGraph.GetBindings(null, bobChar));
        Assert.Contains(ctx.Messages, m => m.Contains("still bound"));
    }

    [Fact]
    public async Task Unbind_outside_a_scene_frees_and_clears_bind_conditions()
    {
        var (mode, _, bobChar, ctx) = Scene();
        await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Sites = ["wrists"], Orientation = "behind", Effects = ["chafing"] }, ctx);
        var bound = Assert.Single(BindingGraph.GetBindings(null, bobChar));
        Assert.Contains("no_somatic_spellcasting", bound.Effects);
        Assert.Contains("chafing", bound.Effects);
        Assert.Contains(bobChar.SystemStats.StatusEffects, e => e.Name == "cuffed");
        Assert.Contains(bobChar.SystemStats.StatusEffects,
            e => e.Name == Restraint.SummaryName && e.StatModifiers.ContainsKey("BlocksSomaticComponents"));

        mode.IsActive = false;
        var result = await new LewdUnbindHandler().ApplyAsync(new LewdUnbindChange { TargetId = "bob", RemoveAll = true }, ctx);

        Assert.True(result.Success);
        Assert.Empty(BindingGraph.GetBindings(null, bobChar));
        Assert.False(bobChar.SystemStats.Traits.ContainsKey(LewdKeys.TraitBindings));
        Assert.DoesNotContain(bobChar.SystemStats.StatusEffects, e => e.Name == "cuffed");
        Assert.DoesNotContain(bobChar.SystemStats.StatusEffects, e => e.Name == Restraint.SummaryName);
    }

    [Fact]
    public async Task Unbind_leaves_restraint_conditions_from_other_sources()
    {
        var (_, _, bobChar, ctx) = Scene();
        bobChar.SystemStats.StatusEffects.Add(new StatusEffect { Name = "restrained", ConditionName = "restrained", AppliedBy = "web_spell" });
        await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Sites = ["torso"], Implies = ["restrained"] }, ctx);

        await new LewdUnbindHandler().ApplyAsync(new LewdUnbindChange { TargetId = "bob", RemoveAll = true }, ctx);

        Assert.Contains(bobChar.SystemStats.StatusEffects, e => e.Name == "restrained" && e.AppliedBy == "web_spell");
    }

    [Fact]
    public async Task Bind_respects_stance_and_non_consent_setting()
    {
        var (_, bob, bobChar, ctx) = Scene();
        bob.State[LewdKeys.Consent] = LewdKeys.ConsentUnwilling;

        var refused = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Sites = ["wrists"] }, ctx);
        Assert.False(refused.Success);
        Assert.Empty(BindingGraph.GetBindings(bob, bobChar));

        ctx.Options[LewdKeys.NonConsentOption] = LewdKeys.NonConsentOn;
        var loose = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Sites = ["wrists"] }, ctx);
        Assert.False(loose.Success); // allowed by the settings, but an unwilling target has to be subdued first
        Assert.Contains("not subdued", loose.Message);

        bobChar.SystemStats.StatusEffects.Add(new StatusEffect { Name = "grappled", ConditionName = "grappled", AppliedBy = "combat" });
        var allowed = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Sites = ["wrists"] }, ctx);
        Assert.True(allowed.Success, allowed.Message);
    }

    [Fact]
    public async Task Stance_scene_and_default_scopes()
    {
        var (_, bob, bobChar, ctx) = Scene();
        var handler = new LewdStanceHandler();

        var scene = await handler.ApplyAsync(
            new LewdStanceChange { CharacterId = "bob", Stance = "selective", AllowedPartners = ["alice"] }, ctx);
        var lasting = await handler.ApplyAsync(
            new LewdStanceChange { CharacterId = "bob", Scope = "default", Kinks = ["rope"], Inhibition = 2 }, ctx);

        Assert.True(scene.Success);
        Assert.True(lasting.Success);
        Assert.Equal("selective", bob.State[LewdKeys.Consent]);
        Assert.Equal("rope", bobChar.SystemStats.Traits[LewdKeys.TraitKinks]);
        Assert.True(LewdProfile.Wants(bob, bobChar, "alice"));
        Assert.False(LewdProfile.Wants(bob, bobChar, "eve"));
        Assert.Equal(2, LewdProfile.Inhibition(null, bobChar));
    }

    [Fact]
    public async Task Revoked_stance_stops_every_advance()
    {
        var (_, _, _, ctx) = Scene();
        ctx.Options[LewdKeys.NonConsentOption] = LewdKeys.NonConsentOn;
        await new LewdStanceHandler().ApplyAsync(new LewdStanceChange { CharacterId = "bob", Stance = "revoked" }, ctx);

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange { ActorId = "alice", TargetId = "bob", Hit = true, StimulationAmount = 3 }, ctx);

        Assert.False(result.Success);
        Assert.Contains("revoked", result.Message);
        Assert.Contains(ctx.Nudges, n => n.Contains("Stop"));
    }

    [Fact]
    public async Task Stance_refuses_a_child()
    {
        var ctx = new TestContext(null, new Character { Id = "pip", Name = "Pip", LifeStage = LifeStage.Child });

        var result = await new LewdStanceHandler().ApplyAsync(new LewdStanceChange { CharacterId = "pip", Stance = "willing" }, ctx);

        Assert.False(result.Success);
        Assert.Contains("never involve minors", result.Message);
    }

    [Fact]
    public async Task Climax_check_requires_the_edge_and_labels_outcomes()
    {
        var (_, bob, bobChar, ctx) = Scene();
        var handler = new LewdClimaxCheckHandler();

        var early = await handler.ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", D20 = 10 }, ctx);
        Assert.False(early.Success);

        bobChar.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current = 10;
        var held = await handler.ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", D20 = 20 }, ctx);
        Assert.True(held.Success);
        Assert.Equal("held", ctx.Field(LewdEvents.Climax, "outcome"));

        bobChar.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current = 10;
        await handler.ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", D20 = 5, InhibitionBonus = 0 }, ctx);
        Assert.Equal("edging", ctx.Field(LewdEvents.Climax, "outcome"));
        Assert.Contains(ctx.Published, p => p.Topic == LewdEvents.Climax);
        _ = bob;
    }

    [Fact]
    public async Task Climax_tally_lives_on_the_sheet_until_the_edge_is_released()
    {
        // No scene at all: every call gets a fresh scratch participant, so only the sheet can carry the tally.
        var bob = TestContext.Adult("bob");
        bob.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 10, Max = 10 };
        var ctx = new TestContext(null, bob);
        var handler = new LewdClimaxCheckHandler();

        await handler.ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", D20 = 5, InhibitionBonus = 0 }, ctx);
        await handler.ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", D20 = 5, InhibitionBonus = 0 }, ctx);
        Assert.Equal((0, 2), LewdPoolHelper.ClimaxCounters(new ModeParticipantState { CharacterId = "bob" }, bob));

        await handler.ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", D20 = 20 }, ctx);
        Assert.Equal((0, 0), LewdPoolHelper.ClimaxCounters(new ModeParticipantState { CharacterId = "bob" }, bob));
        Assert.DoesNotContain(bob.SystemStats.Attributes.Keys, k => k.StartsWith("lewd.climax_"));
    }

    [Fact]
    public async Task Denial_intercept_publishes_denied_not_climax()
    {
        var (_, _, bobChar, ctx) = Scene();
        await new LewdApplyBrandHandler().ApplyAsync(
            new LewdApplyBrandChange { TargetId = "bob", BrandId = "denial", Willing = true }, ctx);

        await new LewdClimaxCheckHandler().ApplyAsync(new LewdClimaxCheckChange { TargetId = "bob", ForceClimax = true }, ctx);

        Assert.Equal("denied", ctx.Field(LewdEvents.Climax, "outcome"));
        _ = bobChar;
    }

    [Fact]
    public async Task Brand_removal_is_not_blocked_by_a_lustbrand_hard_limit()
    {
        var (_, bob, bobChar, ctx) = Scene();
        await new LewdApplyBrandHandler().ApplyAsync(
            new LewdApplyBrandChange { TargetId = "bob", BrandId = "ruin", Willing = true }, ctx);
        bob.State[LewdKeys.HardLimits] = new List<string> { "lustbrand" };

        var result = await new LewdApplyBrandHandler().ApplyAsync(
            new LewdApplyBrandChange { TargetId = "bob", BrandId = "ruin", Action = "remove", Method = "wish" }, ctx);

        Assert.True(result.Success);
        Assert.False(BrandState.Has(bobChar, BrandCatalog.Ruin));
    }

    [Fact]
    public async Task Decondition_publishes_imprint_changed()
    {
        var character = TestContext.Adult("bob");
        character.SystemStats.Traits[LewdKeys.TraitImprints] = "wanton:4:1:willing:0";
        var ctx = new TestContext(null, character);

        var result = await new LewdDeconditionHandler().ApplyAsync(
            new LewdDeconditionChange { TargetId = "bob", Category = "wanton", Method = "therapy", D20 = 20, WisMod = 0 }, ctx);

        Assert.True(result.Success);
        Assert.Equal("decondition", ctx.Field(LewdEvents.ImprintChanged, "action"));
    }

    [Fact]
    public async Task Imprint_set_level_seeds_backstory_without_a_save()
    {
        var character = TestContext.Adult("mira");
        var ctx = new TestContext(null, character) { Options = { [LewdKeys.NonConsentOption] = LewdKeys.NonConsentOn } };

        var result = await new LewdImprintHandler().ApplyAsync(
            new LewdImprintChange { TargetId = "mira", Category = "ordeal", Source = "bad_end", SetLevel = 3 }, ctx);

        Assert.True(result.Success);
        Assert.Equal(3, ImprintState.Level(character, "ordeal"));
        Assert.Contains("ordeal:12:3:unwilling", character.SystemStats.Traits[LewdKeys.TraitImprints]);
    }

    [Fact]
    public void Imprint_write_keeps_vice_intrusive_thoughts()
    {
        var character = TestContext.Adult("bob");
        ViceState.AppendThought(character, "sex");

        ImprintState.Tick(null, character, "ordeal", willing: false, delta: 3, day: 0, convert: false, context: null);

        var thoughts = character.SystemStats.Traits[LewdKeys.TraitIntrusiveThoughts];
        Assert.Contains("vice:sex", thoughts);
        Assert.Contains("imprint:ordeal", thoughts);
    }

    [Fact]
    public void Vice_does_not_claim_or_strip_a_brand_owned_condition()
    {
        var character = TestContext.Adult("bob");
        character.SystemStats.StatusEffects.Add(new StatusEffect
        {
            Name = LewdKeys.ConditionDenied, ConditionName = LewdKeys.ConditionDenied, AppliedBy = BrandCatalog.AppliedBy,
        });
        ViceCatalog.TryGet(ViceCatalog.SuccubusVenom, out var venom);

        ViceState.FailWithdrawal(character, null, venom, d20: 3, context: null);
        ViceState.ClearAddicted(character, venom);

        var denied = Assert.Single(character.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionDenied);
        Assert.Equal(BrandCatalog.AppliedBy, denied.AppliedBy);
    }

    [Fact]
    public void Brand_addiction_starts_the_withdrawal_clock()
    {
        var character = TestContext.Adult("bob");
        ViceState.LockSexualFluids(character, null);
        ViceCatalog.TryGet(ViceCatalog.SexualFluids, out var fluids);

        ViceState.SyncWithdrawal(character, fluids, nowHours: 100, context: null);
        Assert.False(ViceState.IsWithdrawal(character, fluids.Id));

        ViceState.SyncWithdrawal(character, fluids, nowHours: 125, context: null);
        Assert.True(ViceState.IsWithdrawal(character, fluids.Id));
    }

    [Fact]
    public void Drinking_clears_alcohol_withdrawal_intoxication()
    {
        var character = TestContext.Adult("bob");
        ViceCatalog.TryGet(ViceCatalog.Alcohol, out var alcohol);
        ViceState.Consume(character, null, alcohol, nowHours: 0, "con", becameAddicted: true, null, null);
        ViceState.SyncWithdrawal(character, alcohol, nowHours: 5, context: null);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionIntoxicated);

        ViceState.Consume(character, null, alcohol, nowHours: 6, "con", becameAddicted: false, null, null);

        Assert.DoesNotContain(character.SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionIntoxicated);
    }

    [Theory]
    [InlineData(null, "Off")]
    [InlineData("grimdark", "Off")]
    [InlineData("not-against-pc", "NotAgainstPc")]
    [InlineData("ON", "On")]
    public void Unknown_non_consent_values_fail_closed(string? raw, string expected) =>
        Assert.Equal(expected, LewdSettings.ParseNonConsent(raw).ToString());
}
