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
        };
        handlers.AddRange(PluginInstances<IWorldChangeHandler>());
        var dispatcher = new WorldChangeDispatcher(
            handlers,
            _keys,
            NullLogger<WorldChangeDispatcher>.Instance,
            observers: PluginInstances<IWorldChangeObserver>(),
            eventHandlers: PluginInstances<IDomainEventHandler>(),
            eventSources: new PluginEventSources([(Plugin, LewdTraitsUpgrader.PluginIdValue, LewdEvents.All)]),
            modeSelector: selector);

        return await dispatcher.DispatchAsync(
            session, changes, Campaign,
            () => Task.FromResult(new CampaignTime()),
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
    public async Task Vice_contributor_reads_campaign_time_from_the_hosts_context_turn()
    {
        var bob = _characters["chars/bob"];
        bob.SystemStats.Traits[ViceState.AddictedKey("alcohol")] = "true";
        bob.SystemStats.Attributes[ViceState.LastHoursKey("alcohol")] = 0;
        var turn = new CampaignVault.Data.Context.ContextTurn
        {
            Session = Substitute.For<IAsyncDocumentSession>(),
            CampaignName = Campaign,
            Config = new CampaignConfig(),
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
        Assert.Contains(alone.Summary, s => s.Contains("bound to"));

        var together = await CommitAsync(
            new TravelChange { CharacterId = "chars/alice", DestinationLocationId = "locations/road" },
            new TravelChange { CharacterId = "chars/bob", DestinationLocationId = "locations/road" });
        Assert.DoesNotContain(together.Summary, s => s.Contains("cannot travel because"));
    }
}
