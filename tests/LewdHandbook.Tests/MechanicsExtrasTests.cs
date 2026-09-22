using CampaignVault.Models;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public class MechanicsExtrasTests
{
    [Fact]
    public void Anatomy_traits_parse_die_and_tags()
    {
        var impl = AnatomyTraits.Parse("anatomy.cock", "die=1d8;tags=phallic,natural;size=medium;finesse=true");
        Assert.Equal("1d8", impl.DieExpression);
        Assert.Contains("phallic", impl.Tags);
        Assert.True(impl.IsFinesse);
        Assert.Equal("medium", impl.Size);
    }

    [Fact]
    public void Binding_seed_from_bitchsuit_item_id()
    {
        var entry = BindingGraph.SeedFromItemProperties("bitchsuit", null);
        Assert.Contains("encased", entry.Implies);
        Assert.Contains("hobbled", entry.Implies);
    }

    [Fact]
    public void Grimdark_allows_unwilling_hard_limits_still_fail()
    {
        var t = new ModeParticipantState
        {
            CharacterId = "bob",
            State =
            {
                [LewdKeys.Consent] = LewdKeys.ConsentUnwilling,
                [LewdKeys.HardLimits] = new List<string> { "piercing" },
            }
        };
        var allow = ConsentGate.AuthorizeAdvance(t, "alice", "bludgeoning", null, IntimacyToneKind.Grimdark, out _);
        Assert.Equal(ConsentAuthorizeResult.Allow, allow);

        var hard = ConsentGate.AuthorizeAdvance(t, "alice", "piercing", null, IntimacyToneKind.Grimdark, out var err);
        Assert.Equal(ConsentAuthorizeResult.Fail, hard);
        Assert.Contains("hard limit", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Maximize_dice_counts_faces()
    {
        Assert.Equal(8, ImplementResolver.MaximizeDice("1d8"));
        Assert.Equal(12, ImplementResolver.MaximizeDice("2d6"));
    }
}
