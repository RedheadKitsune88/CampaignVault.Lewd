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

public class BadEndTests
{
    [Fact]
    public void Math_table()
    {
        Assert.False(BadEndMath.Evaluate(Probe(overstim: 6, already: true)).Mark);
        Assert.Equal(BadEndMath.Overstim, BadEndMath.Evaluate(Probe(overstim: 6)).Reason);
        Assert.Equal(BadEndMath.EmptyRecovery, BadEndMath.Evaluate(Probe(climaxed: true, edging: true, recovery: true, recoveryCurrent: 0)).Reason);
        Assert.True(BadEndMath.Evaluate(Probe(climaxed: true, edging: true)).MissingRecoveryPool);
        Assert.False(BadEndMath.Evaluate(Probe(climaxed: true, edging: true)).Mark);
        Assert.False(BadEndMath.Evaluate(Probe(climaxed: true, edging: true, recovery: true, recoveryCurrent: 2)).Mark);
        Assert.Equal(BadEndMath.ArousalMax, BadEndMath.Evaluate(Probe(arousal: true, arousalMax: 0)).Reason);
        Assert.False(BadEndMath.Evaluate(Probe(arousalMax: 0)).Mark);
        var down = BadEndMath.Evaluate(Probe(hp: 0));
        Assert.False(down.Mark);
        Assert.True(down.PromptDefeat);
        Assert.False(BadEndMath.Evaluate(Probe(hp: 0, already: true)).PromptDefeat);
        Assert.True(BadEndMath.IsVerbReason("defeat"));
        Assert.False(BadEndMath.IsVerbReason("overstim"));
    }

    [Fact]
    public void Empty_recovery_stamps_trait_status_and_flag()
    {
        var (mode, bob) = Mode();
        bob.State[LewdKeys.Edging] = true;
        var character = Character("bob");
        character.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = new ResourcePool { Current = 0, Max = 4 };
        LewdAdvanceHandler.ApplyClimaxResult(bob, character, new ResourcePool { Current = 8, Max = 10 }, Climax());
        Assert.Equal("true", character.SystemStats.Traits[LewdKeys.BadEnded]);
        Assert.Equal(BadEndMath.EmptyRecovery, character.SystemStats.Traits[LewdKeys.BadEndReason]);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionBadEnded);
        Assert.True(ConsentGate.GetBool(bob, LewdKeys.BadEnded));
        _ = mode;
    }

    [Fact]
    public void Recovery_remaining_does_not_mark()
    {
        var (_, bob) = Mode();
        bob.State[LewdKeys.Edging] = true;
        var character = Character("bob");
        character.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = new ResourcePool { Current = 2, Max = 4 };
        LewdAdvanceHandler.ApplyClimaxResult(bob, character, new ResourcePool { Current = 8, Max = 10 }, Climax());
        Assert.False(ConsentGate.GetBool(bob, LewdKeys.BadEnded));
    }

    [Fact]
    public void Overstim_tick_to_six_stamps_trait_and_status()
    {
        var (_, bob) = Mode();
        bob.State[LewdKeys.ClimaxIncapacitated] = true;
        bob.State[LewdKeys.ClimaxStreak] = 5;
        bob.State[LewdKeys.Overstimulation] = 5;
        var character = Character("bob");
        LewdAdvanceHandler.ApplyClimaxResult(bob, character, new ResourcePool { Current = 1, Max = 10 }, Climax());
        Assert.Equal("true", character.SystemStats.Traits[LewdKeys.BadEnded]);
        Assert.Equal(BadEndMath.Overstim, character.SystemStats.Traits[LewdKeys.BadEndReason]);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.Name == BadEndState.EffectName);
    }

    [Fact]
    public void Second_apply_does_not_duplicate_status()
    {
        var (_, bob) = Mode();
        var character = Character("bob");
        BadEndState.Apply(bob, character, BadEndMath.Overstim);
        BadEndState.Apply(bob, character, BadEndMath.Overstim);
        Assert.Equal(1, character.SystemStats.StatusEffects.Count(e => e.ConditionName == LewdKeys.ConditionBadEnded));
    }

    [Fact]
    public async Task Observer_hp_prompts_arousal_marks_status_restamps_inactive_skips()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.CurrentHp = 0;
        var ctx = new Recorder(mode, character);
        var observer = new LewdBadEndObserver();

        await observer.OnCommittedAsync(new HpChange { CharacterId = "bob", Delta = -1 }, ctx);
        Assert.False(PregnancyState.Flag(character, LewdKeys.BadEnded));
        Assert.Contains(ctx.Messages, m => m.Contains("not automatic"));

        character.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 0, Max = 0 };
        await observer.OnCommittedAsync(new CharacterUpdate { CharacterId = "bob" }, ctx);
        Assert.Equal(BadEndMath.ArousalMax, character.SystemStats.Traits[LewdKeys.BadEndReason]);

        character.SystemStats.StatusEffects.Clear();
        bob.State[LewdKeys.BadEnded] = true;
        character.SystemStats.Traits[LewdKeys.BadEnded] = "true";
        await observer.OnCommittedAsync(new StatusRemove { CharacterId = "bob", Status = "Bad-Ended" }, ctx);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionBadEnded);

        mode.IsActive = false;
        Assert.False(observer.IsInterestedIn(new HpChange { CharacterId = "bob" }, ctx));
    }

    [Fact]
    public async Task Verb_gates_and_stores_without_level_drain()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.ClassLevel = "Fighter 5";
        character.SystemStats = new Dnd5eExtension { Level = 5 };
        var ctx = new Recorder(mode, character);
        var handler = new LewdBadEndHandler();

        var consensual = await handler.ApplyAsync(new LewdBadEndChange
        {
            TargetId = "bob",
            Reason = "defeat",
            NoEscape = true,
        }, ctx);
        Assert.False(consensual.Success);

        ctx.Tone = LewdKeys.ToneGrimdark;
        var noEscape = await handler.ApplyAsync(new LewdBadEndChange
        {
            TargetId = "bob",
            Reason = "explicit",
            NoEscape = false,
            Consequence = "slave",
        }, ctx);
        Assert.False(noEscape.Success);

        var slave = await handler.ApplyAsync(new LewdBadEndChange
        {
            TargetId = "bob",
            Reason = "defeat",
            NoEscape = true,
            Consequence = "slave",
        }, ctx);
        Assert.True(slave.Success);
        Assert.Equal("slave", character.SystemStats.Traits[LewdKeys.BadEndConsequence]);
        Assert.Equal("Fighter 5", character.ClassLevel);
        Assert.Equal(5, ((Dnd5eExtension)character.SystemStats).Level);

        var missingTrack = await handler.ApplyAsync(new LewdBadEndChange
        {
            TargetId = "bob",
            Reason = "explicit",
            NoEscape = true,
            Consequence = "imprint",
        }, ctx);
        Assert.False(missingTrack.Success);

        var imprint = await handler.ApplyAsync(new LewdBadEndChange
        {
            TargetId = "bob",
            Reason = "explicit",
            NoEscape = true,
            Consequence = "imprint",
            ImprintTrack = "ordeal",
            ImprintJump = 3,
        }, ctx);
        Assert.True(imprint.Success);
        Assert.Equal("ordeal", character.SystemStats.Traits[LewdKeys.BadEndImprintTrack]);
        Assert.Equal("3", character.SystemStats.Traits[LewdKeys.BadEndImprintJump]);
        Assert.False(character.SystemStats.Traits.ContainsKey("imprints"));
        _ = bob;
    }

    private static BadEndProbe Probe(
        bool already = false,
        int overstim = 0,
        bool climaxed = false,
        bool edging = false,
        bool recovery = false,
        int recoveryCurrent = 0,
        bool arousal = false,
        int arousalMax = 10,
        int? hp = null) =>
        new(already, overstim, climaxed, edging, recovery, recoveryCurrent, arousal, arousalMax, hp);

    private static ClimaxSaveResult Climax() =>
        new(ClimaxOutcomeKind.Climaxed, 0, 3, 0, false, "climax");

    private static (ModeEncounter mode, ModeParticipantState bob) Mode()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        return (mode, mode.Participants[1]);
    }

    private static Character Character(string id) => new()
    {
        Id = id,
        Name = id,
        SystemStats = new SystemExtension(),
    };

    private sealed class Recorder : IChangeContext
    {
        public Recorder(ModeEncounter mode, Character character)
        {
            ActiveMode = mode;
            Characters = new Dictionary<string, Character> { [character.Id] = character };
        }

        public List<string> Messages { get; } = [];
        public string Tone { get; set; } = LewdKeys.ToneConsensual;
        public IReadOnlyDictionary<string, Character> Characters { get; }
        public IReadOnlyDictionary<string, Item> Items { get; } = new Dictionary<string, Item>();
        public IReadOnlyDictionary<string, Location> Locations { get; } = new Dictionary<string, Location>();
        public IReadOnlyDictionary<string, Faction> Factions { get; } = new Dictionary<string, Faction>();
        public IReadOnlyDictionary<string, Quest> Quests { get; } = new Dictionary<string, Quest>();
        public Microsoft.Extensions.Logging.ILogger Logger { get; } = NullLogger.Instance;
        public CombatEncounter? ActiveCombat => null;
        public ModeEncounter? ActiveMode { get; }
        public CampaignConfig? Config => null;
        public IRollService? Rolls { get; set; }
        public string? CampaignName => "test";
        public HashSet<string> InvolvedEntities { get; } = [];
        public IReadOnlyList<WorldChange>? Batch => null;
        public int BatchIndex => 0;
        public Func<Task<CampaignTime>> GetCurrentTimeAsync { get; } = () => Task.FromResult(new CampaignTime());
        public Func<Task<Dictionary<string, string>>> GetSystemOptionsAsync =>
            () => Task.FromResult(new Dictionary<string, string> { [LewdKeys.IntimacyToneOption] = Tone });
        public Func<Event, Task> LogEventAsync { get; } = _ => Task.CompletedTask;
        public void RegisterNewLocation(Location loc) { }
        public void RegisterNewCharacter(Character c) { }
        public void RegisterNewItem(Item i) { }
        public void RegisterNewFaction(Faction f) { }
        public void RegisterNewQuest(Quest q) { }
        public void RecordMessage(string message) => Messages.Add(message);
        public void RecordPhysicalStateNudge(string message) => Messages.Add(message);
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
