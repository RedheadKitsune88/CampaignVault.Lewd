using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;

using Xunit;

namespace LewdHandbook.Tests;

public sealed class LewdDirtTests
{
    private static DomainEvent Climax(
        string characterId,
        string? finish,
        string? anatomy = null,
        string? depositOn = null,
        bool physical = true,
        string outcome = "climax") =>
        DomainEvent.Create(LewdHandbook.Events.LewdEvents.Climax, new Dictionary<string, object?>
        {
            [LewdHandbook.Events.LewdEvents.Fields.CharacterId] = characterId,
            [LewdHandbook.Events.LewdEvents.Fields.Outcome] = outcome,
            [LewdHandbook.Events.LewdEvents.Fields.Forced] = false,
            [LewdHandbook.Events.LewdEvents.Fields.InEncounter] = true,
            [LewdHandbook.Events.LewdEvents.Fields.Finish] = finish,
            [LewdHandbook.Events.LewdEvents.Fields.TargetAnatomy] = anatomy,
            [LewdHandbook.Events.LewdEvents.Fields.DepositOnId] = depositOn,
            [LewdHandbook.Events.LewdEvents.Fields.Physical] = physical,
        });

    [Fact]
    public async Task SoilHandler_OffByDefault_EmitsNothing()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);

        var changes = await new LewdSoilHandler().HandleAsync(
            Climax("chars/a", LewdKeys.FinishOutside, "face", "chars/b"), ctx);

        Assert.Empty(changes);
    }

    [Fact]
    public async Task SoilHandler_OutsideFace_SoilsDepositTarget()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;

        var changes = await new LewdSoilHandler().HandleAsync(
            Climax("chars/a", LewdKeys.FinishOutside, "face", "chars/b"), ctx);

        var soil = Assert.IsType<SoilChange>(Assert.Single(changes));
        Assert.Equal("chars/b", soil.TargetId);
        Assert.Equal(LewdKeys.DirtKindCum, soil.Kind);
        Assert.Equal(DirtSpots.Face, soil.Spot);
        Assert.Equal(2, soil.Amount);
        Assert.Equal("lewd_climax:chars/a", soil.AppliedBy);
    }

    [Fact]
    public async Task SoilHandler_Inside_OverflowThighs_AndCanPromptPregnancy()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;
        ctx.Options[LewdKeys.CreampiePregnancyOption] = LewdKeys.CreampiePregnancyPrompt;

        var changes = await new LewdSoilHandler().HandleAsync(
            Climax("chars/a", LewdKeys.FinishInside, "pussy", "chars/b"), ctx);

        var soil = Assert.IsType<SoilChange>(Assert.Single(changes));
        Assert.Equal("thighs", soil.Spot);
        Assert.Equal(2, soil.Amount);
        Assert.Contains(ctx.Messages, m => m.Contains("lewd_pregnancy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SoilHandler_Inside_AutoPregnancy_EnqueuesImpregnate()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;
        ctx.Options[LewdKeys.CreampiePregnancyOption] = LewdKeys.CreampiePregnancyAuto;

        var changes = await new LewdSoilHandler().HandleAsync(
            Climax("chars/a", LewdKeys.FinishInside, "pussy", "chars/b"), ctx);

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c is SoilChange);
        var preg = Assert.IsType<LewdPregnancyChange>(Assert.Single(changes, c => c is LewdPregnancyChange));
        Assert.Equal("chars/b", preg.TargetId);
        Assert.Equal("chars/a", preg.ActorId);
        Assert.Equal("impregnate", preg.Action);
    }

    [Fact]
    public async Task SoilHandler_HardLimitFluids_Skips()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;
        ctx.Options[LewdKeys.HardLimitsOption] = "fluids";

        var changes = await new LewdSoilHandler().HandleAsync(
            Climax("chars/a", LewdKeys.FinishOutside, "chest", "chars/b"), ctx);

        Assert.Empty(changes);
    }

    [Fact]
    public async Task SoilHandler_ExternalMarksOff_StillCanAutoPreg()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;
        ctx.Options[LewdKeys.ExternalMarksOption] = LewdKeys.OptionOff;
        ctx.Options[LewdKeys.CreampiePregnancyOption] = LewdKeys.CreampiePregnancyAuto;

        var changes = await new LewdSoilHandler().HandleAsync(
            Climax("chars/a", LewdKeys.FinishInside, "pussy", "chars/b"), ctx);

        Assert.DoesNotContain(changes, c => c is SoilChange);
        Assert.IsType<LewdPregnancyChange>(Assert.Single(changes));
    }

    [Fact]
    public async Task SoilHandler_NoFinish_Skips()
    {
        var a = TestContext.Adult("chars/a");
        var ctx = new TestContext(null, a);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;

        Assert.Empty(await new LewdSoilHandler().HandleAsync(Climax("chars/a", null), ctx));
        Assert.Empty(await new LewdSoilHandler().HandleAsync(Climax("chars/a", LewdKeys.FinishNone), ctx));
        Assert.Empty(await new LewdSoilHandler().HandleAsync(Climax("chars/a", LewdKeys.FinishOutside, outcome: "edging"), ctx));
    }

    [Fact]
    public void RememberPendingDeposit_StoresOnActor_ClearedSemanticsForNone()
    {
        var actor = new ModeParticipantState { CharacterId = "chars/a" };
        LewdAdvanceHandler.RememberPendingDeposit(actor, new LewdAdvanceChange
        {
            ActorId = "chars/a",
            TargetId = "chars/b",
            Finish = LewdKeys.FinishInside,
            TargetAnatomy = "pussy",
        });
        Assert.Equal(LewdKeys.FinishInside, ConsentGate.GetString(actor, LewdKeys.PendingFinish));
        Assert.Equal("pussy", ConsentGate.GetString(actor, LewdKeys.PendingTargetAnatomy));
        Assert.Equal("chars/b", ConsentGate.GetString(actor, LewdKeys.PendingDepositOn));

        LewdAdvanceHandler.RememberPendingDeposit(actor, new LewdAdvanceChange
        {
            ActorId = "chars/a",
            TargetId = "chars/b",
            Finish = LewdKeys.FinishNone,
        });
        Assert.Equal(LewdKeys.FinishNone, ConsentGate.GetString(actor, LewdKeys.PendingFinish));
        Assert.Null(ConsentGate.GetString(actor, LewdKeys.PendingTargetAnatomy));
        Assert.Null(ConsentGate.GetString(actor, LewdKeys.PendingDepositOn));
    }

    [Fact]
    public async Task ApplyClimax_PublishesFinishFromPending_AndClearsIt()
    {
        var a = TestContext.Adult("chars/a");
        a.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 10, Max = 10 };
        a.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = new ResourcePool { Current = 2, Max = 2 };
        var participant = new ModeParticipantState { CharacterId = "chars/a" };
        LewdAdvanceHandler.RememberPendingDeposit(participant, new LewdAdvanceChange
        {
            ActorId = "chars/a",
            TargetId = "chars/b",
            Finish = LewdKeys.FinishOutside,
            TargetAnatomy = "face",
        });
        var ctx = new TestContext(null, a);
        var arousal = LewdPoolHelper.Arousal(a);

        LewdAdvanceHandler.ApplyClimaxResult(
            participant,
            a,
            arousal,
            new ClimaxSaveResult(ClimaxOutcomeKind.InstantClimax, 0, 0, 0, false, "forced"),
            sourceId: null,
            context: ctx,
            forced: true);

        Assert.Equal(LewdKeys.FinishOutside, ctx.Field(LewdHandbook.Events.LewdEvents.Climax, LewdHandbook.Events.LewdEvents.Fields.Finish));
        Assert.Equal("face", ctx.Field(LewdHandbook.Events.LewdEvents.Climax, LewdHandbook.Events.LewdEvents.Fields.TargetAnatomy));
        Assert.Equal("chars/b", ctx.Field(LewdHandbook.Events.LewdEvents.Climax, LewdHandbook.Events.LewdEvents.Fields.DepositOnId));
        Assert.Null(ConsentGate.GetString(participant, LewdKeys.PendingFinish));
    }

    [Fact]
    public async Task FluidViceHook_NudgesOnLewdSoil()
    {
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;

        await new LewdFluidViceSoilHandler().HandleAsync(
            DomainEvent.Create(CoreEvents.Soiled, new Dictionary<string, object?>
            {
                [CoreEvents.Fields.TargetId] = "chars/b",
                [CoreEvents.Fields.Kind] = LewdKeys.DirtKindCum,
                [CoreEvents.Fields.Action] = "applied",
                [CoreEvents.Fields.Spot] = DirtSpots.Face,
                [CoreEvents.Fields.Severity] = 2,
            }),
            ctx);

        Assert.Contains(ctx.Messages, m => m.Contains("sexual_fluids", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ctx.Nudges, n => n.Contains("cum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Settings_ParseFluidsOptions()
    {
        var off = LewdSettings.From(new Dictionary<string, string>());
        Assert.False(off.Fluids);
        Assert.True(off.ExternalMarks);
        Assert.Equal(LewdCreampiePregnancy.Off, off.CreampiePregnancy);

        var on = LewdSettings.From(new Dictionary<string, string>
        {
            [LewdKeys.FluidsOption] = "on",
            [LewdKeys.ExternalMarksOption] = "off",
            [LewdKeys.FluidViceHookOption] = "off",
            [LewdKeys.CreampiePregnancyOption] = "auto",
        });
        Assert.True(on.Fluids);
        Assert.False(on.ExternalMarks);
        Assert.False(on.FluidViceHook);
        Assert.Equal(LewdCreampiePregnancy.Auto, on.CreampiePregnancy);
    }
}
