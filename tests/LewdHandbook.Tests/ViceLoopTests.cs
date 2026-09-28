using CampaignVault.Data.Context;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Guidance;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>The addiction loop: stages, temptation saves, compulsion, Resolve, treatment, getting clean.</summary>
public sealed class ViceLoopTests
{
    /// <summary>Addicted to alcohol (DC 12, base 10, window 4h), last drink at hour 0; Con +2.</summary>
    private static (TestContext Ctx, Character Bob) Drinker(float hoursNow)
    {
        var bob = TestContext.Adult("bob");
        bob.SystemStats = new Dnd5eExtension { Constitution = 14 };
        bob.SystemStats.Traits[ViceState.AddictedKey("alcohol")] = "true";
        bob.SystemStats.Traits[ViceState.AbilityKey("alcohol")] = "con";
        bob.SystemStats.Attributes[ViceState.DcKey("alcohol")] = 12;
        bob.SystemStats.Attributes[ViceState.BaseDcKey("alcohol")] = 10;
        bob.SystemStats.Attributes[ViceState.LastHoursKey("alcohol")] = 0;
        var ctx = new TestContext(null, bob) { Time = new CampaignTime { TotalDaysElapsed = (int)(hoursNow / 24), Hour = (int)(hoursNow % 24) } };
        return (ctx, bob);
    }

    private static Task<CampaignVault.Data.ChangeHandlers.ChangeHandlerResult> Vice(TestContext ctx, string action, int d20 = 0, string? method = null) =>
        new LewdViceHandler().ApplyAsync(new LewdViceChange { CharacterId = "bob", ViceId = "alcohol", Action = action, D20 = d20, Method = method }, ctx);

    [Fact]
    public async Task Stages_deepen_with_time_and_each_is_announced_once()
    {
        var (ctx, bob) = Drinker(hoursNow: 2);
        await Vice(ctx, "note_presence");
        Assert.Contains(ctx.Messages, m => m.Contains("craves alcohol"));

        ctx.Time = new CampaignTime { Hour = 5 };
        await Vice(ctx, "note_presence", d20: 20);
        Assert.Contains(bob.SystemStats.StatusEffects, e => e.Name == ViceTrack.WithdrawalEffect("alcohol"));

        ctx.Time = new CampaignTime { Hour = 13 };
        ctx.Messages.Clear();
        await Vice(ctx, "note_presence", d20: 20);
        await Vice(ctx, "note_presence", d20: 20);
        Assert.Single(ctx.Messages, m => m.Contains("severe"));
    }

    [Fact]
    public async Task In_withdrawal_presence_forces_a_save_failure_compels_and_partaking_releases()
    {
        var (ctx, bob) = Drinker(hoursNow: 6);

        await Vice(ctx, "note_presence", d20: 3); // 3 + 2 < 12
        Assert.True(ViceTrack.IsCompelled(bob, "alcohol"));
        Assert.Contains(bob.SystemStats.StatusEffects, e => e.Name == ViceTrack.CompelledEffect("alcohol"));

        var drink = await new LewdViceHandler().ApplyAsync(
            new LewdViceChange { CharacterId = "bob", ViceId = "alcohol", Action = "consume", Ability = "con" }, ctx);
        Assert.True(drink.Success, drink.Message);
        Assert.False(ViceTrack.IsCompelled(bob, "alcohol"));
        Assert.DoesNotContain(bob.SystemStats.StatusEffects, e => e.Name == ViceTrack.WithdrawalEffect("alcohol"));
        Assert.DoesNotContain("vice:alcohol", bob.SystemStats.Traits.GetValueOrDefault(LewdKeys.TraitIntrusiveThoughts) ?? "");
    }

    [Fact]
    public async Task Resisting_earns_resolve_that_is_spent_on_the_next_long_rest_save()
    {
        var (ctx, bob) = Drinker(hoursNow: 6);
        await Vice(ctx, "note_presence", d20: 15);
        await Vice(ctx, "resist", d20: 15);
        Assert.Equal(2, ViceTrack.Resolve(bob, "alcohol"));

        // 7 + Con 2 + Resolve 2 = 11 < 12: still fails, but the resolve was spent.
        var rest = await Vice(ctx, "rest", d20: 7);
        Assert.True(rest.Success);
        Assert.Contains(ctx.Messages, m => m.Contains("Resolve +2"));
        Assert.Equal(0, ViceTrack.Resolve(bob, "alcohol"));
    }

    [Fact]
    public async Task Greater_restoration_makes_the_next_saves_automatic_until_the_long_rest()
    {
        var (ctx, bob) = Drinker(hoursNow: 6);
        var treat = await Vice(ctx, "treat", method: "greater_restoration");
        Assert.True(treat.Success, treat.Message);

        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "bob", RestType = "long" }, ctx);

        Assert.Equal(11, ViceState.CurrentDc(bob, ViceCatalog.All.Single(v => v.Id == "alcohol")));
        Assert.Contains(ctx.Messages, m => m.Contains("automatic"));
        Assert.Null(ViceTrack.Aid(bob, "alcohol")); // used up by the rest
    }

    [Fact]
    public async Task Remove_curse_only_eases_magical_vices()
    {
        var (ctx, _) = Drinker(hoursNow: 1);

        var result = await Vice(ctx, "treat", method: "remove_curse");

        Assert.False(result.Success);
        Assert.Contains("magical", result.Message);
    }

    [Fact]
    public async Task Party_craving_line_comes_from_campaign_time()
    {
        var (_, bob) = Drinker(hoursNow: 0);
        var turn = new Turn(bob, new CampaignTime { Hour = 5 });

        var item = Assert.Single(await new LewdViceContributor().ContributeAsync(turn));

        Assert.Contains("withdrawal from alcohol", item.Text);
        Assert.EndsWith(":Withdrawal", item.Key);
    }

    [Fact]
    public async Task Party_craving_line_stays_quiet_where_the_mode_is_not_enabled()
    {
        var (_, bob) = Drinker(hoursNow: 0);
        var turn = new QuietTurn(new Turn(bob, new CampaignTime { Hour = 5 }));

        Assert.Empty(await new LewdViceContributor().ContributeAsync(turn));
    }

    private sealed class QuietTurn(IContextTurn inner) : IContextTurn
    {
        public string CampaignName => inner.CampaignName;
        public IReadOnlyList<WorldChange> AppliedChanges => inner.AppliedChanges;
        public IReadOnlyList<string> InvolvedEntityIds => inner.InvolvedEntityIds;
        public IReadOnlyList<string> PartyCharacterIds => inner.PartyCharacterIds;
        public string? PartyLocationId => inner.PartyLocationId;
        public CampaignTime? Time => inner.Time;
        public CampaignConfig? Config => new();

        public Task<Character?> LoadCharacterAsync(string characterId, CancellationToken ct = default) =>
            inner.LoadCharacterAsync(characterId, ct);
    }

    private sealed class Turn(Character pc, CampaignTime time) : IContextTurn
    {
        public string CampaignName => "test";
        public IReadOnlyList<WorldChange> AppliedChanges => [];
        public IReadOnlyList<string> InvolvedEntityIds => [];
        public IReadOnlyList<string> PartyCharacterIds => [pc.Id];
        public string? PartyLocationId => null;
        public CampaignTime? Time => time;
        public CampaignConfig? Config => new() { EnabledModeIds = [LewdEncounterMode.ModeIdValue] };

        public Task<Character?> LoadCharacterAsync(string characterId, CancellationToken ct = default) =>
            Task.FromResult<Character?>(characterId == pc.Id ? pc : null);
    }
}
