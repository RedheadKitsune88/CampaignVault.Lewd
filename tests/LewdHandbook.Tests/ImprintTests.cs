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
        cruel.SystemStats.Traits[LewdKeys.TraitImprints] = "cruelty:12:3:willing:0";
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
        Assert.Equal("wanton:3:1:willing:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.Contains("wanton", ConsentGate.GetStringList(bob, LewdKeys.Kinks));
        Assert.DoesNotContain(character.SystemStats.StatusEffects, e => e.ConditionName == "imprint:wanton");
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitIntrusiveThoughts));
    }

    [Fact]
    public async Task Unwilling_save_negates_and_failure_injects_kink_plus_thought()
    {
        var (mode, bob) = Mode();
        bob.State[LewdKeys.Consent] = LewdKeys.ConsentUnwilling;
        var character = Character("bob");
        var ctx = new Recorder(mode, character) { Tone = LewdKeys.NonConsentOn };
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
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitImprints));

        var failed = await handler.ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "ordeal",
            Source = "bad_end",
            Ability = "wis",
            D20 = 2,
        }, ctx);
        Assert.True(failed.Success);
        Assert.Contains("ordeal:1:0:unwilling:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.DoesNotContain("ordeal", ConsentGate.GetStringList(bob, LewdKeys.Kinks));
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitIntrusiveThoughts));

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
        Assert.Equal("imprint:ordeal", character.SystemStats.Traits[LewdKeys.TraitIntrusiveThoughts]);
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
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitImprints));
    }

    [Fact]
    public async Task First_write_applies_stored_jump_once()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintTrack] = "ordeal";
        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintJump] = "3";
        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintOrigin] = "unwilling";
        var ctx = new Recorder(mode, character) { Tone = LewdKeys.NonConsentOn };

        var first = await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "wanton",
            Source = "exposure",
            Willing = true,
        }, ctx);
        Assert.True(first.Success);
        Assert.Contains("ordeal:12:3:unwilling:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.Contains("wanton:1:0:willing:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitBadEndImprintTrack));
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "imprint:ordeal");
        Assert.Equal("edge_puppet", character.SystemStats.Traits[LewdKeys.ImprintTraitPrefix + "ordeal.feat"]);
        Assert.Contains("imprint:ordeal", character.SystemStats.Traits[LewdKeys.TraitIntrusiveThoughts]);

        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintTrack] = "cruelty";
        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintJump] = "1";
        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintOrigin] = "willing";
        await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "wanton",
            Source = "exposure",
            Willing = true,
        }, ctx);
        Assert.Contains("cruelty:3:1:willing:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitBadEndImprintJump));
        _ = bob;
    }

    [Fact]
    public async Task Decondition_drops_and_refuses_a_live_brand_core()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.TraitImprints] = "ordeal:7:2:unwilling:0|breeding:3:1:willing:0";
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
        Assert.Contains("ordeal:7:2:unwilling:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);

        var therapy = await handler.ApplyAsync(new LewdDeconditionChange
        {
            TargetId = "bob",
            Category = "ordeal",
            Method = "therapy",
            D20 = 11,
            WisMod = 1,
        }, ctx);
        Assert.True(therapy.Success);
        Assert.Contains("ordeal:6:1:unwilling:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        _ = bob;
    }

    [Fact]
    public async Task Accept_converts_origin()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.TraitImprints] = "training:3:1:unwilling:0";
        var ctx = new Recorder(mode, character);
        var result = await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob",
            Category = "training",
            Source = "training",
            Accept = true,
        }, ctx);
        Assert.True(result.Success);
        Assert.Contains("training:3:1:willing:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);
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
        // In a scene the pressure is only recorded; the scene end resolves one tick per track.
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitImprints));
        Assert.Equal("training:willing:alice", character.SystemStats.Traits[LewdKeys.TraitSceneImprints]);

        var end = await new LewdSceneEndHandler().ApplyAsync(new LewdSceneEndChange { ParticipantIds = ["bob"] }, ctx);
        Assert.True(end.Success);
        Assert.Contains("training:1:0:willing:0:alice", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitSceneImprints));
    }

    [Fact]
    public void An_imprint_tied_to_someone_only_weighs_against_them()
    {
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.TraitImprints] = "ordeal:7:2:unwilling:1:chars/captor|wanton:3:1:unwilling:1";

        Assert.Equal(3, ImprintState.InhibitionPenalty(character, "chars/captor"));
        Assert.Equal(1, ImprintState.InhibitionPenalty(character, "chars/stranger"));
        Assert.Equal(1, ImprintState.InhibitionPenalty(character, null));
        Assert.Equal("chars/captor", ImprintState.Find(character, "ordeal")!.Value.AnchorId);
    }

    [Fact]
    public async Task Seeded_backstory_imprint_can_name_its_anchor()
    {
        var (mode, _) = Mode();
        var character = Character("bob");
        var ctx = new Recorder(mode, character);

        var result = await new LewdImprintHandler().ApplyAsync(new LewdImprintChange
        {
            TargetId = "bob", Category = "training", SetLevel = 3, Willing = true, AnchorId = "chars/partner",
        }, ctx);

        Assert.True(result.Success, result.Message);
        var effect = Assert.Single(character.SystemStats.StatusEffects, e => e.ConditionName == "imprint:training");
        Assert.Contains("chars/partner", effect.RecoveryHint);
    }

    [Fact]
    public async Task Rest_observer_cancels_exposure_skips_level_3_and_restamps()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.TraitImprints] = "wanton:4:1:willing:1|ordeal:12:3:unwilling:1";
        character.SystemStats.Traits[LewdKeys.ImprintTraitPrefix + "wanton.exposed"] = "true";
        var ctx = new Recorder(mode, character);
        var observer = new LewdImprintObserver();
        Assert.False(observer.IsInterestedIn(new LewdAdvanceChange { ActorId = "alice", TargetId = "bob" }, ctx));

        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "bob", RestType = "long" }, ctx);
        Assert.Contains("wanton:4:1:willing:1", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.Contains("ordeal:12:3:unwilling:1", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.ImprintTraitPrefix + "wanton.exposed"));
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
        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintTrack] = "breeding";
        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintJump] = "2";
        character.SystemStats.Traits[LewdKeys.TraitBadEndImprintOrigin] = "unwilling";
        var ctx = new Recorder(mode, character);

        await new LewdRestHandler().ApplyAsync(new LewdRestChange { CharacterId = "bob", RestType = "short" }, ctx);

        Assert.Contains("breeding:7:2:unwilling:0", character.SystemStats.Traits[LewdKeys.TraitImprints]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.TraitBadEndImprintTrack));
        Assert.Contains(ctx.Messages, m => m.Contains("bad-end imprint jump"));
        _ = bob;
    }

    private static (ModeEncounter mode, ModeParticipantState bob) Mode()
    {
        var mode = new LewdEncounterMode().StateMachine.CreateEncounter("loc", ["alice", "bob"]);
        return (mode, mode.Participants[1]);
    }

    private static Character Character(string id) => new()
    { LifeStage = LifeStage.Adult,
        Id = id,
        Name = id,
        SystemStats = new SystemExtension(),
    };

    private sealed class Recorder : IChangeContext
    {
        public Recorder(ModeEncounter mode, Character character)
        {
            ActiveMode = mode;
            Characters = WithActor(new Dictionary<string, Character> { [character.Id] = character });
        }

        public List<string> Messages { get; } = [];
        public string Tone { get; set; } = LewdKeys.NonConsentOff;
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
        public Func<Task<Dictionary<string, string>>> GetSystemOptionsAsync =>
            () => Task.FromResult(new Dictionary<string, string> { [LewdKeys.NonConsentOption] = Tone });
        public Func<Event, Task> LogEventAsync { get; } = _ => Task.CompletedTask;
        public void RegisterNewLocation(Location loc) { }
        public void RegisterNewCharacter(Character c) { }
        public void RegisterNewItem(Item i) { }
        public void RegisterNewFaction(Faction f) { }
        public void RegisterNewQuest(Quest q) { }
        public void Publish(string topic, object? data = null) =>
            Published.Add((topic, data));
        public List<(string Topic, object? Data)> Published { get; } = [];
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

    private static Dictionary<string, Character> WithActor(Dictionary<string, Character> characters)
    {
        characters.TryAdd("alice", new Character { Id = "alice", Name = "alice", LifeStage = LifeStage.Adult });
        return characters;
    }
}
