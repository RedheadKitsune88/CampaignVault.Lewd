using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using LewdHandbook.Changes;
using LewdHandbook.Handlers;
using LewdHandbook.Mechanics;
using LewdHandbook.Observers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LewdHandbook.Tests;

public class PregnancyTests
{
    [Fact]
    public void Traditional_dc_and_condom()
    {
        Assert.Equal(13, PregnancyMath.TraditionalDc(1, 2, condom: false));
        Assert.Equal(25, PregnancyMath.TraditionalDc(1, 2, condom: true));
        Assert.Equal(18, PregnancyMath.PickDie(11, 18, advantage: true));
        Assert.Equal(4, PregnancyMath.PickDie(11, 4, advantage: false));
        Assert.Equal(1, PregnancyMath.DefaultRestDelta(nontraditional: false));
        Assert.Equal(25, PregnancyMath.DefaultRestDelta(nontraditional: true));
        Assert.Equal(100, PregnancyMath.ClampProgress(140));
    }

    [Fact]
    public async Task Traditional_success_stamps_trait_and_status()
    {
        var (mode, bobState) = BuildMode();
        var bob = Character("bob");
        var ctx = new FakeChangeContext(mode, bob);

        var result = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange
            {
                ActorId = "alice",
                TargetId = "bob",
                D20 = 15,
                ActorConModifier = 3,
                TargetConModifier = 1,
                TargetProficiency = 2,
            },
            ctx);

        Assert.True(result.Success);
        Assert.Equal("true", bob.SystemStats.Traits[LewdKeys.Pregnant]);
        Assert.Equal("0", bob.SystemStats.Traits[LewdKeys.PregnancyProgress]);
        Assert.Equal("traditional", bob.SystemStats.Traits[LewdKeys.PregnancyType]);
        // Not showing yet: the Pregnant condition (and rest saves) start at progress 25.
        Assert.DoesNotContain(bob.SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionPregnant);
        Assert.True(ConsentGate.GetBool(bobState, LewdKeys.Pregnant));
        Assert.Contains(ctx.Published, p => p.Topic == LewdHandbook.Events.LewdEvents.Pregnancy);
    }

    [Fact]
    public async Task Infertile_blocks_traditional_without_failing_commit()
    {
        var (mode, _) = BuildMode();
        var bob = Character("bob");
        bob.SystemStats.Traits[LewdKeys.TraitInfertile] = "true";
        var ctx = new FakeChangeContext(mode, bob);

        var result = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange
            {
                ActorId = "alice",
                TargetId = "bob",
                D20 = 20,
                ActorConModifier = 5,
            },
            ctx);

        Assert.True(result.Success);
        Assert.False(PregnancyState.Flag(bob, LewdKeys.Pregnant));
    }

    [Fact]
    public async Task Unwilling_fails_unless_grimdark()
    {
        var (mode, bobState) = BuildMode();
        bobState.State[LewdKeys.Consent] = LewdKeys.ConsentUnwilling;
        var ctx = new FakeChangeContext(mode, Character("bob"));

        var blocked = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange { ActorId = "alice", TargetId = "bob", D20 = 20, ActorConModifier = 5, Force = true },
            ctx);
        Assert.False(blocked.Success);

        ctx.GetSystemOptionsAsync = () => Task.FromResult(new Dictionary<string, string>
        {
            [LewdKeys.NonConsentOption] = LewdKeys.NonConsentOn,
        });
        var allowed = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange
            {
                ActorId = "alice",
                TargetId = "bob",
                D20 = 20,
                ActorConModifier = 5,
                NoEscape = true,
                Force = true,
            },
            ctx);
        Assert.True(allowed.Success);
        Assert.Equal("true", ctx.Characters["bob"].SystemStats.Traits[LewdKeys.BadEnded]);
        Assert.Equal(BadEndMath.CaptureImpreg, ctx.Characters["bob"].SystemStats.Traits[LewdKeys.BadEndReason]);
        Assert.Contains(ctx.Characters["bob"].SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionBadEnded);
        Assert.True(ConsentGate.GetBool(bobState, LewdKeys.BadEnded));
    }

    [Fact]
    public async Task Rest_failure_stamps_poisoned()
    {
        var (mode, _) = BuildMode();
        var bob = Character("bob");
        bob.SystemStats.Traits[LewdKeys.Pregnant] = "true";
        bob.SystemStats.Traits[LewdKeys.PregnancyType] = "nontraditional";
        var ctx = new FakeChangeContext(mode, bob);

        var result = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange
            {
                TargetId = "bob",
                Action = "rest",
                D20 = 2,
                TargetConModifier = 0,
            },
            ctx);

        Assert.True(result.Success);
        Assert.Contains(bob.SystemStats.StatusEffects, e => e.ConditionName == "poisoned");
    }

[Fact]
    public async Task RestChange_observer_stamps_poison_from_sheet_con()
    {
        var (mode, _) = BuildMode();
        var bob = new Character
        { LifeStage = LifeStage.Adult,
            Id = "bob",
            Name = "bob",
            SystemStats = new Dnd5eExtension { Constitution = 8 },
        };
        bob.SystemStats.Traits[LewdKeys.Pregnant] = "true";
        bob.SystemStats.Traits[LewdKeys.PregnancyProgress] = "40";
        var ctx = new FakeChangeContext(mode, bob)
        {
            Rolls = new FixedFace(4),
        };

        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "bob", RestType = "short" }, ctx);

        // Face 4 + Con -1 = 3 < DC 15; a second message for the same rest does not roll again.
        Assert.Contains(bob.SystemStats.StatusEffects, e => e.ConditionName == "poisoned");
        Assert.Equal(ViceState.HoursNow(new CampaignTime()), bob.SystemStats.Attributes[PregnancyState.RestCheckHoursKey]);
        var effects = bob.SystemStats.StatusEffects.Count;
        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "bob", RestType = "short" }, ctx);
        Assert.Contains(ctx.Messages, m => m.Contains("already resolved"));
        Assert.Equal(effects, bob.SystemStats.StatusEffects.Count);
    }

    private static (ModeEncounter mode, ModeParticipantState bob) BuildMode()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        return (mode, mode.Participants[1]);
    }

    [Theory]
    [InlineData("oil_of_impotence")]
    [InlineData("beads_of_prevention")]
    [InlineData("potion_of_infertility")]
    public async Task Catalog_contraceptive_names_block_like_their_short_forms(string contraceptive)
    {
        var (mode, _) = BuildMode();
        var bob = Character("bob");
        var ctx = new FakeChangeContext(mode, bob);

        var result = await new LewdPregnancyHandler().ApplyAsync(
            new LewdPregnancyChange
            {
                ActorId = "alice", TargetId = "bob", D20 = 20, ActorConModifier = 5, Contraceptive = contraceptive,
            },
            ctx);

        Assert.True(result.Success);
        Assert.False(bob.SystemStats.Traits.ContainsKey(LewdKeys.Pregnant));
    }

    private static Character Character(string id) => new()
    { LifeStage = LifeStage.Adult,
        Id = id,
        Name = id,
        SystemStats = new SystemExtension(),
    };

private sealed class FixedFace : IRollService
    {
        private readonly int _face;
        public FixedFace(int face) => _face = face;

        public Task<RollOutcome> RollAsync(RollRequest request, CancellationToken ct = default) =>
            Task.FromResult(new RollOutcome
            {
                Tag = request.Tag,
                Result = _face + request.Bonus,
                IndividualDice = [_face],
                Summary = $"[{_face}]+{request.Bonus}",
            });

        public async Task<IReadOnlyList<RollOutcome>> RollBatchAsync(
            IEnumerable<RollRequest> requests, CancellationToken ct = default)
        {
            var list = new List<RollOutcome>();
            foreach (var r in requests)
                list.Add(await RollAsync(r, ct));
            return list;
        }
    }

    private sealed class FakeChangeContext : IChangeContext
    {
        private readonly Dictionary<string, Character> _characters;

        public FakeChangeContext(ModeEncounter mode, Character character)
        {
            ActiveMode = mode;
            _characters = new Dictionary<string, Character> { [character.Id] = character };
            _characters.TryAdd("alice", new Character { Id = "alice", Name = "alice", LifeStage = LifeStage.Adult });
            Characters = _characters;
        }

        public IReadOnlyDictionary<string, Character> Characters { get; }
        public IReadOnlyDictionary<string, Item> Items { get; } = new Dictionary<string, Item>();
        public IReadOnlyDictionary<string, Location> Locations { get; } = new Dictionary<string, Location>();
        public IReadOnlyDictionary<string, Faction> Factions { get; } = new Dictionary<string, Faction>();
        public IReadOnlyDictionary<string, Quest> Quests { get; } = new Dictionary<string, Quest>();
        public Microsoft.Extensions.Logging.ILogger Logger { get; } = NullLogger.Instance;
        public CombatEncounter? ActiveCombat => null;
        public ModeEncounter? ActiveMode { get; }
        public IReadOnlyDictionary<string, ModeEncounter> ActiveModes =>
            ActiveMode is { } m
                ? new Dictionary<string, ModeEncounter>(StringComparer.OrdinalIgnoreCase) { [m.ModeId] = m }
                : new Dictionary<string, ModeEncounter>(StringComparer.OrdinalIgnoreCase);
        public CampaignConfig? Config => null;
        public IRollService? Rolls { get; set; }
        public string? CampaignName => "test";
        public HashSet<string> InvolvedEntities { get; } = [];
        public IReadOnlyList<WorldChange>? Batch => null;
        public int BatchIndex => 0;
        public Func<Task<CampaignTime>> GetCurrentTimeAsync { get; } = () => Task.FromResult(new CampaignTime());
        public Func<Task<Dictionary<string, string>>> GetSystemOptionsAsync { get; set; } =
            () => Task.FromResult(new Dictionary<string, string>());
        public Func<Event, Task> LogEventAsync { get; } = _ => Task.CompletedTask;
        public void RegisterNewLocation(Location loc) { }
        public void RegisterNewCharacter(Character c) { }
        public void RegisterNewItem(Item i) { }
        public void RegisterNewFaction(Faction f) { }
        public void RegisterNewQuest(Quest q) { }
        public void Publish(string topic, object? data = null) =>
            Published.Add((topic, data));
        public List<(string Topic, object? Data)> Published { get; } = [];
        public List<string> Messages { get; } = [];
        public void RecordMessage(string message) => Messages.Add(message);
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
