using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public sealed class CleanupAndPendingTests
{
    [Fact]
    public void RememberPendingDeposit_WritesSheetTraits()
    {
        var actor = new ModeParticipantState { CharacterId = "chars/a" };
        var sheet = TestContext.Adult("chars/a");
        LewdAdvanceHandler.RememberPendingDeposit(actor, new LewdAdvanceChange
        {
            ActorId = "chars/a",
            TargetId = "chars/b",
            Finish = LewdKeys.FinishOutside,
            TargetAnatomy = "face",
        }, sheet);

        Assert.Equal(LewdKeys.FinishOutside, PregnancyState.Text(sheet, LewdKeys.TraitPendingFinish));
        Assert.Equal("face", PregnancyState.Text(sheet, LewdKeys.TraitPendingTargetAnatomy));
        Assert.Equal("chars/b", PregnancyState.Text(sheet, LewdKeys.TraitPendingDepositOn));
    }

    [Fact]
    public void ApplyClimax_ReadsPendingFromSheet_WhenScratchParticipant()
    {
        var sheet = TestContext.Adult("chars/a");
        sheet.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 10, Max = 10 };
        sheet.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = new ResourcePool { Current = 1, Max = 1 };
        sheet.SystemStats.Traits[LewdKeys.TraitPendingFinish] = LewdKeys.FinishOutside;
        sheet.SystemStats.Traits[LewdKeys.TraitPendingTargetAnatomy] = "chest";
        sheet.SystemStats.Traits[LewdKeys.TraitPendingDepositOn] = "chars/b";

        var scratch = new ModeParticipantState { CharacterId = "chars/a" };
        var ctx = new TestContext(null, sheet);
        var arousal = LewdPoolHelper.Arousal(sheet);

        LewdAdvanceHandler.ApplyClimaxResult(
            scratch, sheet, arousal,
            new ClimaxSaveResult(ClimaxOutcomeKind.InstantClimax, 0, 0, 0, false, "forced"),
            context: ctx, forced: true);

        Assert.Equal(LewdKeys.FinishOutside, ctx.Field(LewdHandbook.Events.LewdEvents.Climax, "finish"));
        Assert.Equal("chest", ctx.Field(LewdHandbook.Events.LewdEvents.Climax, "targetAnatomy"));
        Assert.Null(PregnancyState.Text(sheet, LewdKeys.TraitPendingFinish));
    }

    [Fact]
    public async Task Cleanup_ClearsDeposits_AndQueuesSoilClears()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var deposits = new List<InternalDeposit>();
        OccupancyGraph.AddDeposit(deposits, "ass", 2, "chars/a", 0);
        OccupancyGraph.SetDeposits(b, deposits);
        Occupancy.Sync(b);
        var ctx = new TestContext(null, a, b);

        var result = await new LewdCleanupHandler().ApplyAsync(new LewdCleanupChange
        {
            ActorId = "chars/a",
            TargetId = "chars/b",
        }, ctx);

        Assert.True(result.Success);
        Assert.Empty(OccupancyGraph.GetDeposits(b));
        Assert.DoesNotContain(b.SystemStats.StatusEffects, e => e.Name == Occupancy.FilledName);
        Assert.Contains(ctx.Published, p => p.Topic == LewdHandbook.Events.LewdEvents.Cleanup);

        var follow = await new LewdCleanupSoilHandler().HandleAsync(
            DomainEvent.Create(LewdHandbook.Events.LewdEvents.Cleanup, new
            {
                characterId = "chars/b",
                kinds = new[] { LewdKeys.DirtKindCum, LewdKeys.DirtKindFluids },
            }), ctx);
        Assert.Equal(2, follow.Count);
        Assert.All(follow, c => Assert.True(((SoilChange)c).Clear == true));
    }
}
