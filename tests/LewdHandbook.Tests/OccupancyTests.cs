using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public sealed class OccupancyTests
{
    [Fact]
    public async Task Insert_RequiresInsertedToysSetting()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);

        var result = await new LewdInsertHandler().ApplyAsync(new LewdInsertChange
        {
            ActorId = "chars/a",
            TargetId = "chars/b",
            Orifice = "ass",
            Kind = "plug",
        }, ctx);

        Assert.False(result.Success);
        Assert.Contains("lewdInsertedToys", result.Message);
    }

    [Fact]
    public async Task Insert_Plug_SeatsAndSyncsStatus()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.InsertedToysOption] = LewdKeys.OptionOn;

        var result = await new LewdInsertHandler().ApplyAsync(new LewdInsertChange
        {
            ActorId = "chars/a",
            TargetId = "chars/b",
            Orifice = "ass",
            Kind = "plug",
            ItemId = "items/anal_plug",
        }, ctx);

        Assert.True(result.Success);
        var entry = Assert.Single(OccupancyGraph.Get(null, b));
        Assert.Equal("ass", entry.Orifice);
        Assert.Equal(LewdKeys.OccupancyPlug, entry.Kind);
        Assert.Equal(LewdKeys.SealPlugged, entry.Seal);
        Assert.Contains(b.SystemStats.StatusEffects, e => e.Name == "Plugged");
        Assert.Contains(b.SystemStats.StatusEffects, e => e.Name == Occupancy.SummaryName);
    }

    [Fact]
    public async Task InsideFinish_WithPlug_HoldsDeposit_NoImmediateSoil()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;
        ctx.Options[LewdKeys.InsertedToysOption] = LewdKeys.OptionOn;

        Assert.True((await new LewdInsertHandler().ApplyAsync(new LewdInsertChange
        {
            ActorId = "chars/a", TargetId = "chars/b", Orifice = "pussy", Kind = "plug",
        }, ctx)).Success);

        var changes = await new LewdSoilHandler().HandleAsync(
            DomainEvent.Create(LewdHandbook.Events.LewdEvents.Climax, new Dictionary<string, object?>
            {
                [LewdHandbook.Events.LewdEvents.Fields.CharacterId] = "chars/a",
                [LewdHandbook.Events.LewdEvents.Fields.Outcome] = "climax",
                [LewdHandbook.Events.LewdEvents.Fields.Finish] = LewdKeys.FinishInside,
                [LewdHandbook.Events.LewdEvents.Fields.TargetAnatomy] = "pussy",
                [LewdHandbook.Events.LewdEvents.Fields.DepositOnId] = "chars/b",
                [LewdHandbook.Events.LewdEvents.Fields.Physical] = true,
            }), ctx);

        Assert.DoesNotContain(changes, c => c is SoilChange);
        var deposit = Assert.Single(OccupancyGraph.GetDeposits(b));
        Assert.Equal("pussy", deposit.Orifice);
        Assert.Equal(2, deposit.Severity);
        Assert.Contains(b.SystemStats.StatusEffects, e => e.Name == Occupancy.FilledName);
    }

    [Fact]
    public async Task Remove_Plug_PublishesLeakThatBecomesSoil()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;
        ctx.Options[LewdKeys.InsertedToysOption] = LewdKeys.OptionOn;

        await new LewdInsertHandler().ApplyAsync(new LewdInsertChange
        {
            ActorId = "chars/a", TargetId = "chars/b", Orifice = "ass", Kind = "plug",
        }, ctx);
        var deposits = new List<InternalDeposit>();
        OccupancyGraph.AddDeposit(deposits, "ass", 3, "chars/a", 0);
        OccupancyGraph.SetDeposits(b, deposits);
        Occupancy.Sync(b);

        Assert.True((await new LewdRemoveHandler().ApplyAsync(new LewdRemoveChange
        {
            ActorId = "chars/a", TargetId = "chars/b", Orifice = "ass",
        }, ctx)).Success);

        var leakPub = Assert.Single(ctx.Published, p => p.Topic == LewdHandbook.Events.LewdEvents.Leak);
        var followUps = await new LewdLeakHandler().HandleAsync(DomainEvent.Create(leakPub.Topic, leakPub.Data), ctx);
        var soil = Assert.IsType<SoilChange>(Assert.Single(followUps));
        Assert.Equal(LewdKeys.DirtKindCum, soil.Kind);
        Assert.Equal("thighs", soil.Spot);
        Assert.True(soil.Amount >= 1);
    }

    [Fact]
    public async Task TravelHandler_LeaksOpenDeposit()
    {
        var b = TestContext.Adult("chars/b");
        var deposits = new List<InternalDeposit>();
        OccupancyGraph.AddDeposit(deposits, "ass", 2, "chars/a", 0);
        OccupancyGraph.SetDeposits(b, deposits);
        var ctx = new TestContext(null, b);
        ctx.Options[LewdKeys.FluidsOption] = LewdKeys.OptionOn;

        var soils = await new LewdOccupancyTravelHandler().HandleAsync(
            DomainEvent.Create(CoreEvents.Traveled, new { characterId = "chars/b" }), ctx);

        Assert.IsType<SoilChange>(Assert.Single(soils));
        Assert.Single(OccupancyGraph.GetDeposits(b)); // severity 2→1 remains
        Assert.Equal(1, OccupancyGraph.GetDeposits(b)[0].Severity);
    }

    [Fact]
    public async Task HardLimitPlugs_BlocksInsert()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        var ctx = new TestContext(null, a, b);
        ctx.Options[LewdKeys.InsertedToysOption] = LewdKeys.OptionOn;
        ctx.Options[LewdKeys.HardLimitsOption] = "plugs";

        var result = await new LewdInsertHandler().ApplyAsync(new LewdInsertChange
        {
            ActorId = "chars/a", TargetId = "chars/b", Orifice = "ass", Kind = "plug",
        }, ctx);

        Assert.False(result.Success);
    }

    [Fact]
    public void Settings_ParseInsertedToysAndLeaks()
    {
        var s = LewdSettings.From(new Dictionary<string, string>
        {
            [LewdKeys.InsertedToysOption] = "on",
            [LewdKeys.LeaksOption] = "off",
        });
        Assert.True(s.InsertedToys);
        Assert.False(s.Leaks);
    }
}
