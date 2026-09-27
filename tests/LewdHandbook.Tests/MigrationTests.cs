using CampaignVault.Data.Context;
using CampaignVault.Models;
using LewdHandbook.Guidance;
using LewdHandbook.Mechanics;
using Xunit;

namespace LewdHandbook.Tests;

/// <summary>0.6/0.7 data migration: intimacyTone → player-owned options, and the retroactive lifeStage prompt.</summary>
public sealed class MigrationTests
{
    [Theory]
    [InlineData("consensual", "off", null)]
    [InlineData("GRIMDARK", "on", null)]
    [InlineData("fade", "on", "fade")]
    [InlineData("something-else", "off", null)]
    public void IntimacyTone_maps_onto_the_new_options(string tone, string nonConsent, string? narration)
    {
        var options = new Dictionary<string, string> { ["IntimacyTone"] = tone };

        Assert.True(new LewdCampaignOptionsUpgrader().TryUpgrade(options));

        Assert.False(options.ContainsKey("IntimacyTone"));
        Assert.Equal(nonConsent, options[LewdKeys.NonConsentOption]);
        Assert.Equal(narration, options.GetValueOrDefault(LewdKeys.NarrationOption));
        Assert.False(new LewdCampaignOptionsUpgrader().TryUpgrade(options)); // idempotent
    }

    [Fact]
    public void IntimacyTone_never_overwrites_a_setting_the_player_already_chose()
    {
        var options = new Dictionary<string, string> { ["intimacyTone"] = "grimdark", ["LEWDNONCONSENT"] = "off" };

        Assert.True(new LewdCampaignOptionsUpgrader().TryUpgrade(options));

        Assert.Equal("off", options["LEWDNONCONSENT"]);
        Assert.False(options.ContainsKey(LewdKeys.NonConsentOption));
    }

    private sealed class Turn(CampaignConfig? config, params Character[] characters) : IContextTurn
    {
        public string CampaignName => "test";
        public IReadOnlyList<WorldChange> AppliedChanges { get; init; } = [];
        public IReadOnlyList<string> InvolvedEntityIds { get; init; } = characters.Select(c => c.Id).Append("locs/tavern").ToList();
        public IReadOnlyList<string> PartyCharacterIds { get; init; } = [];
        public string? PartyLocationId => null;
        public CampaignConfig? Config => config;
        public List<string> Loaded { get; } = [];

        public Task<Character?> LoadCharacterAsync(string characterId, CancellationToken ct = default)
        {
            Loaded.Add(characterId);
            return Task.FromResult(characters.FirstOrDefault(c => c.Id == characterId));
        }
    }

    private static CampaignConfig LewdEnabled() => new() { EnabledModeIds = [LewdEncounterMode.ModeIdValue] };

    [Fact]
    public async Task LifeStage_prompt_names_unset_characters_in_one_keyed_line()
    {
        var turn = new Turn(
            LewdEnabled(),
            new Character { Id = "chars/b", LifeStage = LifeStage.Unspecified },
            new Character { Id = "chars/a", LifeStage = LifeStage.Unspecified },
            new Character { Id = "chars/c", LifeStage = LifeStage.Adult });

        var item = Assert.Single(await new LewdLifeStageContributor().ContributeAsync(turn));

        Assert.Equal("lewd:lifestage:chars/a, chars/b", item.Key);
        Assert.Contains("character_update lifeStage", item.Text);
        Assert.DoesNotContain("chars/c", item.Text);
        Assert.DoesNotContain("locs/tavern", turn.Loaded);
    }

    [Fact]
    public async Task LifeStage_prompt_is_silent_when_lewd_is_not_enabled_or_everyone_is_set()
    {
        var unset = new Character { Id = "chars/a" };

        Assert.Empty(await new LewdLifeStageContributor().ContributeAsync(new Turn(new CampaignConfig(), unset)));
        Assert.Empty(await new LewdLifeStageContributor().ContributeAsync(new Turn(null, unset)));
        Assert.Empty(await new LewdLifeStageContributor().ContributeAsync(
            new Turn(LewdEnabled(), new Character { Id = "chars/a", LifeStage = LifeStage.Elder })));
    }
}
