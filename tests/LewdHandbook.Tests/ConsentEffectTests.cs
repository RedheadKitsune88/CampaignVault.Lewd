using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>
/// The verbs that are not advances (brand, imprint, impregnation, forced climax, vice, stance) obey the same consent
/// rules: a flag on the change never lifts the player's policy, and lewdNonConsent=on is the grimdark switch.
/// </summary>
public class ConsentEffectTests
{
    private static (TestContext Ctx, Character Bob) Scene(string nonConsent, string? bobStance = LewdKeys.ConsentUnwilling)
    {
        var bob = TestContext.Adult("bob");
        if (bobStance is not null)
            bob.SystemStats.Traits[LewdKeys.TraitStance] = bobStance;
        var ctx = new TestContext(null, TestContext.Adult("alice"), bob);
        ctx.Options[LewdKeys.NonConsentOption] = nonConsent;
        return (ctx, bob);
    }

    [Fact]
    public async Task Brand_willing_flag_does_not_override_an_unwilling_stance()
    {
        var (ctx, _) = Scene(LewdKeys.NonConsentOff);
        var refused = await new LewdApplyBrandHandler().ApplyAsync(
            new LewdApplyBrandChange { TargetId = "bob", BrandId = "denial", Willing = true }, ctx);
        Assert.False(refused.Success);
        Assert.Contains("lewd_stance", refused.Message);

        var (grimdark, _) = Scene(LewdKeys.NonConsentOn);
        var applied = await new LewdApplyBrandHandler().ApplyAsync(
            new LewdApplyBrandChange { TargetId = "bob", BrandId = "denial" }, grimdark);
        Assert.True(applied.Success, applied.Message);
    }

    [Fact]
    public async Task Imprint_seeding_and_ticks_need_the_policy_for_an_unwilling_target()
    {
        var (ctx, _) = Scene(LewdKeys.NonConsentOff);
        var seeded = await new LewdImprintHandler().ApplyAsync(
            new LewdImprintChange { TargetId = "bob", Category = "training", SetLevel = 2, Willing = true }, ctx);
        Assert.False(seeded.Success);

        var ticked = await new LewdImprintHandler().ApplyAsync(
            new LewdImprintChange { TargetId = "bob", Category = "training", Source = "training", Willing = true, D20 = 10 }, ctx);
        Assert.False(ticked.Success);

        var (grimdark, _) = Scene(LewdKeys.NonConsentOn);
        var ok = await new LewdImprintHandler().ApplyAsync(
            new LewdImprintChange { TargetId = "bob", Category = "training", SetLevel = 2 }, grimdark);
        Assert.True(ok.Success, ok.Message);
    }

    [Fact]
    public async Task Impregnation_without_an_actor_still_asks_the_stance()
    {
        var (ctx, _) = Scene(LewdKeys.NonConsentOff);
        var refused = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange { TargetId = "bob", Force = true }, ctx);
        Assert.False(refused.Success);

        var (willing, _) = Scene(LewdKeys.NonConsentOff, bobStance: null);
        var ok = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange { TargetId = "bob", Force = true }, willing);
        Assert.True(ok.Success, ok.Message);
    }

    [Fact]
    public async Task Forced_climax_on_an_unwilling_target_is_refused_unless_non_consent_is_on()
    {
        var (ctx, _) = Scene(LewdKeys.NonConsentOff);
        var refused = await new LewdClimaxCheckHandler().ApplyAsync(
            new LewdClimaxCheckChange { TargetId = "bob", ForceClimax = true }, ctx);
        Assert.False(refused.Success);
        Assert.Contains("Forced climax refused", refused.Message);

        var (grimdark, _) = Scene(LewdKeys.NonConsentOn);
        var ok = await new LewdClimaxCheckHandler().ApplyAsync(
            new LewdClimaxCheckChange { TargetId = "bob", ForceClimax = true }, grimdark);
        Assert.True(ok.Success, ok.Message);
    }

    [Fact]
    public async Task Vice_consume_respects_the_players_hard_limits()
    {
        var (ctx, _) = Scene(LewdKeys.NonConsentOn, bobStance: null);
        ctx.Options[LewdKeys.HardLimitsOption] = "sexual_fluids";
        var refused = await new LewdViceHandler().ApplyAsync(
            new LewdViceChange { CharacterId = "bob", ViceId = "sexual_fluids", Ability = "con", D20 = 15 }, ctx);
        Assert.False(refused.Success);
        Assert.Contains("Hard limit", refused.Message);
    }

    [Fact]
    public async Task Loosening_the_players_character_needs_their_words_but_tightening_does_not()
    {
        var pc = TestContext.Adult("pc");
        pc.IsPc = true;
        pc.SystemStats.Traits[LewdKeys.TraitStance] = LewdKeys.ConsentUnwilling;
        pc.SystemStats.Traits[LewdKeys.TraitHardLimits] = "vore,pregnancy";
        var ctx = new TestContext(null, pc);
        var handler = new LewdStanceHandler();

        var loosen = await handler.ApplyAsync(new LewdStanceChange { CharacterId = "pc", Stance = "willing" }, ctx);
        Assert.False(loosen.Success);
        Assert.Contains("playerRequest", loosen.Message);

        var dropLimit = await handler.ApplyAsync(new LewdStanceChange { CharacterId = "pc", HardLimits = ["vore"] }, ctx);
        Assert.False(dropLimit.Success);

        var tighten = await handler.ApplyAsync(
            new LewdStanceChange { CharacterId = "pc", HardLimits = ["vore", "pregnancy", "bondage"] }, ctx);
        Assert.True(tighten.Success, tighten.Message);

        var quoted = await handler.ApplyAsync(
            new LewdStanceChange { CharacterId = "pc", Stance = "willing", PlayerRequest = "yes, go ahead" }, ctx);
        Assert.True(quoted.Success, quoted.Message);
        Assert.Contains("yes, go ahead", ctx.Messages.Last());

        var npc = TestContext.Adult("npc");
        ctx.Add(npc);
        var npcOk = await handler.ApplyAsync(new LewdStanceChange { CharacterId = "npc", Stance = "willing" }, ctx);
        Assert.True(npcOk.Success);
    }
}
