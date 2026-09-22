using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LewdHandbook.Tests;

public class BindingSeedTests
{
    [Fact]
    public async Task Bitchsuit_item_properties_seed_crawl_effects_and_posture()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        var bob = mode.Participants[1];
        var suit = new Item
        {
            Id = "items/bitchsuit-1",
            Name = "bitchsuit",
            DefinitionName = "bitchsuit",
            HolderId = "bob",
            Description = "suit",
            Properties = new Dictionary<string, object>
            {
                ["lewdCategory"] = "worn_restraint",
                ["sites"] = new List<string> { "full_body", "arms", "legs" },
                ["implies"] = new List<string> { "encased", "hobbled", "cuffed" },
                ["effects"] = new List<string>
                {
                    "forced_crawl", "bent_knees_elbows", "all_fours", "no_upright_walk", "no_somatic_spellcasting",
                },
                ["posture"] = "all_fours_crawl",
            }
        };

        var character = new Character { Id = "bob", Name = "bob", SystemStats = new SystemExtension() };
        var ctx = new SeedContext(mode, suit, character);
        var result = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", ItemId = "items/bitchsuit-1" },
            ctx);

        Assert.True(result.Success);
        var bindings = BindingGraph.GetBindings(bob);
        Assert.Contains("forced_crawl", bindings[0].Effects);
        Assert.Equal("all_fours_crawl", bob.State[LewdKeys.Posture]?.ToString());
        Assert.Contains("forced_crawl", (bob.State["binding_effects"] as IEnumerable<string>)!);
    }

    [Fact]
    public async Task Ball_gag_stamps_gagged_with_BlocksVerbalComponents()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        var bob = mode.Participants[1];
        var gag = new Item
        {
            Id = "items/ball_gag-1",
            Name = "ball_gag",
            DefinitionName = "ball_gag",
            HolderId = "bob",
            Description = "gag",
            Properties = new Dictionary<string, object>
            {
                ["sites"] = new List<string> { "mouth" },
                ["implies"] = new List<string> { "gagged" },
                ["effects"] = new List<string> { "no_verbal_spellcasting", "no_clear_speech" },
                ["posture"] = "gagged",
            }
        };
        var character = new Character { Id = "bob", Name = "bob", SystemStats = new SystemExtension() };
        var ctx = new SeedContext(mode, gag, character);

        var result = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange { ActorId = "alice", TargetId = "bob", ItemId = "items/ball_gag-1" },
            ctx);

        Assert.True(result.Success);
        Assert.Equal("gagged", bob.State[LewdKeys.Posture]?.ToString());
        var gagged = character.SystemStats.StatusEffects.Single(e => e.Name == "gagged");
        Assert.Equal("gagged", gagged.ConditionName);
        Assert.True(gagged.StatModifiers.TryGetValue("BlocksVerbalComponents", out var v) && v != 0);
    }

    [Fact]
    public async Task Cuffs_behind_sets_arm_position_and_somatic_block_tags()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        var bob = mode.Participants[1];
        var cuffs = new Item
        {
            Id = "items/leather_cuffs-1",
            Name = "leather_cuffs",
            DefinitionName = "leather_cuffs",
            HolderId = "bob",
            Description = "cuffs",
            Properties = new Dictionary<string, object>
            {
                ["sites"] = new List<string> { "wrists" },
                ["implies"] = new List<string> { "cuffed" },
            }
        };
        var character = new Character { Id = "bob", Name = "bob", SystemStats = new SystemExtension() };
        var ctx = new SeedContext(mode, cuffs, character);

        var result = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange
            {
                ActorId = "alice",
                TargetId = "bob",
                ItemId = "items/leather_cuffs-1",
                Sites = ["wrists"],
                Orientation = "behind",
                Implies = ["cuffed"],
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal("behind", bob.State[LewdKeys.ArmPosition]?.ToString());
        Assert.Equal("free", bob.State[LewdKeys.LegPosition]?.ToString());
        var binding = BindingGraph.GetBindings(bob).Single();
        Assert.Equal("behind", binding.Orientation);
        Assert.Contains("no_somatic_spellcasting", binding.Effects);
        Assert.Contains(character.SystemStats.StatusEffects, e =>
            e.StatModifiers.ContainsKey("BlocksSomaticComponents"));
    }

    [Fact]
    public async Task Cuffs_front_sets_arm_position_front_without_somatic_auto_block()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        var bob = mode.Participants[1];
        var cuffs = new Item
        {
            Id = "items/leather_cuffs-1",
            Name = "leather_cuffs",
            DefinitionName = "leather_cuffs",
            HolderId = "bob",
            Description = "cuffs",
            Properties = new Dictionary<string, object>
            {
                ["implies"] = new List<string> { "cuffed" },
            }
        };
        var ctx = new SeedContext(mode, cuffs);

        var result = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange
            {
                ActorId = "alice",
                TargetId = "bob",
                ItemId = "items/leather_cuffs-1",
                Sites = ["wrists"],
                Orientation = "front",
                Implies = ["cuffed"],
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal("front", bob.State[LewdKeys.ArmPosition]?.ToString());
        var binding = BindingGraph.GetBindings(bob).Single();
        Assert.DoesNotContain("no_somatic_spellcasting", binding.Effects);
    }

    [Fact]
    public async Task Ankle_apart_sets_leg_position()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        var bob = mode.Participants[1];
        var ctx = new SeedContext(mode, new Item
        {
            Id = "items/spreader-1",
            Name = "spreader_bar",
            DefinitionName = "spreader_bar",
            HolderId = "bob",
            Description = "bar",
            Properties = new Dictionary<string, object>
            {
                ["implies"] = new List<string> { "cuffed", "hobbled" },
                ["effects"] = new List<string> { "forced_spread" },
            }
        });

        var result = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange
            {
                ActorId = "alice",
                TargetId = "bob",
                ItemId = "items/spreader-1",
                Sites = ["ankles"],
                Orientation = "apart",
                Implies = ["cuffed", "hobbled"],
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal("apart", bob.State[LewdKeys.LegPosition]?.ToString());
        Assert.Equal("free", bob.State[LewdKeys.ArmPosition]?.ToString());
    }

    private sealed class SeedContext : IChangeContext
    {
        public SeedContext(ModeEncounter mode, Item item, Character? character = null)
        {
            ActiveMode = mode;
            Items = new Dictionary<string, Item> { [item.Id] = item };
            var chars = new Dictionary<string, Character>();
            if (character is not null)
                chars[character.Id] = character;
            Characters = chars;
        }

        public IReadOnlyDictionary<string, Character> Characters { get; }
        public IReadOnlyDictionary<string, Item> Items { get; }
        public IReadOnlyDictionary<string, Location> Locations { get; } = new Dictionary<string, Location>();
        public IReadOnlyDictionary<string, Faction> Factions { get; } = new Dictionary<string, Faction>();
        public IReadOnlyDictionary<string, Quest> Quests { get; } = new Dictionary<string, Quest>();
        public Microsoft.Extensions.Logging.ILogger Logger { get; } = NullLogger.Instance;
        public CombatEncounter? ActiveCombat => null;
        public ModeEncounter? ActiveMode { get; }
        public CampaignConfig? Config => null;
        public CampaignVault.Data.IRollService? Rolls => null;
        public string? CampaignName => "test";
        public HashSet<string> InvolvedEntities { get; } = [];
        public IReadOnlyList<WorldChange>? Batch => null;
        public int BatchIndex => 0;
        public Func<Task<CampaignTime>> GetCurrentTimeAsync { get; } = () => Task.FromResult(new CampaignTime());
        public Func<Task<Dictionary<string, string>>> GetSystemOptionsAsync { get; } =
            () => Task.FromResult(new Dictionary<string, string>());
        public Func<Event, Task> LogEventAsync { get; } = _ => Task.CompletedTask;
        public void RegisterNewLocation(Location loc) { }
        public void RegisterNewCharacter(Character c) { }
        public void RegisterNewItem(Item i) { }
        public void RegisterNewFaction(Faction f) { }
        public void RegisterNewQuest(Quest q) { }
        public void RecordMessage(string message) { }
        public void RecordPhysicalStateNudge(string message) { }
        public void RecordFailure() { }
        public void RecordEntityCollision(string entityId, string message) { }
        public void RecordCommittedId(string id) { }
        public Task<string?> SuggestLocationMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestCharacterMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestItemMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestFactionMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestQuestMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
    }
}
