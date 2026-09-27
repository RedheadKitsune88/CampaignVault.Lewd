using CampaignVault.Data.Context;
using CampaignVault.Models;
using LewdHandbook.Guidance;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>Anatomy slot schema, derived universal slots, the migration that fills them, and the entry-time completeness prompt.</summary>
public sealed class AnatomyTests
{
    private static Character Adult(string id = "chars/a", params (string Key, string Value)[] traits)
    {
        var c = new Character { Id = id, Name = id, LifeStage = LifeStage.Adult };
        foreach (var (k, v) in traits)
            c.SystemStats.Traits[k] = v;
        return c;
    }

    private static string Slot(string slot) => LewdKeys.AnatomyPrefix + slot;

    [Fact]
    public void Undeclared_character_derives_mouth_hands_ass_but_never_cock_pussy_or_breasts()
    {
        var slots = AnatomyTraits.Effective(Adult()).Select(a => a.Slot).ToList();

        Assert.Equal(["hands", "mouth", "ass"], slots);
        Assert.All(AnatomyTraits.Effective(Adult()), a => Assert.True(a.IsDefault));
        Assert.Null(AnatomyTraits.Find(Adult(), "cock"));
    }

    [Fact]
    public void Declared_slots_join_the_derived_ones_in_catalogue_order()
    {
        var c = Adult(traits: (Slot("cock"), "die=1d10;tags=phallic"));

        Assert.Equal(["hands", "mouth", "ass", "cock"], AnatomyTraits.Effective(c).Select(a => a.Slot));
        Assert.False(AnatomyTraits.Find(c, "cock")!.IsDefault);
    }

    [Fact]
    public void None_removes_a_slot_and_custom_plan_turns_derivation_off()
    {
        var noAss = Adult(traits: (Slot("ass"), "none"));
        Assert.Equal(["hands", "mouth"], AnatomyTraits.Effective(noAss).Select(a => a.Slot));
        Assert.Null(AnatomyTraits.Find(noAss, "ass"));
        Assert.True(AnatomyTraits.IsDeclared(noAss, "ass"));

        var slime = Adult(traits: (Slot("plan"), "custom"));
        Assert.Empty(AnatomyTraits.Effective(slime));
        Assert.Null(AnatomyTraits.Find(slime, "mouth"));
    }

    [Fact]
    public void Aliases_resolve_to_the_canonical_slot()
    {
        var c = Adult(traits: (LewdKeys.LegacyAnatomyPrefix + "penis", "die=1d6"));

        Assert.Equal(Slot("cock"), AnatomyTraits.Find(c, "dick")!.Key);
        Assert.Contains(AnatomyTraits.ListAnatomy(c), a => a.Key == Slot("cock"));
        Assert.True(AnatomyTraits.IsDeclared(c, "cock"));
    }

    [Fact]
    public void Receptive_slots_parse_with_a_role_and_never_supply_dice()
    {
        var c = Adult(traits: (Slot("pussy"), "tags=natural,tight"));

        var pussy = AnatomyTraits.Find(c, "pussy")!;
        Assert.Equal(AnatomyRole.Receptive, pussy.Role);
        Assert.False(pussy.CanStimulate);
        Assert.Equal(AnatomyRole.Both, AnatomyTraits.Find(c, "mouth")!.Role);
    }

    [Fact]
    public void Guess_prefers_a_declared_implement_over_defaults_and_skips_receptive_slots()
    {
        var c = Adult(traits: [(Slot("pussy"), "role=receptive"), (Slot("tail"), "die=1d6;tags=prehensile")]);

        var resolved = ImplementResolver.Resolve(c, new Dictionary<string, Item>(), null, null, null, null);
        Assert.Equal(Slot("tail"), resolved!.Source);

        // With nothing declared, hands is the last-resort implement, never the receptive ass or the mouth.
        var bare = ImplementResolver.Resolve(Adult(), new Dictionary<string, Item>(), null, null, null, null);
        Assert.Equal(Slot("hands"), bare!.Source);
        Assert.True(bare.IsFinesse);
    }

    [Fact]
    public void A_receptive_slot_is_never_guessed_but_works_when_the_commit_names_it()
    {
        var c = Adult(traits: [(Slot("cock"), "die=1d8;tags=phallic"), (Slot("pussy"), "tags=natural,tight")]);
        var none = new Dictionary<string, Item>();

        var named = ImplementResolver.Resolve(c, none, null, "pussy", null, null);
        Assert.Equal(Slot("pussy"), named!.Source);
        Assert.DoesNotContain("phallic", named.Tags);

        var guessed = ImplementResolver.Resolve(c, none, null, null, null, null);
        Assert.Equal(Slot("cock"), guessed!.Source);
    }

    [Fact]
    public void Guess_ranks_a_pure_implement_above_the_mouth()
    {
        var c = Adult(traits: [(Slot("mouth"), "die=1d6;tags=oral"), (Slot("tail"), "die=1d4")]);

        Assert.Equal(Slot("tail"), ImplementResolver.Resolve(c, new Dictionary<string, Item>(), null, null, null, null)!.Source);
    }

    // --- migration ---------------------------------------------------------------------------------------

    private readonly LewdTraitsUpgrader _upgrader = new();

    private static Dictionary<string, string> Bag(params (string, string)[] kv) =>
        new(kv.ToDictionary(x => x.Item1, x => x.Item2), StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Upgrader_fills_universal_slots_only_where_lewd_data_exists()
    {
        var plain = Bag(("mood", "grumpy"));
        Assert.False(_upgrader.TryUpgrade(plain));
        Assert.DoesNotContain(plain.Keys, k => k.Contains("anatomy"));

        var lewd = Bag((LewdKeys.TraitStance, "willing"), (Slot("cock"), "die=1d8"));
        Assert.True(_upgrader.TryUpgrade(lewd));
        Assert.Contains(Slot("mouth"), lewd.Keys);
        Assert.Contains(Slot("hands"), lewd.Keys);
        Assert.Contains(Slot("ass"), lewd.Keys);
        Assert.Equal("die=1d8", lewd[Slot("cock")]);
        Assert.DoesNotContain(Slot("pussy"), lewd.Keys);
        Assert.False(_upgrader.TryUpgrade(lewd));
    }

    [Fact]
    public void Upgrader_ignores_bookkeeping_that_can_arise_for_anyone()
    {
        var bag = Bag((ViceState.AddictedKey("alcohol"), "true"), (LewdKeys.TraitInfertile, "true"), ("infertile", "true"));

        _upgrader.TryUpgrade(bag);

        Assert.DoesNotContain(bag.Keys, k => k.Contains("anatomy"));
    }

    [Fact]
    public void Upgrader_respects_none_aliases_and_custom_plans()
    {
        var bag = Bag((LewdKeys.TraitStance, "willing"), (Slot("ass"), "none"), (LewdKeys.AnatomyPrefix + "lips", "die=1d4"));
        Assert.True(_upgrader.TryUpgrade(bag));
        Assert.Equal("none", bag[Slot("ass")]);
        Assert.Equal("die=1d4", bag[Slot("mouth")]); // "lips" is a mouth alias: renamed, not overwritten by a default
        Assert.Contains(Slot("hands"), bag.Keys);

        var slime = Bag((LewdKeys.TraitStance, "willing"), (Slot("plan"), "custom"));
        Assert.False(_upgrader.TryUpgrade(slime));
        Assert.DoesNotContain(Slot("mouth"), slime.Keys);
    }

    [Fact]
    public void Upgrader_renames_anatomy_aliases_to_the_canonical_slot()
    {
        var bag = Bag((LewdKeys.AnatomyPrefix + "penis", "die=1d6"), (LewdKeys.LegacyAnatomyPrefix + "vagina", "role=receptive"));

        Assert.True(_upgrader.TryUpgrade(bag));

        Assert.Equal("die=1d6", bag[Slot("cock")]);
        Assert.Equal("role=receptive", bag[Slot("pussy")]);
        Assert.DoesNotContain(bag.Keys, k => k.Contains("penis") || k.Contains("vagina"));
    }

    [Fact]
    public void Filled_defaults_are_marked_as_defaults()
    {
        var bag = Bag((LewdKeys.TraitStance, "willing"));
        _upgrader.TryUpgrade(bag);

        var c = Adult();
        foreach (var (k, v) in bag)
            c.SystemStats.Traits[k] = v;

        Assert.All(AnatomyTraits.ListAnatomy(c), a => Assert.True(a.IsDefault));
    }

    // --- completeness prompt -----------------------------------------------------------------------------

    private sealed class Turn(IReadOnlyList<WorldChange> changes, params Character[] characters) : IContextTurn
    {
        public string CampaignName => "test";
        public IReadOnlyList<WorldChange> AppliedChanges => changes;
        public IReadOnlyList<string> InvolvedEntityIds => characters.Select(c => c.Id).ToList();
        public IReadOnlyList<string> PartyCharacterIds => [];
        public string? PartyLocationId => null;
        public CampaignConfig? Config => null;

        public Task<Character?> LoadCharacterAsync(string characterId, CancellationToken ct = default) =>
            Task.FromResult(characters.FirstOrDefault(c => c.Id == characterId));
    }

    private static ModeTransitionChange Enter(params string[] ids) =>
        new() { ModeId = LewdEncounterMode.ModeIdValue, Action = "enter", ParticipantIds = ids.ToList() };

    [Fact]
    public async Task Entering_a_scene_names_each_adult_with_undeclared_body_parts()
    {
        var a = Adult("chars/a", (Slot("cock"), "die=1d8"));
        var b = Adult("chars/b");
        var minor = new Character { Id = "chars/kid", LifeStage = LifeStage.Child };

        var item = Assert.Single(await new LewdAnatomyContributor().ContributeAsync(
            new Turn([Enter("chars/a", "chars/b", "chars/kid")], a, b, minor)));

        Assert.Contains("chars/a (pussy,breasts)", item.Text);
        Assert.Contains("chars/b (cock,pussy,breasts)", item.Text);
        Assert.DoesNotContain("kid", item.Text);
        Assert.Contains("chars/a (pussy,breasts)", item.Key);
    }

    [Fact]
    public async Task Prompt_goes_quiet_once_every_asked_slot_is_declared_or_marked_none()
    {
        var done = Adult("chars/a", (Slot("cock"), "none"), (Slot("pussy"), "role=receptive"), (Slot("breasts"), "none"));

        Assert.Empty(await new LewdAnatomyContributor().ContributeAsync(new Turn([Enter("chars/a")], done)));
    }

    [Fact]
    public async Task Prompt_only_fires_on_lewd_scene_entry()
    {
        var b = Adult("chars/b");
        var other = new ModeTransitionChange { ModeId = "crafting", Action = "enter", ParticipantIds = ["chars/b"] };
        var exit = new ModeTransitionChange { ModeId = LewdEncounterMode.ModeIdValue, Action = "exit" };

        Assert.Empty(await new LewdAnatomyContributor().ContributeAsync(new Turn([other, exit], b)));
    }
}
