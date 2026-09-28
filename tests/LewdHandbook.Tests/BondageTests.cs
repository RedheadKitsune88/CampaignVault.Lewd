using CampaignVault.Events;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>Bindings as a restraint system: outside scenes, anchors, locks, escapes, the in-scene action cost.</summary>
public sealed class BondageTests
{
    private static (TestContext Ctx, Character Guard, Character Convict) Road()
    {
        var guard = TestContext.Adult("chars/guard");
        var convict = TestContext.Adult("chars/convict");
        convict.SystemStats = new Dnd5eExtension { Dexterity = 14, Strength = 10 };
        return (new TestContext(null, guard, convict), guard, convict);
    }

    private static void Grapple(Character c) =>
        c.SystemStats.StatusEffects.Add(new StatusEffect { Name = "grappled", ConditionName = "grappled", AppliedBy = "combat" });

    private static LewdBindChange Shackles(string anchor = "chars/guard") => new()
    {
        ActorId = "chars/guard",
        TargetId = "chars/convict",
        Kind = "shackles",
        Sites = ["ankles"],
        AnchorId = anchor,
        KeyItemId = "items/warden_key",
    };

    [Fact]
    public async Task Chain_gang_restraint_works_outside_any_scene_and_pins_travel_to_the_anchor()
    {
        var (ctx, _, convict) = Road();
        Grapple(convict);

        var result = await new LewdBindHandler().ApplyAsync(Shackles(), ctx);

        Assert.True(result.Success, result.Message);
        var effects = convict.SystemStats.StatusEffects.Select(e => e.Name).ToList();
        Assert.Contains("hobbled", effects);
        Assert.Contains("leashed", effects);
        Assert.Contains(Restraint.SummaryName, effects);
        var tether = Assert.Single(convict.SystemStats.Tethers);
        Assert.Equal("chars/guard", tether.AnchorId);
        Assert.Equal("chars/guard", tether.HolderId); // a character anchor holds their own end
        Assert.Empty(convict.SystemStats.EngagementRelations);
        Assert.Equal("bound", ctx.Field(LewdHandbook.Events.LewdEvents.BindingChanged, "action"));
    }

    [Fact]
    public async Task Plain_restraint_ignores_the_lewd_non_consent_setting_but_needs_a_subdued_or_willing_target()
    {
        var (ctx, _, convict) = Road(); // lewdNonConsent defaults to off

        var loose = await new LewdBindHandler().ApplyAsync(Shackles(), ctx);
        Assert.False(loose.Success);
        Assert.Contains("not subdued", loose.Message);

        var submits = Shackles();
        submits.Willing = true;
        Assert.True((await new LewdBindHandler().ApplyAsync(submits, ctx)).Success);
        _ = convict;
    }

    [Fact]
    public async Task Erotic_gear_outside_a_scene_still_follows_the_lewd_consent_rules()
    {
        var (ctx, _, convict) = Road();
        Grapple(convict);
        convict.SystemStats.Traits[LewdKeys.TraitStance] = "unwilling";

        var suit = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "chars/guard", TargetId = "chars/convict", Kind = "bitchsuit" }, ctx);

        Assert.False(suit.Success);
        Assert.Contains("lewdNonConsent=off", suit.Message);
    }

    [Fact]
    public async Task Player_hard_limits_still_apply_to_plain_restraint()
    {
        var (ctx, _, convict) = Road();
        Grapple(convict);
        ctx.Options[LewdKeys.HardLimitsOption] = "shackles";

        var result = await new LewdBindHandler().ApplyAsync(Shackles(), ctx);

        Assert.False(result.Success);
        Assert.Contains("hard limit", result.Message);
    }

    [Fact]
    public async Task A_locked_binding_needs_its_key_to_unbind_and_the_key_opens_it()
    {
        var (ctx, _, convict) = Road();
        Grapple(convict);
        await new LewdBindHandler().ApplyAsync(Shackles(), ctx);

        var noKey = await new LewdUnbindHandler().ApplyAsync(new LewdUnbindChange { TargetId = "chars/convict" }, ctx);
        Assert.False(noKey.Success);
        Assert.Contains("items/warden_key", noKey.Message);

        var keyed = await new LewdUnbindHandler().ApplyAsync(
            new LewdUnbindChange { TargetId = "chars/convict", KeyItemId = "items/warden_key" }, ctx);
        Assert.True(keyed.Success);
        Assert.Empty(convict.SystemStats.EngagementRelations);
        Assert.DoesNotContain(convict.SystemStats.StatusEffects, e => e.AppliedBy == Restraint.AppliedBy);
    }

    [Fact]
    public async Task Slip_rolls_dex_against_the_escape_dc()
    {
        var (ctx, _, convict) = Road();
        Grapple(convict);
        await new LewdBindHandler().ApplyAsync(Shackles(), ctx);

        var fail = await new LewdEscapeHandler().ApplyAsync(
            new LewdEscapeChange { CharacterId = "chars/convict", Method = "slip", D20 = 10 }, ctx);
        Assert.True(fail.Success);
        Assert.Single(BindingGraph.GetBindings(null, convict)); // 10 + 2 < 20

        var slip = await new LewdEscapeHandler().ApplyAsync(
            new LewdEscapeChange { CharacterId = "chars/convict", Method = "slip", D20 = 18 }, ctx);
        Assert.True(slip.Success);
        Assert.Empty(BindingGraph.GetBindings(null, convict));
        Assert.Empty(convict.SystemStats.EngagementRelations);
        Assert.Equal("escaped", ctx.Field(LewdHandbook.Events.LewdEvents.BindingChanged, "action"));
    }

    [Fact]
    public async Task Picking_needs_a_lock_and_free_hands_so_a_cuffed_prisoner_needs_help()
    {
        var (ctx, _, convict) = Road();
        Grapple(convict);
        await new LewdBindHandler().ApplyAsync(new LewdBindChange
        {
            ActorId = "chars/guard", TargetId = "chars/convict", Kind = "manacles", Orientation = "behind",
        }, ctx);
        var friend = TestContext.Adult("chars/friend");
        friend.SystemStats = new Dnd5eExtension { Dexterity = 16 };
        ctx.Add(friend);

        var self = await new LewdEscapeHandler().ApplyAsync(
            new LewdEscapeChange { CharacterId = "chars/convict", Method = "pick", D20 = 20 }, ctx);
        Assert.False(self.Success);
        Assert.Contains("hands are bound", self.Message);

        var helped = await new LewdEscapeHandler().ApplyAsync(
            new LewdEscapeChange { CharacterId = "chars/convict", ActorId = "chars/friend", Method = "pick", D20 = 10, Bonus = 2 }, ctx);
        Assert.True(helped.Success);
        Assert.Empty(BindingGraph.GetBindings(null, convict)); // 10 + 3 + 2 ≥ 15
    }

    [Fact]
    public async Task Cutting_wears_down_the_binding_hit_points()
    {
        var (ctx, _, convict) = Road();
        Grapple(convict);
        await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "chars/guard", TargetId = "chars/convict", Kind = "rope", Sites = ["wrists"], Hp = 8 }, ctx);

        await new LewdEscapeHandler().ApplyAsync(new LewdEscapeChange { CharacterId = "chars/convict", ActorId = "chars/guard", Method = "cut", Amount = 5 }, ctx);
        Assert.Equal(3, BindingGraph.GetBindings(null, convict).Single().Hp);
        await new LewdEscapeHandler().ApplyAsync(new LewdEscapeChange { CharacterId = "chars/convict", ActorId = "chars/guard", Method = "cut", Amount = 5 }, ctx);
        Assert.Empty(BindingGraph.GetBindings(null, convict));
    }

    [Fact]
    public async Task In_a_scene_binding_someone_is_the_actors_action_for_the_turn()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        mode.ModeId = LewdEncounterMode.ModeIdValue;
        var ctx = new TestContext(mode, TestContext.Adult("alice"), TestContext.Adult("bob"));

        var first = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Kind = "rope", Sites = ["wrists"] }, ctx);
        var second = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", Kind = "gag" }, ctx);

        Assert.True(first.Success, first.Message);
        Assert.False(second.Success);
        Assert.Contains("mode_transition action=turn", second.Message);
    }

    [Fact]
    public async Task Echoes_bearer_chained_to_someone_who_climaxes_is_forced_to_climax_too()
    {
        var a = TestContext.Adult("chars/a");
        var b = TestContext.Adult("chars/b");
        b.SystemStats.Traits[LewdKeys.Lustbrands] = "echoes:2";
        b.SystemStats.Traits[LewdKeys.TraitBindings] =
            "[{\"id\":\"c1\",\"kind\":\"chain\",\"anchorId\":\"chars/a\",\"sites\":[\"neck\"]}]";
        var far = TestContext.Adult("chars/far");
        far.SystemStats.Traits[LewdKeys.Lustbrands] = "echoes:2";
        far.SystemStats.SpatialPositions.Add(new SpatialPosition { TargetId = "chars/a", DistanceBand = SpatialDistanceBand.Far });
        var unknown = TestContext.Adult("chars/unknown");
        unknown.SystemStats.Traits[LewdKeys.Lustbrands] = "echoes:2";
        var ctx = new TestContext(null, a, b, far, unknown);

        var followUps = await new LewdEchoesHandler().HandleAsync(
            DomainEvent.Create(LewdHandbook.Events.LewdEvents.Climax, new { characterId = "chars/a", outcome = "climax" }), ctx);
        var check = Assert.IsType<LewdEchoCheckChange>(Assert.Single(followUps));
        Assert.Contains("chars/b", check.CandidateIds);
        Assert.DoesNotContain("chars/a", check.CandidateIds);

        b.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 5, Max = 10 };
        await new LewdEchoCheckHandler().ApplyAsync(check, ctx);

        Assert.Equal("climax", ctx.Field(LewdHandbook.Events.LewdEvents.Climax, "outcome"));
        Assert.Equal("chars/b", ctx.Field(LewdHandbook.Events.LewdEvents.Climax, "characterId"));
        Assert.Contains(ctx.Messages, m => m.Contains("chars/unknown"));
        Assert.DoesNotContain(ctx.Messages, m => m.Contains("chars/far"));
    }
}

public sealed class AgeTripwireTests
{
    [Theory]
    [InlineData("The Kid", null)]
    [InlineData("Mara", "a widow with two children")]
    [InlineData("Mara", "mother of her own kids, forty")]
    [InlineData("Mara", "wears kid gloves and a riding coat")]
    [InlineData("Mara", "looks one way, then another")]
    [InlineData("Mara", "seven feet tall, looks 7 feet")]
    [InlineData("Mara", "served 12 years in the guard")]
    [InlineData("Mara", "a widow with 3 kids")]
    [InlineData("Mara", "mother of 4 children")]
    [InlineData("Mara", "a T-shirt and a.k.a. mask")]
    public void Everyday_uses_do_not_trip(string name, string? appearance)
    {
        var c = new Character { Id = "chars/x", Name = name, CurrentAppearance = appearance, LifeStage = LifeStage.Adult };

        Assert.False(AgeGate.DescribesMinor(c, out var term), term);
    }

    [Theory]
    [InlineData("Pip", "a teen runaway")]
    [InlineData("Pip", "looks like a child")]
    [InlineData("Pip", "a 15-year-old squire")]
    [InlineData("Little Schoolgirl Pip", null)]
    [InlineData("Pip", "a 16yo squire")]
    [InlineData("Pip", "16 y/o")]
    [InlineData("Pip", "sixteen-year-old elf")]
    [InlineData("Pip", "just turned 17")]
    [InlineData("Pip", "aged twelve")]
    [InlineData("Pip", "sweet sixteen")]
    [InlineData("Pip", "under 18")]
    [InlineData("Pip", "looks 14")]
    [InlineData("Pip", "a l0li in a hat")]
    [InlineData("Pip", "t.e.e.n")]
    [InlineData("Pip", "a  15\u200B-year-old")]
    public void Minor_descriptions_still_trip(string name, string? appearance)
    {
        var c = new Character { Id = "chars/x", Name = name, CurrentAppearance = appearance, LifeStage = LifeStage.Adult };

        Assert.True(AgeGate.DescribesMinor(c, out _));
    }
}

public sealed class AgeNotesTests
{
    [Theory]
    [InlineData("Orphaned as a child, raised by monks.", false)]
    [InlineData("When she was a teen her village burned.", false)]
    [InlineData("She is a child.", true)]
    [InlineData("Aged 15, lives at the inn.", true)]
    public void Notes_allow_backstory_but_not_present_minors(string notes, bool trips)
    {
        var c = new Character { Id = "chars/x", Name = "Mara", Notes = notes, LifeStage = LifeStage.Adult };

        Assert.Equal(trips, AgeGate.DescribesMinor(c, out _));
    }
}
