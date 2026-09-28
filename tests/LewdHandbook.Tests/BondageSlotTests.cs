using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>Body slots: effects come from which slots are occupied, DCs from material and quality, anchors are core tethers.</summary>
public sealed class BondageSlotTests
{
    private static (TestContext Ctx, Character Guard, Character Convict) Road()
    {
        var guard = TestContext.Adult("chars/guard");
        var convict = TestContext.Adult("chars/convict");
        convict.SystemStats = new Dnd5eExtension { Dexterity = 14 };
        convict.SystemStats.StatusEffects.Add(new StatusEffect { Name = "grappled", ConditionName = "grappled", AppliedBy = "combat" });
        return (new TestContext(null, guard, convict), guard, convict);
    }

    private static Task<CampaignVault.Data.ChangeHandlers.ChangeHandlerResult> Bind(TestContext ctx, LewdBindChange change)
    {
        change.ActorId = "chars/guard";
        change.TargetId = "chars/convict";
        return new LewdBindHandler().ApplyAsync(change, ctx);
    }

    private static List<string> Names(Character c) => c.SystemStats.StatusEffects.Select(e => e.Name).ToList();

    [Theory]
    [InlineData("hands", "wrists")]
    [InlineData("feet", "ankles")]
    [InlineData("throat", "neck")]
    [InlineData("jaw", "mouth")]
    public void Site_aliases_land_on_slots(string site, string slot) =>
        Assert.Equal([slot], BondageSlots.SlotsOf(site));

    [Fact]
    public async Task Wrists_alone_in_front_leave_the_hands_usable_but_behind_they_are_not()
    {
        var (ctx, _, front) = Road();
        Assert.True((await Bind(ctx, new LewdBindChange { Kind = "rope", Sites = ["wrists"], Orientation = "front", Materials = ["rope"] })).Success);
        Assert.False(Restraint.BlocksSomaticComponents(BindingGraph.GetBindings(null, front)));

        var (ctx2, _, behind) = Road();
        Assert.True((await Bind(ctx2, new LewdBindChange { Kind = "rope", Sites = ["wrists"], Orientation = "behind" })).Success);
        Assert.True(Restraint.BlocksSomaticComponents(BindingGraph.GetBindings(null, behind)));
    }

    [Fact]
    public async Task Slots_from_different_ties_add_up_to_effects_no_single_tie_names()
    {
        var (ctx, _, c) = Road();
        // No `implies` anywhere: a wrist tie in front, then an elbow tie, then a ring gag and a blindfold.
        await Bind(ctx, new LewdBindChange { Kind = "rope", Sites = ["wrists"], Orientation = "front" });
        Assert.DoesNotContain("limb_bound", Names(c));

        await Bind(ctx, new LewdBindChange { Kind = "strap", Sites = ["elbows"], Implies = ["cuffed"] });
        Assert.Contains("limb_bound", Names(c));
        Assert.True(Restraint.BlocksSomaticComponents(BindingGraph.GetBindings(null, c)));

        await Bind(ctx, new LewdBindChange { Kind = "ring gag", Sites = ["mouth"] });
        await Bind(ctx, new LewdBindChange { Kind = "sash", Sites = ["eyes"] });
        Assert.Contains("gagged", Names(c));
        Assert.Contains("blinded", Names(c));
        Assert.True(Restraint.BlocksVerbalComponents(BindingGraph.GetBindings(null, c)));
    }

    [Fact]
    public async Task Removing_the_tie_that_occupied_a_slot_removes_what_it_derived()
    {
        var (ctx, _, c) = Road();
        await Bind(ctx, new LewdBindChange { Kind = "ring gag", Sites = ["mouth"] });
        Assert.Contains("gagged", Names(c));

        var unbind = await new LewdUnbindHandler().ApplyAsync(
            new LewdUnbindChange { ActorId = "chars/guard", TargetId = "chars/convict", RemoveAll = true }, ctx);

        Assert.True(unbind.Success, unbind.Message);
        Assert.DoesNotContain("gagged", Names(c));
    }

    [Fact]
    public async Task Wrists_ankles_and_the_hogtie_posture_make_a_hogtie()
    {
        var (ctx, _, c) = Road();
        await Bind(ctx, new LewdBindChange { Kind = "rope", Sites = ["wrists"], Orientation = "behind" });
        await Bind(ctx, new LewdBindChange { Kind = "rope", Sites = ["ankles"], Posture = "hogtie" });

        Assert.Contains("full_tied", Names(c));
        Assert.Contains("prone", Names(c));
    }

    [Fact]
    public async Task The_dm_gets_one_line_naming_the_slots_and_their_effects()
    {
        var (ctx, _, c) = Road();
        await Bind(ctx, new LewdBindChange { Kind = "armbinder", Sites = ["arms", "wrists"], Orientation = "behind" });
        await Bind(ctx, new LewdBindChange { Kind = "ring gag", Sites = ["mouth"] });

        var line = Assert.Single(ctx.Nudges.TakeLast(1));
        Assert.Contains("Slots [wrists behind, arms behind, mouth]", line);
        Assert.Contains("hands unusable", line);
        Assert.Contains("no verbal components", line);
        var hint = c.SystemStats.StatusEffects.Single(e => e.Name == Restraint.SummaryName).RecoveryHint;
        Assert.Contains("Slots [", hint);
    }

    [Theory]
    [InlineData("rope", null, 15, 13)]
    [InlineData("iron", null, 20, 22)]
    [InlineData("rope", "fine", 17, 15)]
    [InlineData("rope", "crude", 11, 9)]
    [InlineData("iron", "masterwork", 24, 26)]
    public async Task Material_sets_the_base_dcs_and_quality_shifts_them(string material, string? quality, int escape, int brk)
    {
        var (ctx, _, c) = Road();
        Assert.True((await Bind(ctx, new LewdBindChange { Kind = "tie", Sites = ["wrists"], Materials = [material], Quality = quality })).Success);

        var entry = Assert.Single(BindingGraph.GetBindings(null, c));
        Assert.Equal(escape, entry.EscapeDc);
        Assert.Equal(brk, entry.BreakDc);
    }

    [Fact]
    public async Task Explicit_dcs_beat_the_material_and_an_unknown_quality_is_refused()
    {
        var (ctx, _, c) = Road();
        await Bind(ctx, new LewdBindChange { Kind = "tie", Sites = ["wrists"], Materials = ["rope"], EscapeDc = 24 });
        Assert.Equal(24, Assert.Single(BindingGraph.GetBindings(null, c)).EscapeDc);

        var bad = await Bind(ctx, new LewdBindChange { Kind = "tie", Sites = ["ankles"], Quality = "shiny" });
        Assert.False(bad.Success);
        Assert.Contains("quality must be", bad.Message);
    }

    [Fact]
    public async Task An_item_anchor_can_be_held_by_a_character_and_becomes_a_core_tether()
    {
        var (ctx, _, c) = Road();
        var result = await Bind(ctx, new LewdBindChange
        {
            Kind = "rope", Sites = ["neck"], AnchorId = "items/saddle_horn", HolderId = "chars/guard", SlackFeet = 10,
        });

        Assert.True(result.Success, result.Message);
        var tether = Assert.Single(c.SystemStats.Tethers);
        Assert.Equal("items/saddle_horn", tether.AnchorId);
        Assert.Equal("chars/guard", tether.HolderId);
        Assert.Equal(10, tether.SlackFeet);
        Assert.Contains("leashed", Names(c));
    }

    [Fact]
    public async Task A_tether_core_released_is_dropped_from_the_binding_on_the_next_sync()
    {
        var (ctx, _, c) = Road();
        await Bind(ctx, new LewdBindChange { Kind = "rope", Sites = ["neck"], AnchorId = "chars/guard" });
        Assert.Contains("leashed", Names(c));

        c.SystemStats.Tethers.Clear(); // strained free, or the holder went down
        Restraint.Sync(c, BindingGraph.GetBindings(null, c));

        Assert.Empty(c.SystemStats.Tethers);
        Assert.Null(Assert.Single(BindingGraph.GetBindings(null, c)).AnchorId);
        Assert.DoesNotContain("leashed", Names(c));
    }

    [Fact]
    public async Task Unbinding_removes_only_our_tether_and_leaves_others()
    {
        var (ctx, _, c) = Road();
        c.SystemStats.Tethers.Add(new Tether { AnchorId = "fixture:post", AttachedBy = "someone" });
        await Bind(ctx, new LewdBindChange { Kind = "rope", Sites = ["neck"], AnchorId = "chars/guard" });
        Assert.Equal(2, c.SystemStats.Tethers.Count);

        await new LewdUnbindHandler().ApplyAsync(
            new LewdUnbindChange { ActorId = "chars/guard", TargetId = "chars/convict", RemoveAll = true }, ctx);

        Assert.Equal("fixture:post", Assert.Single(c.SystemStats.Tethers).AnchorId);
    }

    [Fact]
    public async Task A_fifth_tether_is_refused()
    {
        var (ctx, _, c) = Road();
        for (var i = 0; i < 4; i++)
            c.SystemStats.Tethers.Add(new Tether { AnchorId = $"fixture:p{i}", AttachedBy = "someone" });

        var result = await Bind(ctx, new LewdBindChange { Kind = "rope", Sites = ["neck"], AnchorId = "chars/guard" });

        Assert.False(result.Success);
        Assert.Contains("four tethers", result.Message);
    }

    [Fact]
    public async Task A_minor_holder_is_refused_by_the_age_gate()
    {
        var (ctx, _, _) = Road();
        var kid = TestContext.Adult("chars/kid");
        kid.LifeStage = LifeStage.Child;
        ctx.Add(kid);

        var result = await Bind(ctx, new LewdBindChange { Kind = "rope", Sites = ["neck"], AnchorId = "items/post", HolderId = "chars/kid" });

        Assert.False(result.Success);
    }
}

/// <summary>Slack, attachment points and fit: the few deterministic knobs, and the free text the DM interprets.</summary>
public sealed class BondageSlackTests
{
    private static async Task<(TestContext Ctx, Character C)> Bind(LewdBindChange change)
    {
        var guard = TestContext.Adult("chars/guard");
        var c = TestContext.Adult("chars/convict");
        c.SystemStats = new Dnd5eExtension { Dexterity = 14, Movement = 30 };
        c.SystemStats.StatusEffects.Add(new StatusEffect { Name = "grappled", ConditionName = "grappled", AppliedBy = "combat" });
        var ctx = new TestContext(null, guard, c);
        change.ActorId = "chars/guard";
        change.TargetId = "chars/convict";
        var r = await new LewdBindHandler().ApplyAsync(change, ctx);
        Assert.True(r.Success, r.Message);
        return (ctx, c);
    }

    private static IEnumerable<RollModifier> Ask(Character c, string kind, string? subject = null) =>
        new LewdRollModifierProvider().Modifiers(
            new RollQuery(kind, subject, [], c, null, "dnd5e", new Dictionary<string, string>()));

    [Fact]
    public async Task A_tight_hobble_is_5_ft_and_a_loose_one_15()
    {
        var (_, tight) = await Bind(new LewdBindChange { Kind = "cord", Sites = ["ankles"], Fit = "25 cm" });
        var (_, loose) = await Bind(new LewdBindChange { Kind = "cord", Sites = ["ankles"], Slack = "loose", Fit = "75 cm hobble cord" });

        Assert.Equal(5, BondageSlots.SpeedCap(BindingGraph.GetBindings(null, tight)));
        Assert.Equal(15, BondageSlots.SpeedCap(BindingGraph.GetBindings(null, loose)));
        Assert.Equal(-999, Assert.Single(Ask(tight, RollKinds.Speed)).Bonus);
        Assert.Equal(-15, Assert.Single(Ask(loose, RollKinds.Speed)).Bonus); // 30 ft base, capped at 15
    }

    [Fact]
    public async Task One_tight_tie_makes_the_slot_tight_even_beside_a_loose_one()
    {
        var (ctx, c) = await Bind(new LewdBindChange { Kind = "cord", Sites = ["ankles"], Slack = "loose" });
        await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "chars/guard", TargetId = "chars/convict", Kind = "manacle", Sites = ["ankles"] }, ctx);

        Assert.Equal(5, BondageSlots.SpeedCap(BindingGraph.GetBindings(null, c)));
    }

    [Fact]
    public async Task Loose_wrists_behind_do_not_pin_the_hands_but_make_them_awkward()
    {
        var (_, c) = await Bind(new LewdBindChange { Kind = "rope", Sites = ["wrists"], Orientation = "behind", Slack = "loose", Fit = "75 cm of rope" });
        var bindings = BindingGraph.GetBindings(null, c);

        Assert.False(Restraint.BlocksSomaticComponents(bindings));
        var attack = Assert.Single(Ask(c, RollKinds.Attack));
        Assert.Contains("awkwardly", attack.Reason);
        Assert.NotEmpty(Ask(c, RollKinds.Save, "Dexterity"));
    }

    [Fact]
    public async Task Tight_wrists_behind_still_pin_them()
    {
        var (_, c) = await Bind(new LewdBindChange { Kind = "rope", Sites = ["wrists"], Orientation = "behind" });

        Assert.True(Restraint.BlocksSomaticComponents(BindingGraph.GetBindings(null, c)));
    }

    [Theory]
    [InlineData("belt", "belt")]
    [InlineData("waist", "belt")]
    [InlineData("to collar", "collar")]
    public async Task Wrists_tied_to_the_belt_or_collar_pin_the_hands_in_front_of_the_body(string said, string stored)
    {
        var (_, c) = await Bind(new LewdBindChange { Kind = "chain", Sites = ["wrists"], Orientation = said, Fit = "25 cm chain" });
        var bindings = BindingGraph.GetBindings(null, c);

        Assert.Equal(stored, Assert.Single(bindings).Orientation);
        Assert.True(Restraint.BlocksSomaticComponents(bindings));
    }

    [Fact]
    public async Task Loose_belt_ties_are_awkward_not_pinned_and_a_loose_tie_slips_four_dcs_easier()
    {
        var (_, tight) = await Bind(new LewdBindChange { Kind = "rope", Sites = ["wrists"], Orientation = "belt" });
        var (_, loose) = await Bind(new LewdBindChange { Kind = "rope", Sites = ["wrists"], Orientation = "belt", Slack = "loose" });

        Assert.False(Restraint.BlocksSomaticComponents(BindingGraph.GetBindings(null, loose)));
        Assert.Equal(
            BindingGraph.GetBindings(null, tight)[0].EscapeDc - 4,
            BindingGraph.GetBindings(null, loose)[0].EscapeDc);
    }

    [Fact]
    public async Task The_dm_line_shows_looseness_and_the_fit_text_and_bad_slack_is_refused()
    {
        var (ctx, _) = await Bind(new LewdBindChange
        {
            Kind = "cord", Sites = ["ankles"], Slack = "loose", Fit = "75 cm hobble cord",
        });

        var line = ctx.Nudges.Last();
        Assert.Contains("ankles (loose)", line);
        Assert.Contains("speed 15 ft (loose)", line);
        Assert.Contains("Fit: cord: 75 cm hobble cord", line);

        var guard = TestContext.Adult("chars/guard");
        var c = TestContext.Adult("chars/convict");
        var bad = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "chars/guard", TargetId = "chars/convict", Kind = "cord", Sites = ["ankles"], Slack = "slippery" },
            new TestContext(null, guard, c));
        Assert.False(bad.Success);
        Assert.Contains("slack must be", bad.Message);
    }
}
