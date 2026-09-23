using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LewdHandbook.Tests;

public class LewdAdvanceHandlerTests
{
    [Fact]
    public async Task Hard_limit_fails_without_mutating_pools()
    {
        var (mode, bob) = BuildMode();
        bob.State[LewdKeys.HardLimits] = new List<string> { "piercing" };
        var character = BuildCharacter("bob", arousal: 2, max: 20, numbing: 0);
        var ctx = new FakeChangeContext(mode, character);

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange
            {
                ActorId = "alice",
                TargetId = "bob",
                Kind = "martial",
                Hit = true,
                StimulationAmount = 5,
                StimulationType = "piercing",
            },
            ctx);

        Assert.False(result.Success);
        Assert.Equal(2, character.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current);
    }

    [Fact]
    public async Task Willing_advance_applies_stim_through_numbing()
    {
        var (mode, _) = BuildMode();
        var character = BuildCharacter("bob", arousal: 4, max: 20, numbing: 3);
        var ctx = new FakeChangeContext(mode, character);

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange
            {
                ActorId = "alice",
                TargetId = "bob",
                Kind = "martial",
                StimulationAmount = 8,
                StimulationType = "bludgeoning",
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal(0, character.SystemStats.ResourcePools[LewdKeys.PoolNumbing].Current);
        Assert.Equal(9, character.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current); // 4 + (8-3)
    }

    [Fact]
    public async Task Unwilling_martial_without_hit_fails_in_grimdark()
    {
        var (mode, bob) = BuildMode();
        bob.State[LewdKeys.Consent] = LewdKeys.ConsentUnwilling;
        var ctx = new FakeChangeContext(mode, BuildCharacter("bob", 0, 10, 0));
        ctx.GetSystemOptionsAsync = () => Task.FromResult(new Dictionary<string, string>
        {
            [LewdKeys.IntimacyToneOption] = LewdKeys.ToneGrimdark,
        });

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange
            {
                ActorId = "alice",
                TargetId = "bob",
                Kind = "martial",
                StimulationAmount = 4,
            },
            ctx);

        Assert.False(result.Success);
        Assert.Contains("hit=", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unwilling_fails_closed_in_consensual_tone()
    {
        var (mode, bob) = BuildMode();
        bob.State[LewdKeys.Consent] = LewdKeys.ConsentUnwilling;
        var ctx = new FakeChangeContext(mode, BuildCharacter("bob", 0, 10, 0));

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange
            {
                ActorId = "alice",
                TargetId = "bob",
                Kind = "martial",
                Hit = true,
                StimulationAmount = 4,
            },
            ctx);

        Assert.False(result.Success);
        Assert.Contains("intimacyTone=consensual", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fade_tone_applies_no_stim_and_nudges()
    {
        var (mode, bob) = BuildMode();
        bob.State[LewdKeys.Consent] = LewdKeys.ConsentUnwilling;
        var character = BuildCharacter("bob", 2, 20, 0);
        var ctx = new FakeChangeContext(mode, character);
        ctx.GetSystemOptionsAsync = () => Task.FromResult(new Dictionary<string, string>
        {
            [LewdKeys.IntimacyToneOption] = LewdKeys.ToneFade,
        });

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange
            {
                ActorId = "alice",
                TargetId = "bob",
                Kind = "martial",
                Hit = true,
                StimulationAmount = 8,
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal(2, character.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current);
        Assert.Contains(ctx.Nudges, n => n.Contains("fades", StringComparison.OrdinalIgnoreCase) || n.Contains("refuses", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Anatomy_traits_roll_stim_when_amount_omitted()
    {
        var (mode, _) = BuildMode();
        var bob = BuildCharacter("bob", 0, 20, 0);
        var alice = new Character
        {
            Id = "alice",
            Name = "alice",
            SystemStats = new SystemExtension
            {
                Traits =
                {
                    ["anatomy.cock"] = "die=1d8;tags=phallic,natural;size=medium",
                }
            }
        };
        var ctx = new FakeChangeContext(mode, bob);
        ctx.AddCharacter(alice);
        ctx.Rolls = new FakeRollService(outcomes: new() { ["lewd_stimulation"] = 6 });

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange
            {
                ActorId = "alice",
                TargetId = "bob",
                Kind = "martial",
                AnatomyKey = "cock",
                AbilityBonus = 2,
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal(8, bob.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current); // 6+2
    }

    [Fact]
    public async Task Bind_and_unbind_update_structured_state()
    {
        var (mode, bob) = BuildMode();
        var ctx = new FakeChangeContext(mode, BuildCharacter("bob", 0, 10, 0));

        var bindResult = await new LewdBindHandler().ApplyAsync(
            new LewdBindChange
            {
                ActorId = "alice",
                TargetId = "bob",
                ItemId = "ball_gag",
                Sites = ["mouth"],
                Implies = ["gagged"],
            },
            ctx);
        Assert.True(bindResult.Success);
        var bindings = BindingGraph.GetBindings(bob);
        Assert.Single(bindings);
        Assert.Contains("gagged", bindings[0].Implies);

        var unbindResult = await new LewdUnbindHandler().ApplyAsync(
            new LewdUnbindChange { ActorId = "alice", TargetId = "bob", RemoveAll = true },
            ctx);
        Assert.True(unbindResult.Success);
        Assert.Empty(BindingGraph.GetBindings(bob));
    }

    [Fact]
    public async Task Incapacitated_fourth_climax_stamps_overstim_without_filth_tick()
    {
        var (mode, bob) = BuildMode();
        bob.State[LewdKeys.ClimaxStreak] = 3;
        bob.State[LewdKeys.ClimaxIncapacitated] = true;
        var character = BuildCharacter("bob", 0, 10, 0);
        var ctx = new FakeChangeContext(mode, character);

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange
            {
                ActorId = "alice",
                TargetId = "bob",
                Kind = "martial",
                Hit = true,
                StimulationAmount = 10,
                StimulationType = "piercing",
                Tags = ["fluids"],
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal(1, ConsentGate.GetInt(bob, LewdKeys.Overstimulation));
        Assert.Contains(character.SystemStats.StatusEffects, e => e.Name == "Overstimulation 1");
        Assert.False(bob.State.ContainsKey("filth"));
        Assert.True(ConsentGate.GetBool(bob, LewdKeys.HadPhysical));
    }

    [Fact]
    public async Task Verbal_advance_caps_virgin_and_does_not_dirty()
    {
        var (mode, bob) = BuildMode();
        bob.State[LewdKeys.TraitSexualHistory] = "virgin";
        var character = BuildCharacter("bob", 0, 20, 0);
        var ctx = new FakeChangeContext(mode, character);

        var result = await new LewdAdvanceHandler().ApplyAsync(
            new LewdAdvanceChange
            {
                ActorId = "alice",
                TargetId = "bob",
                Kind = "skilled",
                StimulationAmount = 9,
                StimulationType = "psychic",
                Tags = ["verbal"],
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal(2, character.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current);
        Assert.False(ConsentGate.GetBool(bob, LewdKeys.HadPhysical));
        Assert.False(bob.State.ContainsKey("filth"));
    }

    [Fact]
    public async Task Climax_check_third_failure_clears_edging()
    {
        var (mode, bob) = BuildMode();
        bob.State[LewdKeys.Edging] = true;
        bob.State[LewdKeys.ClimaxFailures] = 2;
        bob.State[LewdKeys.ArousalCurrentMirror] = 10;
        bob.State[LewdKeys.ArousalMaxMirror] = 10;
        var character = BuildCharacter("bob", 10, 10, 0);
        var ctx = new FakeChangeContext(mode, character);

        var result = await new LewdClimaxCheckHandler().ApplyAsync(
            new LewdClimaxCheckChange { TargetId = "bob", D20 = 2, InhibitionBonus = 0 },
            ctx);

        Assert.True(result.Success);
        Assert.False(ConsentGate.GetBool(bob, LewdKeys.Edging));
        Assert.Equal(0, ConsentGate.GetInt(bob, LewdKeys.ClimaxFailures));
    }

    private static (ModeEncounter mode, ModeParticipantState bob) BuildMode()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        return (mode, mode.Participants[1]);
    }

    private static Character BuildCharacter(string id, int arousal, int max, int numbing) =>
        new()
        {
            Id = id,
            Name = id,
            SystemStats = new SystemExtension
            {
                ResourcePools =
                {
                    [LewdKeys.PoolArousal] = new ResourcePool { Current = arousal, Max = max, Recovery = RecoveryType.Never },
                    [LewdKeys.PoolNumbing] = new ResourcePool { Current = numbing, Max = numbing, Recovery = RecoveryType.Never },
                }
            }
        };

    private sealed class FakeChangeContext : IChangeContext
    {
        private readonly List<string> _messages = [];

        private readonly Dictionary<string, Character> _characters;
        private readonly List<string> _nudges = [];

        public FakeChangeContext(ModeEncounter mode, Character character)
        {
            ActiveMode = mode;
            _characters = new Dictionary<string, Character> { [character.Id] = character };
            Characters = _characters;
        }

        public void AddCharacter(Character c) => _characters[c.Id] = c;
        public IReadOnlyList<string> Nudges => _nudges;

        public IReadOnlyDictionary<string, Character> Characters { get; }
        public IReadOnlyDictionary<string, Item> Items { get; } = new Dictionary<string, Item>();
        public IReadOnlyDictionary<string, Location> Locations { get; } = new Dictionary<string, Location>();
        public IReadOnlyDictionary<string, Faction> Factions { get; } = new Dictionary<string, Faction>();
        public IReadOnlyDictionary<string, Quest> Quests { get; } = new Dictionary<string, Quest>();
        public Microsoft.Extensions.Logging.ILogger Logger { get; } = NullLogger.Instance;
        public CombatEncounter? ActiveCombat => null;
        public ModeEncounter? ActiveMode { get; }
        public CampaignConfig? Config => null;
        public CampaignVault.Data.IRollService? Rolls { get; set; }
        public string? CampaignName => "test";
        public HashSet<string> InvolvedEntities { get; } = [];
        public IReadOnlyList<WorldChange>? Batch => null;
        public int BatchIndex => 0;
        public Func<Task<CampaignTime>> GetCurrentTimeAsync { get; } = () => Task.FromResult(new CampaignTime());
        public Func<Task<Dictionary<string, string>>> GetSystemOptionsAsync { get; set; } = () => Task.FromResult(new Dictionary<string, string>());
        public Func<Event, Task> LogEventAsync { get; } = _ => Task.CompletedTask;
        public void RegisterNewLocation(Location loc) { }
        public void RegisterNewCharacter(Character c) { }
        public void RegisterNewItem(Item i) { }
        public void RegisterNewFaction(Faction f) { }
        public void RegisterNewQuest(Quest q) { }
        public void RecordMessage(string message) => _messages.Add(message);
        public void RecordPhysicalStateNudge(string message) { if (!string.IsNullOrWhiteSpace(message)) _nudges.Add(message); }
        public void RecordFailure() { }
        public void RecordEntityCollision(string entityId, string message) { }
        public void RecordCommittedId(string id) { }
        public Task<string?> SuggestLocationMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestCharacterMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestItemMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestFactionMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
        public Task<string?> SuggestQuestMatchAsync(string? nameQuery) => Task.FromResult<string?>(null);
    }

    private sealed class FakeRollService : IRollService
    {
        private readonly Dictionary<string, int> _outcomes;
        public FakeRollService(Dictionary<string, int> outcomes) => _outcomes = outcomes;

        public Task<RollOutcome> RollAsync(RollRequest request, CancellationToken ct = default)
        {
            var result = _outcomes.TryGetValue(request.Tag, out var v) ? v : 10;
            // AbilityBonus is applied by handler via request.Bonus — return face only in Result? Handler uses roll.Result as stim.
            // Our Fake returns result+Bonus to match real RollService behavior.
            return Task.FromResult(new RollOutcome
            {
                Tag = request.Tag,
                Result = result + request.Bonus,
                IndividualDice = [result],
                Summary = $"[{result}]+{request.Bonus} = {result + request.Bonus}",
            });
        }

        public async Task<IReadOnlyList<RollOutcome>> RollBatchAsync(IEnumerable<RollRequest> requests, CancellationToken ct = default)
        {
            var list = new List<RollOutcome>();
            foreach (var r in requests)
                list.Add(await RollAsync(r, ct));
            return list;
        }
    }
}
