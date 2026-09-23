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

public class BrandTests
{
    [Fact]
    public async Task Apply_denial_stamps_status_denied_inhib_and_glow()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        var ctx = new Recorder(mode, character) { Tone = LewdKeys.ToneGrimdark };
        var handler = new LewdApplyBrandHandler();

        var unknown = await handler.ApplyAsync(new LewdApplyBrandChange { TargetId = "bob", BrandId = "nope" }, ctx);
        Assert.False(unknown.Success);

        ctx.Tone = LewdKeys.ToneConsensual;
        var unwilling = await handler.ApplyAsync(new LewdApplyBrandChange { TargetId = "bob", BrandId = "denial" }, ctx);
        Assert.False(unwilling.Success);

        bob.State[LewdKeys.HardLimits] = new List<string> { "lustbrand" };
        var limited = await handler.ApplyAsync(new LewdApplyBrandChange
        {
            TargetId = "bob",
            BrandId = "denial",
            Willing = true,
        }, ctx);
        Assert.False(limited.Success);
        bob.State[LewdKeys.HardLimits] = new List<string>();

        var applied = await handler.ApplyAsync(new LewdApplyBrandChange
        {
            TargetId = "bob",
            BrandId = "denial",
            Willing = true,
            Payload = "permission",
        }, ctx);
        Assert.True(applied.Success);
        Assert.Equal("denial:5", character.SystemStats.Traits[LewdKeys.Lustbrands]);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "lustbrand:denial" && e.Name == "Lustbrand: Denial");
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == LewdKeys.ConditionDenied);
        Assert.Equal(5, ConsentGate.GetInt(bob, LewdKeys.LustbrandInhib));
        Assert.Equal("mark", character.SystemStats.Traits[LewdKeys.LustbrandGlow]);
        Assert.Equal(-5, character.SystemStats.StatusEffects.First(e => e.ConditionName == "lustbrand:denial").StatModifiers["Inhibition"]);

        await handler.ApplyAsync(new LewdApplyBrandChange { TargetId = "bob", BrandId = "denial", Willing = true }, ctx);
        Assert.Equal(1, character.SystemStats.StatusEffects.Count(e => e.ConditionName == "lustbrand:denial"));
    }

    [Fact]
    public async Task Remove_curse_fails_status_remove_restamps_wish_clears()
    {
        var (mode, _) = Mode();
        var character = Character("bob");
        var ctx = new Recorder(mode, character) { Tone = LewdKeys.ToneGrimdark };
        var handler = new LewdApplyBrandHandler();
        await handler.ApplyAsync(new LewdApplyBrandChange { TargetId = "bob", BrandId = "denial" }, ctx);

        var curse = await handler.ApplyAsync(new LewdApplyBrandChange
        {
            TargetId = "bob",
            BrandId = "denial",
            Action = "remove",
            Method = "remove_curse",
        }, ctx);
        Assert.False(curse.Success);

        character.SystemStats.StatusEffects.Clear();
        var observer = new LewdBrandObserver();
        await observer.OnCommittedAsync(new StatusRemove { CharacterId = "bob", Status = "Lustbrand: Denial" }, ctx);
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "lustbrand:denial");

        var wish = await handler.ApplyAsync(new LewdApplyBrandChange
        {
            TargetId = "bob",
            BrandId = "denial",
            Action = "remove",
            Method = "wish",
        }, ctx);
        Assert.True(wish.Success);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.Lustbrands));
        Assert.DoesNotContain(character.SystemStats.StatusEffects, e => e.ConditionName == "lustbrand:denial");
    }

    [Fact]
    public async Task Addiction_locks_fluids_and_remove_clears_only_the_lock()
    {
        var (mode, _) = Mode();
        var character = Character("bob");
        var ctx = new Recorder(mode, character) { Tone = LewdKeys.ToneGrimdark };
        await new LewdApplyBrandHandler().ApplyAsync(new LewdApplyBrandChange { TargetId = "bob", BrandId = "addiction" }, ctx);

        Assert.Equal("true", character.SystemStats.Traits[BrandState.ViceLocked]);
        Assert.Equal("true", character.SystemStats.Traits[BrandState.ViceAddicted]);
        Assert.Equal(18, character.SystemStats.Attributes[BrandState.ViceDc]);
        Assert.False(character.SystemStats.Traits.ContainsKey("vice.sexual_fluids.withdrawal"));
        Assert.Contains(character.SystemStats.StatusEffects, e => e.ConditionName == "vice_sexual_fluids");

        await new LewdApplyBrandHandler().ApplyAsync(new LewdApplyBrandChange
        {
            TargetId = "bob",
            BrandId = "addiction",
            Action = "remove",
            Method = "feature",
        }, ctx);
        Assert.False(character.SystemStats.Traits.ContainsKey(BrandState.ViceLocked));
        Assert.Equal("true", character.SystemStats.Traits[BrandState.ViceAddicted]);
        Assert.Equal(18, character.SystemStats.Attributes[BrandState.ViceDc]);
    }

    [Fact]
    public async Task Rest_raises_abundance_heal_lowers_altruism_max()
    {
        var (mode, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 2, Max = 10 };
        var ctx = new Recorder(mode, character) { Tone = LewdKeys.ToneGrimdark };
        var handler = new LewdApplyBrandHandler();
        await handler.ApplyAsync(new LewdApplyBrandChange { TargetId = "bob", BrandId = "abundance" }, ctx);
        await handler.ApplyAsync(new LewdApplyBrandChange { TargetId = "bob", BrandId = "altruism" }, ctx);

        var observer = new LewdBrandObserver();
        Assert.False(observer.IsInterestedIn(new ResourceChange { CharacterId = "bob", PoolName = "arousal", Delta = 1 }, ctx));
        await observer.OnCommittedAsync(new RestChange
        {
            CharacterId = "bob",
            LocationId = "loc",
            RestType = RestType.LongRest,
            IntendedHours = 8,
        }, ctx);
        Assert.Equal("1", character.SystemStats.Traits["lustbrand.abundance.endowment"]);

        await observer.OnCommittedAsync(new HpChange { CharacterId = "bob", Delta = 4 }, ctx);
        Assert.Equal(6, character.SystemStats.ResourcePools[LewdKeys.PoolArousal].Max);
        Assert.Contains(ctx.Messages, m => m.Contains("did not roll"));
        _ = bob;
    }

    [Fact]
    public void Ruin_spends_a_die_and_does_not_count_as_climax()
    {
        var (_, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.Lustbrands] = "ruin:1";
        character.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice] = new ResourcePool { Current = 2, Max = 4 };
        var arousal = new ResourcePool { Current = 8, Max = 10 };
        var note = LewdAdvanceHandler.ApplyClimaxResult(bob, character, arousal, Climax());
        Assert.Contains("Ruin", note);
        Assert.Equal(0, ConsentGate.GetInt(bob, LewdKeys.ClimaxStreak));
        Assert.Equal(1, character.SystemStats.ResourcePools[LewdKeys.PoolRecoveryDice].Current);
        Assert.Equal(7, arousal.Current);
        Assert.False(ConsentGate.GetBool(bob, LewdKeys.LustbrandJustClimaxed));
    }

    [Fact]
    public void Inhib_penalty_stacks_after_the_willing_floor()
    {
        var (_, bob) = Mode();
        bob.State[LewdKeys.Inhibition] = 3;
        bob.State[LewdKeys.LustbrandInhib] = 5;
        Assert.Equal(-5, ConsentGate.EffectiveInhibition(bob, advanceIsWanted: true));
    }

    [Fact]
    public void Concubi_rebrand_on_next_climax_and_echo_does_not_climax()
    {
        var (_, bob) = Mode();
        var character = Character("bob");
        character.SystemStats.Traits[LewdKeys.Lustbrands] = "echoes:2";
        character.SystemStats.Traits[LewdKeys.LustbrandConcubi] = "true";
        BrandState.Remove(bob, character, "echoes", concubi: true, context: null);
        Assert.Equal("3", character.SystemStats.Traits[LewdKeys.LustbrandPendingRebrand]);

        var arousal = new ResourcePool { Current = 4, Max = 10 };
        LewdAdvanceHandler.ApplyClimaxResult(bob, character, arousal, Climax());
        Assert.Equal("echoes:3", character.SystemStats.Traits[LewdKeys.Lustbrands]);
        Assert.False(character.SystemStats.Traits.ContainsKey(LewdKeys.LustbrandPendingRebrand));

        var echoed = Character("alice");
        echoed.SystemStats.Traits[LewdKeys.Lustbrands] = "echoes:2";
        echoed.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 1, Max = 10 };
        var alice = Mode().bob;
        alice.CharacterId = "alice";
        BrandState.EchoStim(alice, echoed, 4, null);
        Assert.Equal(5, echoed.SystemStats.ResourcePools[LewdKeys.PoolArousal].Current);
        Assert.False(ConsentGate.GetBool(alice, LewdKeys.LustbrandJustClimaxed));
    }

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
            Characters = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase) { [character.Id] = character };
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
