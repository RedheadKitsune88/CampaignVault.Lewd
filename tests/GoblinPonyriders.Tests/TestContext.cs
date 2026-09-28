using CampaignVault.Data;
using CampaignVault.Data.ChangeHandlers;
using CampaignVault.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoblinPonyriders.Tests;

internal sealed class TestContext : IChangeContext
{
    private readonly Dictionary<string, Character> _characters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Faction> _factions = new(StringComparer.OrdinalIgnoreCase);

    public TestContext(params Character[] characters)
    {
        foreach (var c in characters)
            _characters[c.Id] = c;
    }

    public static Character Adult(string id) =>
        new() { Id = id, Name = id, LifeStage = LifeStage.Adult, SystemStats = new SystemExtension() };

    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["goblinClans"] = "on",
    };

    public List<string> Messages { get; } = [];
    public List<(string Topic, object? Data)> Published { get; } = [];

    public void Add(Faction f) => _factions[f.Id] = f;

    public IReadOnlyDictionary<string, Character> Characters => _characters;
    public IReadOnlyDictionary<string, Item> Items { get; } = new Dictionary<string, Item>();
    public IReadOnlyDictionary<string, Location> Locations { get; } = new Dictionary<string, Location>();
    public IReadOnlyDictionary<string, Faction> Factions => _factions;
    public IReadOnlyDictionary<string, Quest> Quests { get; } = new Dictionary<string, Quest>();
    public Microsoft.Extensions.Logging.ILogger Logger { get; } = NullLogger.Instance;
    public CombatEncounter? ActiveCombat => null;
    public ModeEncounter? ActiveMode => null;
    public IReadOnlyDictionary<string, ModeEncounter> ActiveModes { get; } =
        new Dictionary<string, ModeEncounter>(StringComparer.OrdinalIgnoreCase);
    public CampaignConfig? Config => null;
    public IRollService? Rolls => null;
    public string? CampaignName => "test";
    public HashSet<string> InvolvedEntities { get; } = [];
    public IReadOnlyList<WorldChange>? Batch => null;
    public int BatchIndex => 0;
    public Func<Task<CampaignTime>> GetCurrentTimeAsync { get; } = () => Task.FromResult(new CampaignTime());
    public Func<Task<Dictionary<string, string>>> GetSystemOptionsAsync => () => Task.FromResult(Options);
    public Func<Event, Task> LogEventAsync { get; } = _ => Task.CompletedTask;
    public void RegisterNewLocation(Location loc) { }
    public void RegisterNewCharacter(Character c) { }
    public void RegisterNewItem(Item i) { }
    public void RegisterNewFaction(Faction f) => _factions[f.Id] = f;
    public void RegisterNewQuest(Quest q) { }
    public void Publish(string topic, object? data = null) => Published.Add((topic, data));
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
