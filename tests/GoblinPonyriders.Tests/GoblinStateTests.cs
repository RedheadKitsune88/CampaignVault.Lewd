using CampaignVault.Models;
using GoblinPonyriders.Changes;
using GoblinPonyriders.Events;
using GoblinPonyriders.Handlers;
using GoblinPonyriders.Mechanics;
using Xunit;

namespace GoblinPonyriders.Tests;

public class GoblinStateTests
{
    [Fact]
    public async Task Capture_sets_defiance_and_capture_flag()
    {
        var who = TestContext.Adult("chars/captive");
        var ctx = new TestContext(who);
        var handler = new GoblinStateHandler();

        var result = await handler.ApplyAsync(new GoblinStateChange
        {
            CharacterId = who.Id,
            Action = "capture",
            AssignedName = "Bell-Thigh",
        }, ctx);

        Assert.True(result.Success);
        Assert.True(CaptureState.IsCaptured(who));
        Assert.Equal(7, DefianceClock.Get(who));
        Assert.Equal("Bell-Thigh", Traits.Get(who, GoblinKeys.AssignedName));
        Assert.Equal(TrainingPhases.RaidCamp, TrainingPhases.Get(who));
        Assert.Contains(ctx.Published, p => p.Topic == GoblinEvents.Capture);
    }

    [Fact]
    public async Task Disabled_option_blocks_capture()
    {
        var who = TestContext.Adult("chars/captive");
        var ctx = new TestContext(who);
        ctx.Options["goblinClans"] = "off";
        var handler = new GoblinStateHandler();

        var result = await handler.ApplyAsync(new GoblinStateChange
        {
            CharacterId = who.Id,
            Action = "capture",
        }, ctx);

        Assert.False(result.Success);
        Assert.False(CaptureState.IsCaptured(who));
    }

    [Fact]
    public async Task Role_assigns_catalog_and_bind_hint_message()
    {
        var who = TestContext.Adult("chars/captive");
        var ctx = new TestContext(who);
        CaptureState.Begin(who, ctx);
        var handler = new GoblinStateHandler();

        var result = await handler.ApplyAsync(new GoblinStateChange
        {
            CharacterId = who.Id,
            Action = "role",
            RoleId = "riding_girl",
        }, ctx);

        Assert.True(result.Success);
        Assert.Equal("riding_girl", Traits.Get(who, GoblinKeys.Role));
        Assert.Contains(ctx.Messages, m => m.Contains("lewd_bind", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Seed_faction_refreshes_when_loaded()
    {
        var ctx = new TestContext();
        ctx.Add(new Faction
        {
            Id = GoblinKeys.FactionId,
            Name = "Old Name",
            FactionType = FactionType.Guild,
            Description = "stale",
        });
        var handler = new GoblinStateHandler();

        var result = await handler.ApplyAsync(new GoblinStateChange { Action = "seed_faction" }, ctx);

        Assert.True(result.Success);
        Assert.Equal(GoblinKeys.FactionName, ctx.Factions[GoblinKeys.FactionId].Name);
        Assert.Equal(FactionType.MilitaryOrder, ctx.Factions[GoblinKeys.FactionId].FactionType);
        Assert.Equal("goblin_ponyriders", ctx.Factions[GoblinKeys.FactionId].Metadata["theme"]);
    }

    [Fact]
    public async Task Seed_faction_fails_with_world_build_hint_when_missing()
    {
        var ctx = new TestContext();
        var handler = new GoblinStateHandler();

        var result = await handler.ApplyAsync(new GoblinStateChange { Action = "seed_faction" }, ctx);

        Assert.False(result.Success);
        Assert.Contains("world_build", result.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Clan_mark_and_defiance_clamp()
    {
        var who = TestContext.Adult("chars/x");
        Assert.Equal(5, ClanMark.Set(who, 99));
        Assert.Equal(0, ClanMark.Set(who, -3));
        Assert.Equal(10, DefianceClock.Set(who, 99));
        Assert.Equal(0, DefianceClock.Set(who, -1));
    }

    [Fact]
    public void Training_advances_raid_to_village()
    {
        var who = TestContext.Adult("chars/x");
        var ctx = new TestContext(who);
        CaptureState.Begin(who, ctx);
        Assert.Equal(TrainingPhases.RaidCamp, TrainingPhases.Advance(who, ctx));
        TrainingPhases.Advance(who, ctx);
        Assert.Equal(TrainingPhases.Village, TrainingPhases.Advance(who, ctx));
    }
}
