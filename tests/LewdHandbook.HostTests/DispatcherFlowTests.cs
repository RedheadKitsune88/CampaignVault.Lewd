using System.Reflection;
using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Data.Events;
using CampaignVault.Events;
using CampaignVault.Models;
using CampaignVault.Rulesets.Modes;
using LewdHandbook.Changes;
using LewdHandbook.Events;
using LewdHandbook.Mechanics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Raven.Client.Documents.Session;
using Xunit;

namespace LewdHandbook.HostTests;

/// <summary>
/// The plugin inside core's real <see cref="WorldChangeDispatcher"/>: its handlers, observers and event reactions are
/// wired the way the host wires them (discovered from the plugin assembly), core's mode handler drives turns, and the
/// session is a stub that serves this test's documents.
/// </summary>
public sealed class DispatcherFlowTests
{
    private const string Campaign = "test";
    private static readonly Assembly Plugin = typeof(LewdEncounterMode).Assembly;
    private readonly CampaignDocumentKeys _keys = new();
    private readonly Dictionary<string, Character> _characters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Location> _locations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["locations/camp"] = new Location { Id = "locations/camp", Name = "Camp" },
        ["locations/road"] = new Location { Id = "locations/road", Name = "Road" },
    };
    private readonly ModeEncounter _encounter;
    private readonly CampaignTime _time = new();

    public DispatcherFlowTests()
    {
        foreach (var id in new[] { "chars/alice", "chars/bob" })
        {
            var c = new Character { Id = id, Name = id, LifeStage = LifeStage.Adult, SystemStats = new Dnd5eExtension { Level = 1 } };
            c.SystemStats.ResourcePools[LewdKeys.PoolArousal] = new ResourcePool { Current = 0, Max = 10, Recovery = RecoveryType.Never };
            _characters[id] = c;
        }

        _encounter = new LewdEncounterMode().StateMachine.CreateEncounter("locations/room", ["chars/alice", "chars/bob"]);
        _encounter.Id = _keys.ModeCurrent(Campaign, LewdEncounterMode.ModeIdValue);
        _encounter.ModeId = LewdEncounterMode.ModeIdValue;
        _encounter.IsActive = true;
    }

    private static IEnumerable<T> PluginInstances<T>() =>
        Plugin.GetTypes()
            .Where(t => typeof(T).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false } &&
                        t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (T)Activator.CreateInstance(t)!);

    private async Task<CommitResult> CommitAsync(params WorldChange[] changes)
    {
        var session = Substitute.For<IAsyncDocumentSession>();
        session.LoadAsync<Character>(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<IEnumerable<string>>()
                .Where(_characters.ContainsKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(id => id, id => _characters[id]));
        session.LoadAsync<Character>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => _characters.GetValueOrDefault(ci.Arg<string>())!);
        session.LoadAsync<Item>(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<string, Item>());
        session.LoadAsync<Location>(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<IEnumerable<string>>()
                .Where(_locations.ContainsKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(id => id, id => _locations[id]));
        session.LoadAsync<CampaignConfig>(Arg.Any<string>())
            .Returns(new CampaignConfig { Id = _keys.Config(Campaign), ActiveSystem = "dnd5e", EnabledModeIds = [LewdEncounterMode.ModeIdValue] });
        session.LoadAsync<CampaignConfig>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CampaignConfig { Id = _keys.Config(Campaign), ActiveSystem = "dnd5e", EnabledModeIds = [LewdEncounterMode.ModeIdValue] });
        session.LoadAsync<ModeEncounter>(Arg.Any<string>()).Returns(_encounter);
        session.LoadAsync<ModeEncounter>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_encounter);

        var selector = new InteractionModeSelector([new LewdEncounterMode()]);
        var handlers = new List<IWorldChangeHandler>
        {
            new ModeTransitionChangeHandler(selector, _keys),
            new TravelChangeHandler(new EncounterResolver()),
            new TimeAdvancedChangeHandler(),
        };
        handlers.AddRange(PluginInstances<IWorldChangeHandler>());
        var dispatcher = new WorldChangeDispatcher(
            handlers,
            _keys,
            NullLogger<WorldChangeDispatcher>.Instance,
            observers: PluginInstances<IWorldChangeObserver>(),
            eventHandlers: PluginInstances<IDomainEventHandler>(),
            eventSources: new PluginEventSources([(Plugin, LewdTraitsUpgrader.PluginIdValue, LewdEvents.All)]),
            modeSelector: selector,
            timeObservers: PluginInstances<IWorldTimeObserver>(),
            rollModifiers: new CampaignVault.Rulesets.RollModifierPipeline(PluginInstances<IRollModifierProvider>()));

        return await dispatcher.DispatchAsync(
            session, changes, Campaign,
            () => Task.FromResult(_time),
            () => Task.FromResult(new Dictionary<string, string>()),
            _ => Task.CompletedTask);
    }

    private static LewdAdvanceChange Advance(string actor, string target, int amount = 2) => new()
    {
        ActorId = actor,
        TargetId = target,
        Kind = "martial",
        StimulationAmount = amount,
        StimulationType = "bludgeoning",
        Tags = ["touch"],
        Hit = true,
    };

    private static ModeTransitionChange Turn() => new() { ModeId = LewdEncounterMode.ModeIdValue, Action = "turn" };

    [Fact]
    public async Task Core_refuses_a_second_advance_in_the_same_turn()
    {
        var result = await CommitAsync(Advance("chars/alice", "chars/bob"), Advance("chars/alice", "chars/bob"));

        Assert.False(result.Success);
        Assert.Contains(result.Summary, s => s.Contains("mode_transition action=turn"));
    }

    [Fact]
    public async Task Out_of_turn_advances_are_refused_until_the_turn_passes()
    {
        var early = await CommitAsync(Advance("chars/bob", "chars/alice"));
        Assert.False(early.Success);

        var result = await CommitAsync(Advance("chars/alice", "chars/bob"), Turn(), Advance("chars/bob", "chars/alice"));

        Assert.True(result.Success, string.Join(" | ", result.Summary));
        Assert.Equal("chars/bob", _encounter.ActiveTurnId);
        Assert.Equal(2, _characters["chars/alice"].SystemStats.ResourcePools[LewdKeys.PoolArousal].Current);
    }

    [Fact]
    public async Task Turn_event_runs_lewd_turn_start_for_whoever_is_up()
    {
        _characters["chars/bob"].SystemStats.ResourcePools[LewdKeys.PoolArousal].Current = 10;

        var result = await CommitAsync(Turn());

        Assert.True(result.Success, string.Join(" | ", result.Summary));
        Assert.True(ConsentGate.GetBool(_encounter.Participants[1], LewdKeys.Edging));
        Assert.Contains(result.Summary, s => s.Contains("make a climax save"));
    }

    [Fact]
    public async Task Exit_event_runs_lewd_scene_end()
    {
        LewdPoolHelper.SetEdging(_encounter.Participants[1], _characters["chars/bob"], true);

        var result = await CommitAsync(new ModeTransitionChange { ModeId = LewdEncounterMode.ModeIdValue, Action = "exit" });

        Assert.True(result.Success, string.Join(" | ", result.Summary));
        Assert.DoesNotContain(_characters["chars/bob"].SystemStats.StatusEffects, e => e.Name == LewdKeys.ConditionEdging);
    }

    [Fact]
    public async Task A_climax_forces_the_echoes_bearer_chained_to_it_through_the_event_pipeline()
    {
        var bob = _characters["chars/bob"];
        bob.SystemStats.Traits[LewdKeys.Lustbrands] = "echoes:2";
        bob.SystemStats.Traits[LewdKeys.TraitBindings] =
            "[{\"id\":\"c1\",\"kind\":\"chain\",\"anchorId\":\"chars/alice\",\"sites\":[\"neck\"]}]";

        // 10 stimulation against max 10 is an instant climax for alice.
        _encounter.ActiveTurnId = "chars/bob";
        _encounter.Participants[0].ActionBudget["action"] = 0;
        _encounter.Participants[1].ActionBudget["action"] = 1;
        var result = await CommitAsync(Advance("chars/bob", "chars/alice", amount: 10));

        Assert.True(result.Success, string.Join(" | ", result.Summary));
        Assert.True(ConsentGate.GetBool(_encounter.Participants[1], LewdKeys.ClimaxIncapacitated));
        Assert.Contains(result.Summary, s => s.Contains("climax check chars/bob"));
    }

    [Fact]
    public async Task A_forced_climax_with_no_scene_blocks_actions_until_the_clock_moves_on()
    {
        _encounter.IsActive = false;
        var climax = await CommitAsync(new LewdClimaxCheckChange { TargetId = "chars/alice", ForceClimax = true });
        Assert.True(climax.Success, string.Join(" | ", climax.Summary));

        var escape = new LewdEscapeChange { CharacterId = "chars/alice", ActorId = "chars/alice", Method = "slip" };
        var blocked = await CommitAsync(escape);
        Assert.False(blocked.Success);
        Assert.Contains(blocked.Summary, s => s.Contains("cannot act"));

        _time.AdvanceHours(2);
        var later = await CommitAsync(new LewdEscapeChange { CharacterId = "chars/alice", ActorId = "chars/alice", Method = "slip" });
        Assert.DoesNotContain(later.Summary, s => s.Contains("cannot act"));
    }

    [Fact]
    public async Task Vice_contributor_reads_campaign_time_from_the_hosts_context_turn()
    {
        var bob = _characters["chars/bob"];
        bob.SystemStats.Traits[ViceState.AddictedKey("alcohol")] = "true";
        bob.SystemStats.Attributes[ViceState.LastHoursKey("alcohol")] = 0;
        var turn = new CampaignVault.Data.Context.ContextTurn
        {
            Session = Substitute.For<IAsyncDocumentSession>(),
            CampaignName = Campaign,
            Config = new CampaignConfig { EnabledModeIds = [LewdEncounterMode.ModeIdValue] },
            AppliedChanges = [],
            InvolvedEntityIds = [],
            Party = [bob],
            Time = new CampaignTime { Hour = 5 },
        };

        var item = Assert.Single(await new LewdHandbook.Guidance.LewdViceContributor().ContributeAsync(turn));

        Assert.Contains("withdrawal from alcohol", item.Text);
    }

    [Fact]
    public async Task A_chained_convict_only_travels_together_with_the_chain_holder()
    {
        _encounter.IsActive = false;
        foreach (var c in _characters.Values)
            c.CurrentLocationId = "locations/camp";
        var bind = await CommitAsync(new LewdBindChange
        {
            ActorId = "chars/alice", TargetId = "chars/bob", Kind = "shackles", AnchorId = "chars/alice", Willing = true,
        });
        Assert.True(bind.Success, string.Join(" | ", bind.Summary));

        var alone = await CommitAsync(new TravelChange { CharacterId = "chars/bob", DestinationLocationId = "locations/road" });
        Assert.False(alone.Success);
        Assert.Contains(alone.Summary, s => s.Contains("cannot travel"));

        var together = await CommitAsync(
            new TravelChange { CharacterId = "chars/alice", DestinationLocationId = "locations/road" },
            new TravelChange { CharacterId = "chars/bob", DestinationLocationId = "locations/road" });
        Assert.DoesNotContain(together.Summary, s => s.Contains("cannot travel"));
    }

    [Fact]
    public async Task Days_in_restraints_wear_a_captive_down_through_the_hosts_time_hook()
    {
        _encounter.IsActive = false;
        var bind = await CommitAsync(new LewdBindChange
        {
            ActorId = "chars/alice", TargetId = "chars/bob", Kind = "rope", Sites = ["wrists", "ankles"], Erotic = false, Willing = true,
        });
        Assert.True(bind.Success, string.Join(" | ", bind.Summary));

        foreach (var step in Enumerable.Range(1, 4))
        {
            var span = await CommitAsync(new TimeAdvancedChange
            {
                Source = "rest", Hours = 6, BucketHours = 6, TotalHoursAfter = 6 * step, CharacterIds = ["chars/bob"],
            });
            Assert.True(span.Success, string.Join(" | ", span.Summary));
        }

        var effects = _characters["chars/bob"].SystemStats.StatusEffects;
        Assert.Equal("Dead arms", Assert.Single(effects, e => e.EffectKey == RestraintStrain.ArmsKey).Name);
        Assert.Equal("Failing legs", Assert.Single(effects, e => e.EffectKey == RestraintStrain.LegsKey).Name);
    }

    [Fact]
    public async Task An_escape_roll_goes_through_the_hosts_pipeline_so_aftermath_numbers_count_and_the_roll_says_why()
    {
        _encounter.IsActive = false;
        var bind = await CommitAsync(new LewdBindChange
        {
            ActorId = "chars/alice", TargetId = "chars/bob", Kind = "rope", Sites = ["wrists"], Orientation = "behind", Willing = true,
        });
        Assert.True(bind.Success, string.Join(" | ", bind.Summary));
        // "Dead arms"-style number on a status effect: core's own layer folds it into the plugin's escape check.
        _characters["chars/bob"].SystemStats.StatusEffects.Add(
            new StatusEffect { Name = "Numb arms", StatModifiers = { ["AllChecks"] = -3 } });

        var escape = await CommitAsync(new LewdEscapeChange { CharacterId = "chars/bob", Method = "slip", D20 = 12 });

        Assert.True(escape.Success, string.Join(" | ", escape.Summary));
        Assert.True(escape.Summary.Any(s => s.Contains("3=9 vs DC")), string.Join(" | ", escape.Summary));
    }

    [Fact]
    public async Task Ordinary_checks_by_a_bound_character_are_hampered_but_an_escape_is_not()
    {
        _encounter.IsActive = false;
        await CommitAsync(new LewdBindChange
        {
            ActorId = "chars/alice", TargetId = "chars/bob", Kind = "rope", Sites = ["wrists"], Orientation = "behind", Willing = true,
        });
        var bob = _characters["chars/bob"];
        var pipeline = new CampaignVault.Rulesets.RollModifierPipeline(PluginInstances<IRollModifierProvider>());
        RollQuery Q(string kind, string subject, params string[] tags) =>
            new(kind, subject, tags, bob, null, "dnd5e", new Dictionary<string, string>());

        Assert.Equal(AdvantageEffect.Disadvantage, pipeline.Resolve(Q(RollKinds.Save, "Dexterity"), 0).Advantage);
        Assert.Equal(AdvantageEffect.None, pipeline.Resolve(Q(RollKinds.Check, "dex", "escape"), 0).Advantage);
    }

    [Fact]
    public async Task A_broken_will_and_a_bound_captive_slow_a_march_by_the_hosts_speed_rules()
    {
        _encounter.IsActive = false;
        var bob = _characters["chars/bob"];
        ((Dnd5eExtension)bob.SystemStats).Movement = 30;
        await CommitAsync(new LewdBindChange
        {
            ActorId = "chars/alice", TargetId = "chars/bob", Kind = "shackles", Sites = ["ankles"], Willing = true,
        });
        var pipeline = new CampaignVault.Rulesets.RollModifierPipeline(PluginInstances<IRollModifierProvider>());

        Assert.Equal(5, pipeline.Speed(bob, new Dictionary<string, string>(), "dnd5e"));
    }

    [Fact]
    public async Task A_participant_travelling_away_ends_the_scene()
    {
        _characters["chars/bob"].CurrentLocationId = "locations/room";
        _locations["locations/room"] = new Location { Id = "locations/room", Name = "Room" };

        var result = await CommitAsync(new TravelChange { CharacterId = "chars/bob", DestinationLocationId = "locations/road", TravelCostHoursOverride = 1 });

        // The stub session cannot save a travel, so only the reaction is asserted, not the commit's overall success.
        Assert.False(_encounter.IsActive);
        Assert.Contains(result.Summary, s => s.Contains("lewd_encounter ends"));
    }

    [Fact]
    public async Task Two_interruptions_in_one_commit_do_not_fail_the_commit()
    {
        _characters["chars/bob"].CurrentLocationId = "locations/room";
        _locations["locations/room"] = new Location { Id = "locations/room", Name = "Room" };

        var result = await CommitAsync(
            new TravelChange { CharacterId = "chars/bob", DestinationLocationId = "locations/road", TravelCostHoursOverride = 1 },
            new TravelChange { CharacterId = "chars/alice", DestinationLocationId = "locations/road", TravelCostHoursOverride = 1 });

        Assert.False(_encounter.IsActive);
        Assert.DoesNotContain(result.Summary, s => s.Contains("No active encounter") || s.Contains("faulted"));
        Assert.Single(result.Summary, s => s.StartsWith("lewd_encounter ends"));
    }

    [Fact]
    public async Task Join_brings_a_third_adult_in_and_refuses_an_unset_life_stage()
    {
        _characters["chars/cara"] = new Character { Id = "chars/cara", Name = "cara", LifeStage = LifeStage.Adult, SystemStats = new Dnd5eExtension { Level = 1 } };
        _characters["chars/kit"] = new Character { Id = "chars/kit", Name = "kit", SystemStats = new Dnd5eExtension { Level = 1 } };

        var refused = await CommitAsync(new ModeTransitionChange { ModeId = LewdEncounterMode.ModeIdValue, Action = "join", ParticipantIds = ["chars/kit"] });
        Assert.False(refused.Success);
        Assert.Equal(2, _encounter.Participants.Count);

        var joined = await CommitAsync(new ModeTransitionChange { ModeId = LewdEncounterMode.ModeIdValue, Action = "join", ParticipantIds = ["chars/cara"] });
        Assert.True(joined.Success, string.Join(" | ", joined.Summary));
        Assert.Equal(3, _encounter.Participants.Count);
        Assert.Equal(0, _encounter.Participants[2].ActionBudget["action"]);
        Assert.True(ConsentGate.GetInt(_encounter.Participants[2], LewdKeys.Overstimulation) == 0);
    }

    [Fact]
    public async Task Engine_only_verbs_cannot_be_sent_by_the_model()
    {
        var result = await CommitAsync(new LewdRestChange { CharacterId = "chars/bob", RestType = "long" });

        Assert.False(result.Success);
        Assert.Contains(result.Summary, s => s.Contains("emitted by the engine"));
    }
}
