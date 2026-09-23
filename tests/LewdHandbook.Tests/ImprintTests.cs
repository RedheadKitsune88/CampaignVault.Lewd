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

public class ImprintTests
{
    [Fact]
    public void Thresholds_are_3_7_12()
    {
        Assert.Equal(0, ImprintMath.LevelFor(2));
        Assert.Equal(1, ImprintMath.LevelFor(3));
        Assert.Equal(2, ImprintMath.LevelFor(7));
        Assert.Equal(3, ImprintMath.LevelFor(12));
        Assert.Equal(12, ImprintMath.PointsFor(3));
        var cruel = Character("a");
        cruel.SystemStats.Traits[LewdKeys.Imprints] = "cruelty:12:3:willing:0";
        Assert.Equal(1, ImprintState.SufferingBonus(cruel, ["pain"], 0));
        Assert.Equal(0, ImprintState.SufferingBonus(cruel, ["kiss"], 0));
        Assert.Equal(1, ImprintState.SufferingBonus(cruel, ["kiss"], 2));
    }

    [Fact]
    public async Task Willing_delta_reaches_level_1_kink_without_status()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        var ctx = new Recorder(mode, character);
        var result = await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "wanton",
            Source = "exposure",
            Willing = true,
            Delta = 3,
        }, ctx);

        Assert.True(result.Success);
        Assert.Equal("wanton:3:1:willing:0", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.Contains("wanton", ConsentGate.GetStringList(bob, LewdKeys.Kinks));
        Assert.DoesNotContain(character.SystemStats.StatusEffects, e => e.ConditionName == "imprint:wanton");
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.IntrusiveThoughts));
    }

    [Fact]
    public async Task Unwilling_save_negates_and_failure_injects_kink_plus_thought()
    {
        var (mode, bob) = Mode();
        bob.State[LewdKeys.Consent] = LewdKeys.ConsentUnwilling;
        var character = Character("bob");
        var ctx = new Recorder(mode, character) { Tone = LewdKeys.ToneGrimdark };
        var handler = new LewdImprintHandler();

        var saved = await handler.ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "ordeal",
            Source = "bad_end",
            Ability = "int",
            D20 = 18,
            AbilityMod = 1,
        }, ctx);
        Assert.True(saved.Success);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.Imprints));

        var failed = await handler.ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "ordeal",
            Source = "bad_end",
            Ability = "wis",
            D20 = 2,
        }, ctx);
        Assert.True(failed.Success);
        Assert.Contains("ordeal:1:0:unwilling:0", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.DoesNotContain("ordeal", ConsentGate.GetStringList(bob, LewdKeys.Kinks));
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.IntrusiveThoughts));

        await handler.ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "ordeal",
            Source = "exposure",
            D20 = 1,
            Delta = 2,
        }, ctx);
        Assert.Contains("ordeal", ConsentGate.GetStringList(bob, LewdKeys.Kinks));
        Assert.DoesNotContain("ordeal", ConsentGate.GetStringList(bob, LewdKeys.SoftLimits));
        Assert.Equal("imprint:ordeal", character.SystemStats.Traits[LewdKeys.IntrusiveThoughts]);
        Assert.Equal(1, ConsentGate.GetInt(bob, LewdKeys.ImprintInhib));
    }

    [Fact]
    public async Task Hard_limit_and_consensual_unwilling_fail()
    {
        var (mode, bob) = Mode();
        bob.State[LewdKeys.HardLimits] = new List<string> { "training" };
        var character = Character("bob");
        var ctx = new Recorder(mode, character);
        var blocked = await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "training",
            Source = "training",
            Willing = true,
        }, ctx);
        Assert.False(blocked.Success);

        bob.State[LewdKeys.HardLimits] = new List<string>();
        bob.State[LewdKeys.Consent] = LewdKeys.ConsentUnwilling;
        var tone = await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "wanton",
            Source = "wanton",
            D20 = 1,
        }, ctx);
        Assert.False(tone.Success);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.Imprints));
    }

    [Fact]
    public async Task First_write_applies_stored_jump_once()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.BadEndImprintTrack] = "ordeal";
        character.SystemStats.Traits[LewdKeys.BadEndImprintJump] = "3";
        character.SystemStats.Traits[LewdKeys.BadEndImprintOrigin] = "unwilling";
        var ctx = new Recorder(mode, character) { Tone = LewdKeys.ToneGrimdark };

        var first = await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "wanton",
            Source = "exposure",
            Willing = true,
        }, ctx);
        Assert.True(first.Success);
        Assert.Contains("ordeal:12:3:unwilling:0", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.Contains("wanton:1:0:willing:0", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.BadEndImprintTrack));
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "imprint:ordeal");
        Assert.Equal("edge_puppet", character.SystemStats.Traits["imprint.ordeal.feat"]);
        Assert.Contains("imprint:ordeal", character.SystemStats.Traits[LewdKeys.IntrusiveThoughts]);

        character.SystemStats.Traits[LewdKeys.BadEndImprintTrack] = "cruelty";
        character.SystemStats.Traits[LewdKeys.BadEndImprintJump] = "1";
        character.SystemStats.Traits[LewdKeys.BadEndImprintOrigin] = "willing";
        await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "wanton",
            Source = "exposure",
            Willing = true,
        }, ctx);
        Assert.Contains("cruelty:3:1:willing:0", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.BadEndImprintJump));
        _ = bob;
    }

    [Fact]
    public async Task Decondition_drops_and_refuses_a_live_brand_core()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.Imprints] = "ordeal:7:2:unwilling:0|breeding:3:1:willing:0";
        character.SystemStats.Traits[LewdKeys.Lustbrands] = "fertility:2";
        var ctx = new Recorder(mode, character);
        var handler = new LewdDeconditionHandler();

        var locked = await handler.ApplyAsync(new LewdDeconditionChange
        {
            TargetId = "bob",
            Category = "breeding",
            Method = "therapy",
            D20 = 20,
            WisMod = 5,
        }, ctx);
        Assert.False(locked.Success);

        var harsh = await handler.ApplyAsync(new LewdDeconditionChange
        {
            TargetId = "bob",
            Category = "ordeal",
            Method = "aftercare",
            D20 = 11,
            WisMod = 1,
        }, ctx);
        Assert.True(harsh.Success);
        Assert.Contains("ordeal:7:2:unwilling:0", character.SystemStats.Traits[LewdKeys.Imprints]);

        var therapy = await handler.ApplyAsync(new LewdDeconditionChange
        {
            TargetId = "bob",
            Category = "ordeal",
            Method = "therapy",
            D20 = 11,
            WisMod = 1,
        }, ctx);
        Assert.True(therapy.Success);
        Assert.Contains("ordeal:6:1:unwilling:0", character.SystemStats.Traits[LewdKeys.Imprints]);
        _ = bob;
    }

    [Fact]
    public async Task Accept_converts_origin()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.Imprints] = "training:3:1:unwilling:0";
        var ctx = new Recorder(mode, character);
        var result = await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "training",
            Source = "training",
            Accept = true,
        }, ctx);
        Assert.True(result.Success);
        Assert.Contains("training:3:1:willing:0", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.Equal(0, ConsentGate.GetInt(bob, LewdKeys.ImprintInhib));
    }

    [Fact]
    public async Task Bitchsuit_bind_ticks_training()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        var ctx = new Recorder(mode, character);
        var result = await new LewdBindHandler().ApplyAsync(new LewdBindChange
        {
            ActorId = "alice",
            TargetId = "bob",
            ItemId = "bitchsuit",
            Sites = ["torso"],
        }, ctx);
        Assert.True(result.Success);
        Assert.Contains("training:1:0:willing:0", character.SystemStats.Traits[LewdKeys.Imprints]);
    }

    [Fact]
    public async Task Rest_observer_cancels_exposure_skips_level_3_and_restamps()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.Imprints] = "wanton:4:1:willing:1|ordeal:12:3:unwilling:1";
        character.SystemStats.Traits["imprint.wanton.exposed"] = "true";
        var ctx = new Recorder(mode, character);
        var observer = new LewdImprintObserver();
        Assert.False(observer.IsInterestedIn(new LewdAdvanceChange { ActorId = "alice", TargetId = "bob" }, ctx));

        await observer.OnCommittedAsync(new RestChange
        {
            CharacterId = "bob",
            LocationId = "loc",
            RestType = RestType.LongRest,
            IntendedHours = 8,
        }, ctx);
        Assert.Contains("wanton:4:1:willing:1", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.Contains("ordeal:12:3:unwilling:1", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey("imprint.wanton.exposed"));
        Assert.Contains(ctx.Messages, m => m.Contains("cancels"));
        Assert.Contains(ctx.Messages, m => m.Contains("therapy"));

        character.SystemStats.StatusEffects.Clear();
        await observer.OnCommittedAsync(new StatusRemove { CharacterId = "bob", Status = "Imprint: Ordeal" }, ctx);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "imprint:ordeal");
        _ = bob;
    }

[Fact]
    public async Task Rest_observer_applies_pending_bad_end_imprint_jump()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.BadEndImprintTrack] = "breeding";
        character.SystemStats.Traits[LewdKeys.BadEndImprintJump] = "2";
        character.SystemStats.Traits[LewdKeys.BadEndImprintOrigin] = "unwilling";
        var ctx = new Recorder(mode, character);

        await new LewdImprintObserver().OnCommittedAsync(new RestChange
        {
            CharacterId = "bob",
            LocationId = "loc",
            RestType = RestType.ShortRest,
            IntendedHours = 1,
        }, ctx);

        Assert.Contains("breeding:7:2:unwilling:0", character.SystemStats.Traits[LewdKeys.Imprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.BadEndImprintTrack));
        Assert.Contains(ctx.Messages, m => m.Contains("bad-end imprint jump"));
        _ = bob;
    }

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
