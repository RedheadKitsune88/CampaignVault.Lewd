using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>What the handbook's conditions and restraints do to rolls, through the host's modifier hook.</summary>
public sealed class RollModifierProviderTests
{
    private static RollQuery Query(Character c, string kind, string? subject = null, params string[] tags) =>
        new(kind, subject, tags, c, null, "dnd5e", new Dictionary<string, string>());

    private static IEnumerable<RollModifier> Ask(Character c, string kind, string? subject = null, params string[] tags) =>
        new LewdRollModifierProvider().Modifiers(Query(c, kind, subject, tags));

    private static async Task<Character> Bound(string orientation, string[] sites, string kind = "rope")
    {
        var guard = TestContext.Adult("chars/guard");
        var c = TestContext.Adult("chars/convict");
        c.SystemStats = new Dnd5eExtension { Dexterity = 14 };
        c.SystemStats.StatusEffects.Add(new StatusEffect { Name = "grappled", ConditionName = "grappled", AppliedBy = "combat" });
        var ctx = new TestContext(null, guard, c);
        var r = await new LewdBindHandler().ApplyAsync(new LewdBindChange
        {
            ActorId = "chars/guard", TargetId = "chars/convict", Kind = kind, Sites = [.. sites], Orientation = orientation,
        }, ctx);
        Assert.True(r.Success, r.Message);
        return c;
    }

    [Fact]
    public void An_intoxicated_character_rolls_mental_saves_at_disadvantage_and_physical_ones_normally()
    {
        var c = TestContext.Adult("chars/x");
        LewdPoolHelper.EnsureNamedCondition(c, LewdKeys.ConditionIntoxicated, LewdKeys.ConditionIntoxicated, "drunk");

        Assert.Contains(Ask(c, RollKinds.Save, "Wisdom", "mental"), m => m.Advantage == AdvantageEffect.Disadvantage);
        Assert.Contains(Ask(c, RollKinds.Save, "cha"), m => m.Advantage == AdvantageEffect.Disadvantage);
        Assert.Empty(Ask(c, RollKinds.Save, "Dexterity"));
    }

    [Fact]
    public async Task Hands_bound_behind_cost_advantage_on_attacks_and_bound_limbs_on_dex_rolls()
    {
        var c = await Bound("behind", ["wrists"]);

        var attack = Assert.Single(Ask(c, RollKinds.Attack));
        Assert.Equal(AdvantageEffect.Disadvantage, attack.Advantage);
        Assert.Contains("attacks", attack.Reason);
        Assert.Contains(Ask(c, RollKinds.Save, "Dexterity"), m => m.Reason.Contains("Dex"));
        Assert.Contains(Ask(c, RollKinds.Check, "Acrobatics"), m => m.Reason.Contains("Dex"));
        Assert.Empty(Ask(c, RollKinds.Check, "Athletics"));
    }

    [Fact]
    public async Task Cuffs_in_front_leave_attacks_alone_but_still_hamper_dex()
    {
        var c = await Bound("front", ["wrists"]);

        Assert.Empty(Ask(c, RollKinds.Attack));
        Assert.NotEmpty(Ask(c, RollKinds.Save, "Dexterity"));
    }

    [Fact]
    public async Task An_escape_attempt_is_not_hampered_by_the_restraint_it_is_escaping()
    {
        var c = await Bound("behind", ["wrists"]);

        Assert.Empty(Ask(c, RollKinds.Check, "dex", "escape"));
        Assert.Empty(Ask(c, RollKinds.Check, "str", "escape"));
    }

    [Fact]
    public async Task Hobbles_cap_speed_by_a_penalty_large_enough_for_the_hosts_floor()
    {
        var c = await Bound("together", ["ankles"]);

        var speed = Assert.Single(Ask(c, RollKinds.Speed));
        Assert.True(speed.Bonus <= -100);
        Assert.Empty(Ask(await Bound("front", ["wrists"]), RollKinds.Speed));
    }

    [Fact]
    public async Task A_blindfold_costs_advantage_on_attacks_and_perception()
    {
        var c = await Bound("free", ["eyes"], kind: "sash");

        Assert.NotEmpty(Ask(c, RollKinds.Attack));
        Assert.NotEmpty(Ask(c, RollKinds.Check, "Perception"));
        Assert.Empty(Ask(c, RollKinds.Check, "Stealth"));
    }

    [Fact]
    public async Task Save_dice_runs_a_plugin_roll_through_the_context_and_says_why()
    {
        var c = await Bound("behind", ["wrists"]);
        var ctx = new TestContext(null, c);

        var roll = await SaveDice.RollAsync(
            ctx, "t", 12, abilityMod: 2, disadvantage: false, CancellationToken.None,
            who: c, subject: "dex", kind: RollKinds.Save);

        Assert.Equal(14, roll.Total);
        Assert.Contains("Bound limbs: disadvantage on Dex", roll.Summary);
    }

    [Fact]
    public async Task Save_dice_without_a_roller_leaves_the_callers_numbers_alone()
    {
        var c = await Bound("behind", ["wrists"]);

        var roll = await SaveDice.RollAsync(new TestContext(null, c), "t", 12, 2, false, CancellationToken.None);

        Assert.Equal("12+2=14", roll.Summary);
    }
}
