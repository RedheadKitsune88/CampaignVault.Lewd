using CampaignVault.Models;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

public class ConsentGateTests
{
    private static ModeParticipantState Target(Action<ModeParticipantState>? cfg = null)
    {
        var p = new ModeParticipantState
        {
            CharacterId = "bob",
            State = new Dictionary<string, object>
            {
                [LewdKeys.Consent] = LewdKeys.ConsentWilling,
                [LewdKeys.HardLimits] = new List<string>(),
                [LewdKeys.SoftLimits] = new List<string>(),
                [LewdKeys.Kinks] = new List<string>(),
                [LewdKeys.Inhibition] = 2,
            }
        };
        cfg?.Invoke(p);
        return p;
    }

    [Fact]
    public void Revoked_consent_fails()
    {
        var t = Target(p => p.State[LewdKeys.Consent] = LewdKeys.ConsentRevoked);
        Assert.False(ConsentGate.TryAuthorizeAdvance(t, "alice", "piercing", null, out var err));
        Assert.Contains("revoked", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hard_limit_fails_commit()
    {
        var t = Target(p => p.State[LewdKeys.HardLimits] = new List<string> { "tentacles" });
        Assert.False(ConsentGate.TryAuthorizeAdvance(t, "alice", "cold", ["tentacles"], out var err));
        Assert.Contains("hard limit", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Willing_inhibition_floors_at_zero_unless_negative()
    {
        var t = Target(p => p.State[LewdKeys.Inhibition] = 3);
        Assert.Equal(0, ConsentGate.EffectiveInhibition(t, advanceIsWanted: true));

        t.State[LewdKeys.Inhibition] = -2;
        Assert.Equal(-2, ConsentGate.EffectiveInhibition(t, advanceIsWanted: true));
    }

    [Fact]
    public void Soft_limit_halves_stim_kink_increases()
    {
        var t = Target(p =>
        {
            p.State[LewdKeys.SoftLimits] = new List<string> { "fire" };
            p.State[LewdKeys.Kinks] = new List<string> { "psychic" };
        });
        Assert.Equal(5, ConsentGate.AdjustStimulationForTags(t, 10, "fire", null));
        Assert.Equal(15, ConsentGate.AdjustStimulationForTags(t, 10, "psychic", null));
    }

    [Fact]
    public void Selective_requires_allowed_partner()
    {
        var t = Target(p =>
        {
            p.State[LewdKeys.Consent] = LewdKeys.ConsentSelective;
            p.State[LewdKeys.AllowedPartners] = new List<string> { "alice" };
        });
        Assert.False(ConsentGate.TryAuthorizeAdvance(t, "eve", null, null, out _));
        Assert.True(ConsentGate.TryAuthorizeAdvance(t, "alice", null, null, out _));
    }
}
