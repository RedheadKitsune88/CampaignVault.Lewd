using CampaignVault.Events;
using CampaignVault.Models;
using GoblinPonyriders.Changes;
using GoblinPonyriders.Events;
using GoblinPonyriders.Handlers;
using GoblinPonyriders.Mechanics;
using Xunit;

namespace GoblinPonyriders.Tests;

public class GoblinHumiliationAndPiercingTests
{
    [Fact]
    public async Task Humiliated_severity_2_plus_raises_defiance_while_captured()
    {
        var who = TestContext.Adult("chars/captive");
        var ctx = new TestContext(who);
        CaptureState.Begin(who, ctx);
        var before = DefianceClock.Get(who);

        await new GoblinLewdEventHandler().HandleAsync(
            DomainEvent.Create(GoblinEvents.LewdTopics.Humiliated, new Dictionary<string, object?>
            {
                ["characterId"] = who.Id,
                ["severity"] = 2,
            }),
            ctx);

        Assert.Equal(before + 1, DefianceClock.Get(who));
    }

    [Fact]
    public async Task Humiliated_severity_1_does_not_raise_defiance()
    {
        var who = TestContext.Adult("chars/captive");
        var ctx = new TestContext(who);
        CaptureState.Begin(who, ctx);
        var before = DefianceClock.Get(who);

        await new GoblinLewdEventHandler().HandleAsync(
            DomainEvent.Create(GoblinEvents.LewdTopics.Humiliated, new Dictionary<string, object?>
            {
                ["characterId"] = who.Id,
                ["severity"] = 1,
            }),
            ctx);

        Assert.Equal(before, DefianceClock.Get(who));
    }

    [Fact]
    public async Task Terror_release_queues_heavy_nose_ring()
    {
        var who = TestContext.Adult("chars/captive");
        var ctx = new TestContext(who);
        CaptureState.Begin(who, ctx);

        await new GoblinStateHandler().ApplyAsync(new GoblinStateChange
        {
            CharacterId = who.Id,
            Action = "release",
            TerrorRelease = true,
        }, ctx);

        var followUps = await new GoblinPiercingHandler().HandleAsync(
            DomainEvent.Create(GoblinEvents.Release, new
            {
                characterId = who.Id,
                action = "terror_release",
            }),
            ctx);

        Assert.Single(followUps);
        var pierce = Assert.IsType<PiercingChange>(followUps[0]);
        Assert.Equal(PiercingSites.NoseSeptum, pierce.Site);
        Assert.Equal(GoblinPiercingKinds.HeavyNoseRing, pierce.Kind);
        Assert.Contains(PiercingTags.Locked, pierce.Tags!);
    }

    [Fact]
    public async Task Riding_girl_role_queues_septum_lead()
    {
        var who = TestContext.Adult("chars/captive");
        var ctx = new TestContext(who);
        CaptureState.Begin(who, ctx);

        await new GoblinStateHandler().ApplyAsync(new GoblinStateChange
        {
            CharacterId = who.Id,
            Action = "role",
            RoleId = "riding_girl",
        }, ctx);

        var pub = ctx.Published.Last(p => p.Topic == GoblinEvents.Role);
        var followUps = await new GoblinPiercingHandler().HandleAsync(
            DomainEvent.Create(pub.Topic, pub.Data),
            ctx);

        var pierce = Assert.IsType<PiercingChange>(Assert.Single(followUps));
        Assert.Equal(GoblinPiercingKinds.SeptumLead, pierce.Kind);
        Assert.Contains(PiercingTags.LeashRing, pierce.Tags!);
    }

    [Fact]
    public async Task Naming_queues_ear_notch_iron()
    {
        var who = TestContext.Adult("chars/captive");
        var ctx = new TestContext(who);

        await new GoblinStateHandler().ApplyAsync(new GoblinStateChange
        {
            CharacterId = who.Id,
            Action = "name",
            AssignedName = "Bell-Thigh",
        }, ctx);

        var pub = ctx.Published.Last(p => p.Topic == GoblinEvents.Name);
        var followUps = await new GoblinPiercingHandler().HandleAsync(
            DomainEvent.Create(pub.Topic, pub.Data),
            ctx);

        var pierce = Assert.IsType<PiercingChange>(Assert.Single(followUps));
        Assert.Equal(GoblinPiercingKinds.EarNotchIron, pierce.Kind);
    }
}
