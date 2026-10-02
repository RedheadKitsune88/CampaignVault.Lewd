using CampaignVault.Services;
using Xunit;

namespace LewdHandbook.HostTests;

/// <summary>The plugin's YAML parses through the host's own providers and gives what it says (SDK 0.15 data-driven content).</summary>
public class ContentLoadingTests
{
    private static readonly System.Reflection.Assembly Host = typeof(ProgressionDefinitionProvider).Assembly;

    private static string DataRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "LewdHandbook", "RulesetData");
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException("src/LewdHandbook/RulesetData not found above the test binaries.");
    }

    [Theory]
    [InlineData("bard", "college_of_burlesque")]
    [InlineData("bard", "college_of_romance")]
    [InlineData("barbarian", "path_of_the_breeder")]
    [InlineData("cleric", "perversion_domain")]
    [InlineData("cleric", "sexuality_domain")]
    public void Subclasses_JoinTheCoreProgressionAsHomebrewOptions(string cls, string option)
    {
        var provider = new ProgressionDefinitionProvider(Path.Combine(Path.GetTempPath(), "cv-none"), Host, null, [DataRoot()]);
        Assert.True(provider.TryGetProgression("dnd5e", cls, out var progression));

        var choice = progression!.Levels[cls == "cleric" ? 1 : 3].Choices.Single(c => c.Key == "subclass");
        var found = Assert.Single(progression.OptionsFor(choice), o => o.Id == option);
        Assert.True(found.Homebrew);
    }

    [Fact]
    public void DomainSpells_AreGrantedByTheDomain()
    {
        var provider = new ProgressionDefinitionProvider(Path.Combine(Path.GetTempPath(), "cv-none"), Host, null, [DataRoot()]);
        provider.TryGetProgression("dnd5e", "cleric", out var cleric);

        var gained = cleric!.FeaturesUpTo(5, (_, key) => key == "subclass" ? ["sexuality_domain"] : []);
        Assert.Contains(gained.SelectMany(g => g.Feature.Spells.Values).SelectMany(s => s), s => s == "pregnancy_ward");
    }

    [Fact]
    public void Feats_CarryEffectsPoolsAndTheGate()
    {
        var feats = new FeatDefinitionProvider(DataRoot(), Host);
        Assert.True(feats.TryGet("dnd5e", "promiscuous", out var promiscuous));
        Assert.Contains(promiscuous!.Effects, e => e.Kind == "advantage" && e.Assert.Contains("sexualPreferences"));
        Assert.Contains("recovery_dice", promiscuous.ExtraPools);
        Assert.Equal("com.campaignvault.lewd-handbook", promiscuous.Requires?.Plugin);
    }

    [Fact]
    public void DevotedPartner_StartsWithAnAftercarePack()
    {
        var backgrounds = new BackgroundDefinitionProvider(DataRoot(), Host);
        Assert.True(backgrounds.TryGet("dnd5e", "devoted_partner", out var background));
        Assert.Contains(background!.Equipment, i => i.Item == "aftercare_pack");
    }

    [Fact]
    public void RecoveryDice_ScaleWithLevel()
    {
        var pool = new ResourcePoolProvider(DataRoot(), Host).GetPoolsForSystem("dnd5e")["recovery_dice"];
        Assert.Equal(1, pool.MaxFrom!.LevelMultiplier);
    }

    [Fact]
    public void EverySpell_NamesItsSchool()
    {
        var spells = Directory.GetFiles(Path.Combine(DataRoot(), "dnd5e", "spells"), "*.yaml");
        Assert.All(spells, f => Assert.Contains("\nschool:", File.ReadAllText(f)));
    }
}
