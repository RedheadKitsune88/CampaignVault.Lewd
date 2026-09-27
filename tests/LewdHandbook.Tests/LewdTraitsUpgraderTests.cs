using CampaignVault.Models;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public class LewdTraitsUpgraderTests
{
    private readonly LewdTraitsUpgrader _upgrader = new();

    [Fact]
    public void TryUpgrade_MovesCatalogAndAnatomy_Idempotent()
    {
        var traits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sexual_history"] = "virgin",
            ["recovery_die"] = "d8",
            ["implement_proficiencies"] = "flogger",
            ["anatomy.cock"] = "die=1d8;tags=phallic,natural",
            ["pregnant"] = "true",
        };

        Assert.True(_upgrader.TryUpgrade(traits));
        Assert.Equal("virgin", traits[LewdKeys.TraitSexualHistory]);
        Assert.Equal("d8", traits[LewdKeys.TraitRecoveryDie]);
        Assert.Equal("flogger", traits[LewdKeys.TraitImplementProficiencies]);
        Assert.Equal("die=1d8;tags=phallic,natural", traits[LewdKeys.AnatomyPrefix + "cock"]);
        Assert.Equal("true", traits["pregnant"]);
        Assert.False(traits.ContainsKey("sexual_history"));
        Assert.False(traits.ContainsKey("anatomy.cock"));

        Assert.False(_upgrader.TryUpgrade(traits));
    }

    [Fact]
    public void TryUpgrade_MovesViceTraits_LeavesAttributeShapedKeys()
    {
        var traits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["vice.sex.addicted"] = "true",
            ["vice.sex.kind"] = "psychological",
            ["vice.sex.locked"] = "true",
            ["vice.sex.ability"] = "wis",
            ["vice.sex.prior_addiction"] = "true",
            ["vice.sex.withdrawal"] = "true",
            ["vice.sex.dc"] = "14",
        };

        Assert.True(_upgrader.TryUpgrade(traits));
        Assert.Equal("true", traits[ViceState.AddictedKey("sex")]);
        Assert.Equal("psychological", traits[ViceState.KindKey("sex")]);
        Assert.Equal("true", traits[ViceState.LockedKey("sex")]);
        Assert.Equal("wis", traits[ViceState.AbilityKey("sex")]);
        Assert.Equal("true", traits[ViceState.PriorKey("sex")]);
        Assert.Equal("true", traits[ViceState.WithdrawalKey("sex")]);
        Assert.Equal("14", traits["vice.sex.dc"]);
        Assert.False(traits.ContainsKey("vice.sex.addicted"));
        Assert.False(_upgrader.TryUpgrade(traits));
    }

    [Fact]
    public void AnatomyTraits_DualReadsLegacyKeys()
    {
        var character = new Character
        { LifeStage = LifeStage.Adult,
            Id = "chars/a",
            Name = "A",
            SystemStats = new SystemExtension
            {
                Traits =
                {
                    ["anatomy.cock"] = "die=1d6;tags=phallic,natural",
                    ["sexual_history"] = "modest_lover",
                }
            }
        };

        var found = AnatomyTraits.Find(character, "cock");
        Assert.NotNull(found);
        Assert.Equal(LewdKeys.AnatomyPrefix + "cock", found!.Key);
        Assert.Equal("modest_lover", AnatomyTraits.GetSexualHistory(character));
    }

    [Fact]
    public void TryUpgrade_HidesBookkeeping_AndUnhidesRestraint()
    {
        var traits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["imprints"] = "wanton:3:1:willing:0",
            ["imprint_inhib"] = "1",
            ["intrusive_thoughts"] = "imprint:wanton",
            ["imprint.wanton.exposed"] = "true",
            ["lustbrand_inhib"] = "2",
            ["lustbrand.abundance.payload"] = "x",
            ["lustbrand.abundance.endowment"] = "1",
            ["hyperfertile"] = "true",
            ["bad_end_vice_id"] = "sex",
            ["bad_end_imprint_track"] = "ordeal",
            [LewdKeys.ModeTraitPrefix + "bindings"] = "[]",
            [LewdKeys.ModeTraitPrefix + "posture"] = "kneeling",
            // Stay visible.
            ["lustbrands"] = "abundance:1",
            ["lustbrand_glow"] = "gold",
            ["pregnant"] = "true",
            ["pregnancy_progress"] = "40",
            ["bad_ended"] = "true",
            ["bad_end_reason"] = "defeat",
        };

        Assert.True(_upgrader.TryUpgrade(traits));

        Assert.Equal("wanton:3:1:willing:0", traits[LewdKeys.TraitImprints]);
        Assert.Equal("1", traits[LewdKeys.TraitImprintInhib]);
        Assert.Equal("imprint:wanton", traits[LewdKeys.TraitIntrusiveThoughts]);
        Assert.Equal("true", traits[LewdKeys.ImprintTraitPrefix + "wanton.exposed"]);
        Assert.Equal("2", traits[LewdKeys.TraitLustbrandInhib]);
        Assert.Equal("x", traits[LewdKeys.LustbrandTraitPrefix + "abundance.payload"]);
        Assert.Equal("1", traits[LewdKeys.LustbrandTraitPrefix + "abundance.endowment"]);
        Assert.Equal("true", traits[LewdKeys.TraitHyperfertile]);
        Assert.Equal("sex", traits[LewdKeys.TraitBadEndViceId]);
        Assert.Equal("ordeal", traits[LewdKeys.TraitBadEndImprintTrack]);
        Assert.Equal("[]", traits["bindings"]);
        Assert.Equal("kneeling", traits["posture"]);

        foreach (var bare in new[] { "imprints", "imprint_inhib", "intrusive_thoughts", "lustbrand_inhib", "hyperfertile",
                     "bad_end_vice_id", "bad_end_imprint_track", "imprint.wanton.exposed", "lustbrand.abundance.payload",
                     LewdKeys.ModeTraitPrefix + "bindings", LewdKeys.ModeTraitPrefix + "posture" })
            Assert.False(traits.ContainsKey(bare), bare);

        foreach (var visible in new[] { "lustbrands", "lustbrand_glow", "pregnant", "pregnancy_progress", "bad_ended", "bad_end_reason" })
            Assert.True(traits.ContainsKey(visible), visible);

        Assert.False(_upgrader.TryUpgrade(traits));
    }

    [Fact]
    public void TryUpgrade_NeverOverwritesANewerKey()
    {
        var traits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["infertile"] = "true",
            [LewdKeys.TraitInfertile] = "false",
            [LewdKeys.ModeTraitPrefix + "bindings"] = "old",
            ["bindings"] = "new",
        };

        _upgrader.TryUpgrade(traits);

        Assert.Equal("false", traits[LewdKeys.TraitInfertile]);
        Assert.Equal("new", traits["bindings"]);
    }

    [Fact]
    public void Readers_fall_back_to_the_legacy_key_on_an_unupgraded_document()
    {
        var character = new Character { Id = "chars/a", LifeStage = LifeStage.Adult };
        character.SystemStats.Traits["infertile"] = "true";
        character.SystemStats.Traits[LewdKeys.ModeTraitPrefix + "bindings"] = "[]";

        Assert.True(PregnancyState.Flag(character, LewdKeys.TraitInfertile));
        Assert.Equal("[]", PregnancyState.Text(character, LewdKeys.TraitBindings));

        PregnancyState.Set(character, LewdKeys.TraitInfertile, "false");
        Assert.False(character.SystemStats.Traits.ContainsKey("infertile"));
        Assert.False(PregnancyState.Flag(character, LewdKeys.TraitInfertile));
    }

    [Fact]
    public void Consuming_a_one_shot_value_also_drops_its_legacy_copy()
    {
        var character = new Character { Id = "chars/a", LifeStage = LifeStage.Adult };
        character.SystemStats.Traits[LewdKeys.BadEndViceId] = "alcohol"; // the bare, pre-migration key

        Assert.Equal("alcohol", PregnancyState.Text(character, LewdKeys.TraitBadEndViceId));
        PregnancyState.Remove(character, LewdKeys.TraitBadEndViceId);

        Assert.Null(PregnancyState.Text(character, LewdKeys.TraitBadEndViceId));
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.BadEndViceId));
    }
}
